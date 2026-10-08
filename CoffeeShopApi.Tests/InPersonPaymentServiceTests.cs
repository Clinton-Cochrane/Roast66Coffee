using System.Collections.Concurrent;
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

public class InPersonPaymentServiceTests
{
    internal static readonly StaffActor Actor = new("staff-42", "Cashier");

    [Theory]
    [InlineData(GatewayPaymentStatus.Pending, PaymentStatuses.Pending)]
    [InlineData(GatewayPaymentStatus.Failed, PaymentStatuses.Failed)]
    [InlineData(GatewayPaymentStatus.Paid, PaymentStatuses.Paid)]
    public async Task Start_UsesSavedTotalAndMapsProviderStateWithoutChangingFulfillment(
        GatewayPaymentStatus status, string expectedStatus)
    {
        await using var context = CreateContext();
        var order = await ManualPaymentServiceTests.AddOrderAsync(context, OrderStatus.Completed);
        var completedUtc = order.CompletedUtc;
        var statusToken = order.StatusConcurrencyToken;
        var gateway = new FakeInPersonGateway { NextStatus = status };
        var service = CreateService(context, gateway);

        var result = await service.StartInPersonPaymentAsync(order.Id, Actor);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(order.Id, result.OrderId);
        Assert.Equal(17.05m, result.Amount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(gateway.ProviderName, result.Provider);
        var call = Assert.Single(gateway.StartCalls);
        Assert.Equal(result.PaymentId, call.Request.PaymentId);
        Assert.Equal(order.Id, call.Request.OrderId);
        Assert.Equal(result.Amount, call.Request.Amount);
        Assert.Equal(result.Currency, call.Request.Currency);
        Assert.Equal(order.Id.ToString(), call.Request.Metadata["order_id"]);
        Assert.Equal(result.PaymentId.ToString("N"), call.Request.Metadata["payment_id"]);
        Assert.Equal(Actor.UserId, call.Request.Metadata["staff_actor_id"]);
        Assert.Equal(result.PaymentId.ToString("N"), call.Key);
        var savedPayment = Assert.Single(await context.Payments.AsNoTracking().ToListAsync());
        Assert.True(savedPayment.IsInPerson);
        Assert.Equal("card", savedPayment.Method);
        Assert.Empty(savedPayment.ProviderCheckoutId);
        Assert.Equal("provider-transaction", savedPayment.ProviderPaymentId);
        Assert.Null(savedPayment.ConfirmedByStaffUtc);
        Assert.DoesNotContain(order.TrackingToken, savedPayment.PayloadJson);
        var savedOrder = await context.Orders.AsNoTracking().SingleAsync();
        Assert.Equal(OrderStatus.Completed, savedOrder.OrderStatus);
        Assert.Equal(completedUtc, savedOrder.CompletedUtc);
        Assert.Equal(statusToken, savedOrder.StatusConcurrencyToken);
        Assert.Equal(status == GatewayPaymentStatus.Paid, savedOrder.PaidUtc.HasValue);
        Assert.Equal(savedOrder.PaidUtc, result.PaidUtc);
    }

    [Fact]
    public async Task RepeatedPendingStart_ReplaysTheReservationWithoutAnotherGatewayCall()
    {
        await using var context = CreateContext();
        var order = await ManualPaymentServiceTests.AddOrderAsync(context);
        var gateway = new FakeInPersonGateway();
        var service = CreateService(context, gateway);
        var first = await service.StartInPersonPaymentAsync(order.Id, Actor);
        var repeat = await service.StartInPersonPaymentAsync(order.Id, Actor);
        Assert.Equal(first, repeat);
        Assert.Single(gateway.StartCalls);
        Assert.Single(await context.Payments.ToListAsync());
    }

    [Fact]
    public async Task FailedAttempt_AllowsANewAttemptWithANewServerIdempotencyKey()
    {
        await using var context = CreateContext();
        var order = await ManualPaymentServiceTests.AddOrderAsync(context);
        var gateway = new FakeInPersonGateway { NextStatus = GatewayPaymentStatus.Failed };
        var service = CreateService(context, gateway);
        var first = await service.StartInPersonPaymentAsync(order.Id, Actor);
        gateway.NextStatus = GatewayPaymentStatus.Pending;
        var second = await service.StartInPersonPaymentAsync(order.Id, Actor);
        Assert.NotEqual(first.PaymentId, second.PaymentId);
        Assert.Equal(PaymentStatuses.Pending, second.Status);
        Assert.Equal(2, gateway.StartCalls.Count);
        Assert.NotEqual(gateway.StartCalls.First().Key, gateway.StartCalls.Last().Key);
    }

    [Fact]
    public async Task UncertainStart_KeepsReservationAndCannotLaunchAnotherCharge()
    {
        await using var context = CreateContext();
        var order = await ManualPaymentServiceTests.AddOrderAsync(context);
        var gateway = new FakeInPersonGateway { ThrowOnStart = true };
        var service = CreateService(context, gateway);
        await Assert.ThrowsAsync<PaymentProviderUnavailableException>(() => service.StartInPersonPaymentAsync(order.Id, Actor));
        var replay = await service.StartInPersonPaymentAsync(order.Id, Actor);
        Assert.Equal(PaymentStatuses.Pending, replay.Status);
        Assert.Single(gateway.StartCalls);
        Assert.Null((await context.Orders.SingleAsync()).PaidUtc);
    }

    [Fact]
    public async Task AlreadyPaidAndUnconfiguredOrders_CannotStartCard()
    {
        await using var context = CreateContext();
        var order = await ManualPaymentServiceTests.AddOrderAsync(context);
        var gateway = new FakeInPersonGateway();
        await Assert.ThrowsAsync<PaymentProviderUnavailableException>(() => CreateService(context).StartInPersonPaymentAsync(order.Id, Actor));
        Assert.Empty(await context.Payments.ToListAsync());
        order.PaidUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InPersonPaymentConflictException>(() => CreateService(context, gateway).StartInPersonPaymentAsync(order.Id, Actor));
        Assert.Empty(gateway.StartCalls);
        Assert.Empty(await context.Payments.ToListAsync());
    }

    [Fact]
    public async Task RegisteredButUnconfiguredAdapter_CannotCreateAReservation()
    {
        await using var context = CreateContext();
        var order = await ManualPaymentServiceTests.AddOrderAsync(context);
        var gateway = new FakeInPersonGateway { Configured = false };
        await Assert.ThrowsAsync<PaymentProviderUnavailableException>(() =>
            CreateService(context, gateway).StartInPersonPaymentAsync(order.Id, Actor));
        Assert.Empty(gateway.StartCalls);
        Assert.Empty(await context.Payments.ToListAsync());
    }

    [Fact]
    public async Task WebhookPaidDuringStart_IsNotOverwrittenByTheLateStartResponse()
    {
        await using var context = CreateContext();
        var order = await ManualPaymentServiceTests.AddOrderAsync(context);
        var gateway = new FakeInPersonGateway { NextStatus = GatewayPaymentStatus.Failed };
        var service = CreateService(context, gateway);
        gateway.BeforeResult = async () =>
        {
            var paymentId = gateway.StartCalls.Single().Request.PaymentId;
            gateway.WebhookEvent = new(paymentId, null, "provider-transaction", GatewayPaymentStatus.Paid, "card");
            await service.HandleWebhookAsync(gateway.ProviderName, "verified", new Dictionary<string, string>());
        };
        var result = await service.StartInPersonPaymentAsync(order.Id, Actor);
        Assert.Equal(PaymentStatuses.Paid, result.Status);
        Assert.NotNull(result.PaidUtc);
        Assert.Single(gateway.StartCalls);
    }

    [Theory]
    [InlineData(GatewayPaymentStatus.Pending, PaymentStatuses.Pending)]
    [InlineData(GatewayPaymentStatus.Failed, PaymentStatuses.Failed)]
    [InlineData(GatewayPaymentStatus.Paid, PaymentStatuses.Paid)]
    public async Task VerifiedWebhook_UpdatesDatabaseStatusAndPollingNeverCallsTheGateway(
        GatewayPaymentStatus status, string expectedStatus)
    {
        await using var context = CreateContext();
        var order = await ManualPaymentServiceTests.AddOrderAsync(context);
        var gateway = new FakeInPersonGateway();
        var service = CreateService(context, gateway);
        var start = await service.StartInPersonPaymentAsync(order.Id, Actor);
        gateway.WebhookEvent = new(start.PaymentId, null, "provider-transaction", status, "card");
        await service.HandleWebhookAsync(gateway.ProviderName, "verified-by-adapter", new Dictionary<string, string>());
        var observation = await service.GetInPersonPaymentAsync(start.PaymentId);
        Assert.NotNull(observation);
        Assert.Equal(expectedStatus, observation.Status);
        Assert.Single(gateway.StartCalls);
        Assert.Equal(1, gateway.WebhookCalls);
        Assert.Equal(OrderStatus.Received, (await context.Orders.SingleAsync()).OrderStatus);
        if (status == GatewayPaymentStatus.Paid)
        {
            gateway.WebhookEvent = gateway.WebhookEvent with { Status = GatewayPaymentStatus.Failed };
            await service.HandleWebhookAsync(gateway.ProviderName, "verified", new Dictionary<string, string>());
            Assert.Equal(PaymentStatuses.Paid, (await service.GetInPersonPaymentAsync(start.PaymentId))!.Status);
            Assert.NotNull(observation.PaidUtc);
        }
    }

    [Fact]
    public async Task PendingCard_BlocksManualSettlementAndOtherPaymentKindsAreNotExposed()
    {
        await using var context = CreateContext();
        var order = await ManualPaymentServiceTests.AddOrderAsync(context);
        var service = CreateService(context, new FakeInPersonGateway());
        await service.StartInPersonPaymentAsync(order.Id, Actor);
        await Assert.ThrowsAsync<ManualPaymentConflictException>(() => service.RecordManualPaymentAsync(order.Id, "cash", Actor));
        var otherOrder = await ManualPaymentServiceTests.AddOrderAsync(context);
        var manual = await service.RecordManualPaymentAsync(otherOrder.Id, "cash", Actor);
        Assert.Null(await service.GetInPersonPaymentAsync(manual.PaymentId));
        Assert.Null(await service.GetInPersonPaymentAsync(Guid.NewGuid()));
    }

    internal static PaymentService CreateService(ApplicationDbContext context, params IInPersonPaymentGateway[] gateways)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Payments:InPersonProvider"] = "fake-terminal"
        }).Build();
        return new PaymentService(context, configuration, new OrderService(context, configuration), [],
            NullLogger<PaymentService>.Instance, inPersonGateways: gateways);
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"in-person-{Guid.NewGuid():N}").Options);
}

