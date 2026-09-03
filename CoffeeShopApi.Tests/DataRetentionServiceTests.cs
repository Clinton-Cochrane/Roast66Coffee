using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoffeeShopApi.Tests;

public class DataRetentionServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 9, 2, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Options_DefaultCompletedOrderRetentionIsFortyEightHours()
    {
        Assert.Equal(48, new DataRetentionOptions().CompletedOrderHours);
    }

    [Fact]
    public async Task PurgeExpiredData_DeletesCompletedOrderAndRelatedRecordsAtFortyEightHours()
    {
        await using var context = CreateContext();
        var expired = CreateOrder(OrderStatus.Completed, NowUtc.AddHours(-48));
        var active = CreateOrder(OrderStatus.ReadyForPickup, null, NowUtc.AddDays(-10));
        context.Orders.AddRange(expired, active);
        await context.SaveChangesAsync();

        context.Payments.Add(CreatePayment(expired.Id, NowUtc.AddHours(-29)));
        context.NotificationMessages.Add(CreateNotification(
            expired.Id,
            "sms",
            "failed",
            NowUtc.AddHours(-1)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await CreateService(context).PurgeExpiredDataAsync();

        Assert.Equal(1, result.CompletedOrdersDeleted);
        Assert.Equal(1, result.PaymentsDeleted);
        Assert.Equal(1, result.NotificationLogsDeleted);
        Assert.Null(await context.Orders.FindAsync(expired.Id));
        Assert.NotNull(await context.Orders.FindAsync(active.Id));
        Assert.Empty(await context.Payments.ToListAsync());
        Assert.Empty(await context.NotificationMessages.ToListAsync());
    }

    [Fact]
    public async Task PurgeExpiredData_KeepsIncompleteOrdersAndTheirPaymentsIndefinitely()
    {
        await using var context = CreateContext();
        var active = CreateOrder(OrderStatus.Received, null, NowUtc.AddYears(-1));
        context.Orders.Add(active);
        await context.SaveChangesAsync();
        context.Payments.Add(CreatePayment(active.Id, NowUtc.AddYears(-1)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await CreateService(context).PurgeExpiredDataAsync();

        Assert.Equal(0, result.CompletedOrdersDeleted);
        Assert.Equal(0, result.PaymentsDeleted);
        Assert.NotNull(await context.Orders.FindAsync(active.Id));
        Assert.Single(await context.Payments.ToListAsync());
    }

    [Fact]
    public async Task PurgeExpiredData_DeletesEveryExpiredLogChannelStatusAndAuditEventAtNinetyDays()
    {
        await using var context = CreateContext();
        context.NotificationMessages.AddRange(
            CreateNotification(null, "email", "sent", NowUtc.AddDays(-90)),
            CreateNotification(null, "sms", "skipped", NowUtc.AddDays(-91)),
            CreateNotification(null, "push", "failed", NowUtc.AddDays(-89)));
        context.AuditEvents.AddRange(
            CreateAuditEvent(NowUtc.AddDays(-90)),
            CreateAuditEvent(NowUtc.AddDays(-89)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await CreateService(context).PurgeExpiredDataAsync();

        Assert.Equal(2, result.NotificationLogsDeleted);
        Assert.Equal(1, result.AuditEventsDeleted);
        var remainingNotification = Assert.Single(await context.NotificationMessages.ToListAsync());
        Assert.Equal("push", remainingNotification.Channel);
        Assert.Single(await context.AuditEvents.ToListAsync());
    }

    [Fact]
    public async Task PurgeExpiredData_DeletesExpiredOrphanPaymentsInBatchesAndCanRunAgain()
    {
        await using var context = CreateContext();
        context.Payments.AddRange(
            CreatePayment(null, NowUtc.AddHours(-49)),
            CreatePayment(null, NowUtc.AddHours(-48)),
            CreatePayment(null, NowUtc.AddHours(-47)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, batchSize: 1);
        var first = await service.PurgeExpiredDataAsync();
        var second = await service.PurgeExpiredDataAsync();

        Assert.Equal(2, first.PaymentsDeleted);
        Assert.Equal(0, second.PaymentsDeleted);
        Assert.Single(await context.Payments.ToListAsync());
    }

    private static DataRetentionService CreateService(ApplicationDbContext context, int batchSize = 100) =>
        new(
            context,
            Options.Create(new DataRetentionOptions
            {
                CompletedOrderHours = 48,
                OperationalLogDays = 90,
                BatchSize = batchSize
            }),
            new FixedTimeProvider(NowUtc));

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"DataRetentionServiceTests-{Guid.NewGuid():N}")
            .Options);

    private static Order CreateOrder(
        OrderStatus status,
        DateTime? completedUtc,
        DateTime? orderDate = null) =>
        new()
        {
            CustomerName = $"Retention Customer {Guid.NewGuid():N}",
            CustomerPhone = "555-867-5309",
            CustomerEmail = "retention@example.test",
            CustomerNotificationOptIn = true,
            TrackingToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .PadRight(43, 'x')[..43],
            OrderDate = orderDate ?? NowUtc.AddHours(-31),
            OrderStatus = status,
            CompletedUtc = completedUtc,
            OrderItems = []
        };

    private static Payment CreatePayment(int? orderId, DateTime createdUtc) =>
        new()
        {
            Provider = "retention-test",
            Status = PaymentStatuses.Paid,
            Amount = 5,
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
