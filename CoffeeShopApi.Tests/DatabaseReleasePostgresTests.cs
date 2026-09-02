using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class DatabaseReleasePostgresTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task MigrationsProduceEveryEfMappedTableAndColumn()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_schema_contract");
        if (database == null)
        {
            return;
        }

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();

        var knownMigrations = context.Database.GetMigrations().ToArray();
        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(knownMigrations, appliedMigrations);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        var mappedColumns = GetMappedColumns(context.Model);
        Assert.NotEmpty(mappedColumns);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var identifierQuoter = new NpgsqlCommandBuilder();

        foreach (var table in mappedColumns.OrderBy(entry => entry.Key.Schema)
                     .ThenBy(entry => entry.Key.Table))
        {
            await using var command = connection.CreateCommand();
            var columns = string.Join(
                ", ",
                table.Value.OrderBy(column => column)
                    .Select(identifierQuoter.QuoteIdentifier));
            command.CommandText =
                $"SELECT {columns} FROM " +
                $"{identifierQuoter.QuoteIdentifier(table.Key.Schema)}." +
                $"{identifierQuoter.QuoteIdentifier(table.Key.Table)} LIMIT 0";
            await command.ExecuteNonQueryAsync();
        }
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task EfBackedReadAndWritePaths_ExecuteAgainstPostgres()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_data_contract");
        if (database == null)
        {
            return;
        }

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();

        var menuService = new MenuService(context);
        var drink = await menuService.CreateMenuItemAsync(new MenuItem
        {
            Name = "PostgreSQL Contract Latte",
            Description = "Exercises every mapped menu column",
            Price = 5.25m,
            CategoryType = CategoryType.COFFEE
        });
        var addOn = await menuService.CreateMenuItemAsync(new MenuItem
        {
            Name = "PostgreSQL Contract Flavor",
            Description = "Exercises add-on snapshots",
            Price = 0.75m,
            CategoryType = CategoryType.FLAVORS
        });

        Assert.Contains(await menuService.GetMenuItemsAsync(), item => item.Id == drink.Id);
        Assert.Contains(await menuService.GetAllMenuItemsAsync(), item => item.Id == addOn.Id);
        Assert.NotNull(await menuService.GetMenuItemByIdAsync(drink.Id));
        Assert.Equal(
            MenuItemUpdateResult.Updated,
            await menuService.SetPromotionAsync(drink.Id, "10%"));
        Assert.True(await menuService.ArchiveMenuItemAsync(drink.Id));
        Assert.True(await menuService.RestoreMenuItemAsync(drink.Id));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Order:DuplicateDetectionWindowMinutes"] = "2"
            })
            .Build();
        var orderService = new OrderService(context, configuration);
        var order = await orderService.CreateOrderAsync(new Order
        {
            CustomerName = "Database Contract Customer",
            CustomerPhone = "555-0100",
            OrderItems =
            [
                new OrderItem
                {
                    MenuItemId = drink.Id,
                    Quantity = 1,
                    AddOns =
                    [
                        new AddOn
                        {
                            MenuItemId = addOn.Id,
                            Quantity = 2
                        }
                    ]
                }
            ]
        });

        Assert.NotNull(await orderService.GetOrderByIdAsync(order.Id));
        Assert.NotNull(await orderService.GetOrderByTrackingTokenAsync(order.TrackingToken));
        Assert.Contains(await orderService.GetOrdersAsync(), item => item.Id == order.Id);
        Assert.NotNull(await orderService.FindDuplicateOrderAsync(order));
        Assert.NotNull(await orderService.GetOrderForCustomerAsync(
            order.Id,
            order.CustomerPhone,
            null));
        Assert.Equal(1, await orderService.GetCountSinceAsync(DateTime.UtcNow.AddMinutes(-5)));

        var settingsService = new NotificationSettingsService(context);
        await settingsService.SaveNotificationSettingsAsync(new NotificationSettings
        {
            AdminEmail = "admin@example.test",
            SmsFromAddress = "+15550101"
        });
        Assert.Equal(
            "admin@example.test",
            (await settingsService.GetNotificationSettingsAsync())?.AdminEmail);

        context.NotificationMessages.Add(new NotificationMessage
        {
            EventType = "database-contract",
            RecipientRole = "admin",
            Channel = "email",
            TemplateKey = "database-contract",
            DedupKey = $"database-contract-{Guid.NewGuid():N}",
            CreatedUtc = DateTime.UtcNow.AddDays(-91),
            UpdatedUtc = DateTime.UtcNow.AddDays(-91)
        });
        context.StaffPushSubscriptions.Add(new StaffPushSubscription
        {
            Endpoint = "https://push.example.test/database-contract",
            P256Dh = "test-p256dh",
            Auth = "test-auth"
        });
        context.Payments.Add(new Payment
        {
            Provider = "contract",
            Status = PaymentStatuses.Pending,
            Amount = 6.75m,
            Currency = "USD",
            ProviderCheckoutId = $"checkout-{Guid.NewGuid():N}",
            IdempotencyKey = $"idempotency-{Guid.NewGuid():N}",
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone ?? string.Empty,
            PayloadJson = "{}",
            OrderId = order.Id
        });
        await context.SaveChangesAsync();

        var retentionService = new DataRetentionService(
            context,
            Options.Create(new DataRetentionOptions()),
            TimeProvider.System);
        Assert.Equal(
            1,
            (await retentionService.PurgeExpiredDataAsync()).NotificationLogsDeleted);
        Assert.Equal(1, await context.StaffPushSubscriptions.CountAsync());
        Assert.Equal(1, await context.Payments.CountAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task RowLevelSecurity_HidesOrdersFromSupabaseClientRoles()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_rls_contract");
        if (database == null)
        {
            return;
        }

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        context.Orders.Add(new Order
        {
            CustomerName = "RLS Contract Customer",
            TrackingToken = $"rls-{Guid.NewGuid():N}",
            OrderItems = []
        });
        context.Users.Add(new StaffUser
        {
            Id = "rls-staff-user",
            UserName = "rls-staff-user",
            NormalizedUserName = "RLS-STAFF-USER",
            DisplayName = "RLS Staff User",
            IsActive = true
        });
        context.AuditEvents.Add(new AuditEvent
        {
            OccurredUtc = DateTime.UtcNow,
            ActorUserId = "rls-staff-user",
            ActorDisplayName = "RLS Staff User",
            Action = "rls.contract",
            EntityType = "contract",
            EntityId = "1",
            DetailsJson = "{}"
        });
        await context.SaveChangesAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "GRANT USAGE ON SCHEMA public TO anon, authenticated; " +
            "GRANT SELECT ON public.orders, public.staffusers, public.auditevents TO anon, authenticated;";
        await command.ExecuteNonQueryAsync();

        // The grants isolate RLS from ordinary table-permission denial: each role
        // can SELECT the table but the explicit false policy must hide every row.
        foreach (var role in new[] { "anon", "authenticated" })
        {
            command.CommandText = $"SET ROLE {role}";
            await command.ExecuteNonQueryAsync();
            command.CommandText = "SELECT count(*) FROM public.orders";
            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
            command.CommandText = "SELECT count(*) FROM public.staffusers";
            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
            command.CommandText = "SELECT count(*) FROM public.auditevents";
            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
            command.CommandText = "RESET ROLE";
            await command.ExecuteNonQueryAsync();
        }
    }

    private static Dictionary<(string Schema, string Table), HashSet<string>> GetMappedColumns(
        IModel model)
    {
        var result = new Dictionary<(string Schema, string Table), HashSet<string>>();

        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName == null)
            {
                continue;
            }

            var schema = entityType.GetSchema() ?? "public";
            var table = StoreObjectIdentifier.Table(tableName, schema);
            var key = (schema, tableName);
            if (!result.TryGetValue(key, out var columns))
            {
                columns = [];
                result[key] = columns;
            }

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(table);
                if (columnName != null)
                {
                    columns.Add(columnName);
                }
            }
        }

        return result;
    }
}
