using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Security;
using CoffeeShopApi.Services;
using CoffeeShopApi.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeShopApi.Tests;

public class ManualPaymentServiceTests
{
    private static readonly StaffActor Actor = new("staff-42", "Cashier");

    [Theory]
    [InlineData("cash", OrderStatus.Received)]
    [InlineData("cash", OrderStatus.Preparing)]
    [InlineData("cash", OrderStatus.ReadyForPickup)]
    [InlineData("cash", OrderStatus.Completed)]
    [InlineData("other", OrderStatus.Received)]
    [InlineData("other", OrderStatus.Preparing)]
    [InlineData("other", OrderStatus.ReadyForPickup)]
    [InlineData("other", OrderStatus.Completed)]
    public async Task Recording_UsesSnapshotsAndStaffConfirmationWithoutChangingFulfillment(
        string method, OrderStatus status)
    {
        await using var context = CreateContext();
        var order = await AddOrderAsync(context, status);
        var completionTime = order.CompletedUtc;
        var statusToken = order.StatusConcurrencyToken;
        var service = CreateService(context);
        Assert.False(service.IsConfigured());

        var result = await service.RecordManualPaymentAsync(order.Id, method, Actor);

        Assert.False(result.WasReplay);
        Assert.Equal(order.Id, result.OrderId);
        Assert.Equal(method, result.Method);
        Assert.Equal(17.05m, result.Amount);
        Assert.Equal("USD", result.Currency);
        var payment = Assert.Single(await context.Payments.AsNoTracking().ToListAsync());
        Assert.Equal(result.PaymentId, payment.Id);
        Assert.Equal("manual", payment.Provider);
        Assert.Equal(method, payment.Method);
        Assert.Equal(PaymentStatuses.Paid, payment.Status);
        Assert.Equal(17.05m, payment.Amount);
        Assert.Equal(order.Id, payment.OrderId);
        Assert.Equal(result.PaidUtc, payment.CompletedUtc);
        Assert.Equal(result.PaidUtc, payment.ConfirmedByStaffUtc);
        Assert.Null(payment.ProviderPaymentId);
        Assert.DoesNotContain(order.TrackingToken, payment.PayloadJson);
        var savedOrder = await context.Orders.AsNoTracking().SingleAsync();
        Assert.Equal(result.PaidUtc, savedOrder.PaidUtc);
        Assert.Equal(method, savedOrder.PaymentProvider);
        Assert.Equal(payment.Id.ToString("N"), savedOrder.PaymentReference);
        Assert.Equal(status, savedOrder.OrderStatus);
        Assert.Equal(completionTime, savedOrder.CompletedUtc);
        Assert.Equal(statusToken, savedOrder.StatusConcurrencyToken);
        var audit = Assert.Single(await context.AuditEvents.AsNoTracking().ToListAsync());
        Assert.Equal("payment.manual.recorded", audit.Action);
        Assert.Equal("staff-42", audit.ActorUserId);
        Assert.Equal("Cashier", audit.ActorDisplayName);
        Assert.Equal("order", audit.EntityType);
        Assert.Equal(order.Id.ToString(), audit.EntityId);
        using var details = JsonDocument.Parse(audit.DetailsJson);
        Assert.Equal(method, details.RootElement.GetProperty("method").GetString());
        Assert.Equal(17.05m, details.RootElement.GetProperty("amount").GetDecimal());
        Assert.DoesNotContain(order.TrackingToken, audit.DetailsJson);
    }

    [Theory]
    [InlineData("cash")]
    [InlineData("other")]
    public async Task RepeatingTheSameMethod_ReplaysOnePaymentAndOneAudit(string method)
    {
        await using var context = CreateContext();
        var order = await AddOrderAsync(context);
        var service = CreateService(context);
        var first = await service.RecordManualPaymentAsync(order.Id, method, Actor);
        var replay = await service.RecordManualPaymentAsync(order.Id, method, new StaffActor("staff-99", "Second cashier"));

        Assert.True(replay.WasReplay);
        Assert.Equal(first.PaymentId, replay.PaymentId);
        Assert.Equal(first.PaidUtc, replay.PaidUtc);
        Assert.Single(await context.Payments.ToListAsync());
        var audit = Assert.Single(await context.AuditEvents.ToListAsync());
        Assert.Equal(Actor.UserId, audit.ActorUserId);
        Assert.Equal(first.PaidUtc, (await context.Orders.SingleAsync()).PaidUtc);
    }

    [Fact]
    public async Task AnotherManualMethod_CannotReplaceTheRecordedPayment()
    {
        await using var context = CreateContext();
        var order = await AddOrderAsync(context);
        var service = CreateService(context);
        var first = await service.RecordManualPaymentAsync(order.Id, "cash", Actor);

        await Assert.ThrowsAsync<ManualPaymentConflictException>(() =>
            service.RecordManualPaymentAsync(order.Id, "other", Actor));

        var payment = Assert.Single(await context.Payments.ToListAsync());
        Assert.Equal(first.PaymentId, payment.Id);
        Assert.Equal("cash", payment.Method);
        Assert.Equal(first.PaidUtc, (await context.Orders.SingleAsync()).PaidUtc);
        Assert.Single(await context.AuditEvents.ToListAsync());
    }

