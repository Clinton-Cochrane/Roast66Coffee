using System.Security.Cryptography;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class DataRetentionPostgresTests
{
    private static readonly DateTime NowUtc = new(2026, 9, 2, 18, 0, 0, DateTimeKind.Utc);

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task Migration_RemovesDestinationColumnsAndRedactsExistingNotificationLogs()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_retention_migration");
        if (database == null)
        {
            return;
        }

        await using (var context = database.CreateContext())
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260901153615_AddStaffIdentityAndAudit");
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO notificationmessages
                    (id, eventtype, recipientrole, recipientphone, recipientemail,
                     channel, templatekey, orderid, payloadjson, status, lasterror,
                     attemptcount, dedupkey, createdutc, updatedutc)
                VALUES
                    ('11111111-1111-1111-1111-111111111111', 'retention.test',
                     'customer', '+15558675309', 'plain@example.test', 'sms',
                     'retention_test', NULL,
                     '{{"customerName":"Plain Customer","phone":"5558675309"}}',
                     'failed', '192.0.2.10 plain@example.test', 3,
                     'plain@example.test|5558675309', now(), now());
                """);

            await migrator.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT count(*)
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'notificationmessages'
              AND column_name IN ('recipientphone', 'recipientemail');
            """;
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);

        command.CommandText =
            "SELECT payloadjson || '|' || coalesce(lasterror, '') || '|' || dedupkey " +
            "FROM notificationmessages WHERE id = '11111111-1111-1111-1111-111111111111';";
        var retainedText = (string)(await command.ExecuteScalarAsync())!;
        Assert.DoesNotContain("Plain Customer", retainedText, StringComparison.Ordinal);
        Assert.DoesNotContain("plain@example.test", retainedText, StringComparison.Ordinal);
        Assert.DoesNotContain("5558675309", retainedText, StringComparison.Ordinal);
        Assert.DoesNotContain("192.0.2.10", retainedText, StringComparison.Ordinal);
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task PurgeExpiredData_DeletesPhysicalOrderGraphPaymentsAndLogs()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_retention_graph");
        if (database == null)
        {
            return;
        }

        int orderId;
        int orderItemId;
        int addOnId;
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            var order = CreateCompletedOrder(NowUtc.AddHours(-30));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            orderId = order.Id;
            orderItemId = order.OrderItems[0].Id;
            addOnId = order.OrderItems[0].AddOns![0].Id;
            context.Payments.Add(CreatePayment(order.Id, NowUtc.AddHours(-1)));
            context.NotificationMessages.Add(CreateNotification(order.Id, "sms", "retrying", NowUtc.AddHours(-1)));
            context.AuditEvents.Add(CreateAuditEvent(NowUtc.AddDays(-90)));
            await context.SaveChangesAsync();

            var result = await CreateService(context).PurgeExpiredDataAsync();

            Assert.Equal(1, result.CompletedOrdersDeleted);
            Assert.Equal(1, result.PaymentsDeleted);
            Assert.Equal(1, result.NotificationLogsDeleted);
            Assert.Equal(1, result.AuditEventsDeleted);
        }

        await using var verification = database.CreateContext();
        Assert.Null(await verification.Orders.FindAsync(orderId));
        Assert.Null(await verification.OrderItems.FindAsync(orderItemId));
        Assert.Null(await verification.Set<AddOn>().FindAsync(addOnId));
        Assert.Empty(await verification.Payments.ToListAsync());
        Assert.Empty(await verification.NotificationMessages.ToListAsync());
        Assert.Empty(await verification.AuditEvents.ToListAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task PurgeExpiredData_ConcurrentRunsDeleteEachOrderOnce()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_retention_concurrency");
        if (database == null)
        {
            return;
        }

        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            setup.Orders.AddRange(
                Enumerable.Range(0, 12)
                    .Select(index => CreateCompletedOrder(NowUtc.AddHours(-31).AddMinutes(-index))));
            await setup.SaveChangesAsync();
        }

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var first = CreateService(firstContext, batchSize: 2).PurgeExpiredDataAsync();
        var second = CreateService(secondContext, batchSize: 2).PurgeExpiredDataAsync();
        var results = await Task.WhenAll(first, second);

        Assert.Equal(12, results.Sum(result => result.CompletedOrdersDeleted));
        await using var verification = database.CreateContext();
        Assert.Empty(await verification.Orders.ToListAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task PurgeExpiredData_FailedLaterBatchKeepsEarlierCommitAndRetryCompletes()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_retention_retry");
        if (database == null)
        {
            return;
        }

        int firstOrderId;
        int blockedOrderId;
        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            var firstOrder = CreateCompletedOrder(NowUtc.AddHours(-32));
            var blockedOrder = CreateCompletedOrder(NowUtc.AddHours(-31));
            setup.Orders.AddRange(firstOrder, blockedOrder);
            await setup.SaveChangesAsync();
            firstOrderId = firstOrder.Id;
            blockedOrderId = blockedOrder.Id;
            setup.Payments.AddRange(
                CreatePayment(firstOrder.Id, NowUtc.AddHours(-32)),
                CreatePayment(blockedOrder.Id, NowUtc.AddHours(-31), "block-delete"));
            await setup.SaveChangesAsync();
            await setup.Database.ExecuteSqlRawAsync(
                """
                CREATE FUNCTION fail_blocked_payment_delete() RETURNS trigger AS $$
                BEGIN
                    IF OLD.provider = 'block-delete' THEN
                        RAISE EXCEPTION 'forced retention failure';
                    END IF;
                    RETURN OLD;
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER fail_blocked_payment_delete
                    BEFORE DELETE ON payments
                    FOR EACH ROW EXECUTE FUNCTION fail_blocked_payment_delete();
                """);
        }

        await using (var failingContext = database.CreateContext())
        {
            await Assert.ThrowsAnyAsync<Exception>(() =>
                CreateService(failingContext, batchSize: 1).PurgeExpiredDataAsync());
        }

        await using (var verification = database.CreateContext())
        {
            Assert.Null(await verification.Orders.FindAsync(firstOrderId));
            Assert.NotNull(await verification.Orders.FindAsync(blockedOrderId));
            await verification.Database.ExecuteSqlRawAsync(
                """
                DROP TRIGGER fail_blocked_payment_delete ON payments;
                DROP FUNCTION fail_blocked_payment_delete();
                """);
        }

        await using (var retryContext = database.CreateContext())
        {
            var result = await CreateService(retryContext, batchSize: 1).PurgeExpiredDataAsync();
            Assert.Equal(1, result.CompletedOrdersDeleted);
            Assert.Equal(1, result.PaymentsDeleted);
        }

        await using var finalVerification = database.CreateContext();
        Assert.Empty(await finalVerification.Orders.ToListAsync());
        Assert.Empty(await finalVerification.Payments.ToListAsync());
    }

    private static DataRetentionService CreateService(ApplicationDbContext context, int batchSize = 100) =>
        new(
            context,
            Options.Create(new DataRetentionOptions
            {
                CompletedOrderHours = 30,
                OperationalLogDays = 90,
                BatchSize = batchSize
            }),
            new FixedTimeProvider(NowUtc));

    private static Order CreateCompletedOrder(DateTime completedUtc) =>
        new()
        {
            CustomerName = $"Retention Customer {Guid.NewGuid():N}",
            CustomerPhone = "555-867-5309",
            CustomerEmail = "retention@example.test",
            CustomerNotificationOptIn = true,
            TrackingToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('='),
            OrderDate = completedUtc.AddHours(-1),
            OrderStatus = OrderStatus.Completed,
            CompletedUtc = completedUtc,
            OrderItems =
            [
                new OrderItem
                {
                    Quantity = 1,
                    UnitPrice = 5,
                    ItemName = "Retention Latte",
                    ItemDescription = "Snapshot",
                    ItemCategoryType = CategoryType.COFFEE,
                    AddOns =
                    [
                        new AddOn
                        {
                            Quantity = 1,
                            UnitPrice = 0.5m,
                            ItemName = "Retention Flavor",
                            ItemDescription = "Snapshot",
                            ItemCategoryType = CategoryType.FLAVORS
                        }
                    ]
                }
            ]
        };

    private static Payment CreatePayment(
        int? orderId,
        DateTime createdUtc,
        string provider = "retention-test") =>
        new()
        {
            Provider = provider,
            Status = PaymentStatuses.Paid,
            Amount = 5.5m,
            Currency = "USD",
            ProviderCheckoutId = $"checkout-{Guid.NewGuid():N}",
            IdempotencyKey = $"payment-{Guid.NewGuid():N}",
            CustomerName = "Retention Customer",
            CustomerPhone = "5558675309",
            PayloadJson = "{}",
            OrderId = orderId,
            CreatedUtc = createdUtc,
            CompletedUtc = createdUtc
        };

    private static NotificationMessage CreateNotification(
        int? orderId,
        string channel,
        string status,
        DateTime createdUtc) =>
        new()
        {
            EventType = "retention.test",
            RecipientRole = "customer",
            Channel = channel,
            TemplateKey = "retention_test",
            OrderId = orderId,
            PayloadJson = "{}",
            Status = status,
            DedupKey = Guid.NewGuid().ToString("N"),
            CreatedUtc = createdUtc,
            UpdatedUtc = createdUtc
        };

    private static AuditEvent CreateAuditEvent(DateTime occurredUtc) =>
        new()
        {
            OccurredUtc = occurredUtc,
            ActorDisplayName = "Retention Staff",
            Action = "retention.test",
            EntityType = "test",
            EntityId = Guid.NewGuid().ToString("N"),
            DetailsJson = "{}"
        };

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