// Only tests register this adapter. Production DI has no fake or fallback provider.
internal sealed class FakeInPersonGateway : IInPersonPaymentGateway
{
    public string ProviderName => "fake-terminal";
    public bool Configured { get; set; } = true;
    public bool IsConfigured() => Configured;
    public GatewayPaymentStatus NextStatus { get; set; } = GatewayPaymentStatus.Pending;
    public bool ThrowOnStart { get; set; }
    public bool RejectWebhook { get; set; }
    public GatewayPaymentEvent? WebhookEvent { get; set; }
    public int WebhookCalls { get; private set; }
    public ConcurrentQueue<(InPersonGatewayRequest Request, string Key)> StartCalls { get; } = new();
    public Func<Task>? BeforeResult { get; set; }

    public async Task<InPersonGatewayResult> StartPaymentAsync(InPersonGatewayRequest request, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        StartCalls.Enqueue((request, idempotencyKey));
        if (BeforeResult != null) await BeforeResult();
        if (ThrowOnStart) throw new HttpRequestException("Uncertain provider response");
        return new(NextStatus, "provider-transaction");
    }

    public Task<GatewayPaymentEvent?> ParseWebhookAsync(string body, IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        WebhookCalls++;
        if (RejectWebhook) throw new PaymentWebhookException("Invalid provider signature");
        return Task.FromResult(WebhookEvent);
    }
}