    [Theory]
    [InlineData("cash")]
    [InlineData("other")]
    public async Task AlreadyPaidOrder_IsNotRecordedOrOverwritten(string method)
    {
        await using var context = CreateContext();
        var order = await AddOrderAsync(context);
        var paidUtc = DateTime.UtcNow.AddMinutes(-10);
        order.PaidUtc = paidUtc;
        order.PaymentProvider = "stripe";
        order.PaymentReference = "provider-payment-42";
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ManualPaymentConflictException>(() =>
            CreateService(context).RecordManualPaymentAsync(order.Id, method, Actor));

        Assert.Empty(await context.Payments.ToListAsync());
        Assert.Empty(await context.AuditEvents.ToListAsync());
        var savedOrder = await context.Orders.AsNoTracking().SingleAsync();
        Assert.Equal(paidUtc, savedOrder.PaidUtc);
        Assert.Equal("stripe", savedOrder.PaymentProvider);
        Assert.Equal("provider-payment-42", savedOrder.PaymentReference);
    }

    [Theory]
    [InlineData("card")]
    [InlineData("stripe")]
    [InlineData("zelle")]
    [InlineData("Cash")]
    [InlineData("")]
    public async Task UnsupportedMethod_IsRejectedWithoutWrites(string method)
    {
        await using var context = CreateContext();
        var order = await AddOrderAsync(context);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(context).RecordManualPaymentAsync(order.Id, method, Actor));
        Assert.Empty(await context.Payments.ToListAsync());
        Assert.Null((await context.Orders.SingleAsync()).PaidUtc);
    }

    [Fact]
    public async Task MissingOrder_IsNotCreatedByPaymentRecording()
    {
        await using var context = CreateContext();
        await Assert.ThrowsAsync<ManualPaymentOrderNotFoundException>(() =>
            CreateService(context).RecordManualPaymentAsync(66, "cash", Actor));
        Assert.Empty(await context.Orders.ToListAsync());
        Assert.Empty(await context.Payments.ToListAsync());
        Assert.Empty(await context.AuditEvents.ToListAsync());
    }

    [Fact]
    public async Task SeparateOrders_GetSeparateManualReceipts()
    {
        await using var context = CreateContext();
        var firstOrder = await AddOrderAsync(context);
        var secondOrder = await AddOrderAsync(context);
        var service = CreateService(context);
        var first = await service.RecordManualPaymentAsync(firstOrder.Id, "cash", Actor);
        var second = await service.RecordManualPaymentAsync(secondOrder.Id, "other", Actor);
        Assert.NotEqual(first.PaymentId, second.PaymentId);
        Assert.Equal(2, await context.Payments.CountAsync());
        Assert.Equal(2, await context.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task OrderWithoutBillableItems_IsNotMarkedPaid()
    {
        await using var context = CreateContext();
        var order = await AddOrderAsync(context);
        context.OrderItems.RemoveRange(order.OrderItems);
        order.OrderItems.Clear();
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context).RecordManualPaymentAsync(order.Id, "cash", Actor));
        Assert.Null((await context.Orders.SingleAsync()).PaidUtc);
        Assert.Empty(await context.Payments.ToListAsync());
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"manual-payments-{Guid.NewGuid():N}").Options);

    internal static PaymentService CreateService(ApplicationDbContext context, params IPaymentGateway[] gateways)
    {
        var configuration = new ConfigurationBuilder().Build();
        return new PaymentService(context, configuration, new OrderService(context, configuration), gateways,
            NullLogger<PaymentService>.Instance);
    }

    internal static async Task<Order> AddOrderAsync(ApplicationDbContext context, OrderStatus status = OrderStatus.Received)
    {
        var order = new Order
        {
            CustomerName = "Stored customer",
            TrackingToken = Guid.NewGuid().ToString("N").PadRight(43, 'x'),
            OrderStatus = status,
            CompletedUtc = status == OrderStatus.Completed ? DateTime.UtcNow.AddMinutes(-5) : null,
            OrderItems = [
                new OrderItem {
                    ItemName = "Saved latte", ItemDescription = "Snapshot", UnitPrice = 4.25m, Quantity = 2,
                    AddOns = [new AddOn { ItemName = "Vanilla", ItemDescription = "Snapshot", UnitPrice = 0.75m, Quantity = 3 }]
                },
                new OrderItem { ItemName = "Cold brew", ItemDescription = "Snapshot", UnitPrice = 2.10m, Quantity = 3, AddOns = [] }
            ]
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }
}
