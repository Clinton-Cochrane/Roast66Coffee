using System.Data.Common;
using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Security;
using CoffeeShopApi.Services;
using CoffeeShopApi.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class ManualPaymentPostgresTests
{
    private static readonly StaffActor Actor = new("staff-42", "Cashier");

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task ConcurrentSameMethod_ReplaysOnePaymentAndOneAudit()
    {
        await using var database = await CreateDatabaseAsync();
        if (database == null) return;
        var orderId = await SeedOrderAsync(database);
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        // A tracked stale order must not bypass the paid-state check after locking.
        await firstContext.Orders.SingleAsync();
        await secondContext.Orders.SingleAsync();
        var results = await Task.WhenAll(
            ManualPaymentServiceTests.CreateService(firstContext).RecordManualPaymentAsync(orderId, "cash", Actor),
            ManualPaymentServiceTests.CreateService(secondContext).RecordManualPaymentAsync(orderId, "cash", Actor));

        Assert.Single(results, result => !result.WasReplay);
        Assert.Single(results, result => result.WasReplay);
        Assert.Equal(results[0].PaymentId, results[1].PaymentId);
        Assert.Equal(results[0].PaidUtc, results[1].PaidUtc);
        await using var verification = database.CreateContext();
        Assert.Single(await verification.Payments.ToListAsync());
        Assert.Single(await verification.AuditEvents.ToListAsync());
        Assert.Equal("cash", (await verification.Orders.SingleAsync()).PaymentProvider);
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task ConcurrentDifferentMethods_HaveOneWinnerWithoutOverwritingIt()
    {
        await using var database = await CreateDatabaseAsync();
        if (database == null) return;
        var orderId = await SeedOrderAsync(database);
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        await firstContext.Orders.SingleAsync();
        await secondContext.Orders.SingleAsync();
        var results = await Task.WhenAll(
            AttemptAsync(firstContext, orderId, "cash"),
            AttemptAsync(secondContext, orderId, "other"));

        var winner = Assert.Single(results, result => result != null)!;
        Assert.Single(results, result => result == null);
        await using var verification = database.CreateContext();
        var payment = Assert.Single(await verification.Payments.ToListAsync());
        Assert.Equal(winner.PaymentId, payment.Id);
        Assert.Equal(winner.Method, payment.Method);
        var order = await verification.Orders.SingleAsync();
        Assert.Equal(winner.Method, order.PaymentProvider);
        Assert.Equal(winner.PaidUtc, order.PaidUtc);
        Assert.Equal(payment.Id.ToString("N"), order.PaymentReference);
        Assert.Single(await verification.AuditEvents.ToListAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task PaymentAndFulfillment_CanCommitWithoutOverwritingEachOther()
    {
        await using var database = await CreateDatabaseAsync();
        if (database == null) return;
        var orderId = await SeedOrderAsync(database);
        await using var paymentContext = database.CreateContext();
        await using var statusContext = database.CreateContext();
        await paymentContext.Orders.SingleAsync();
        await statusContext.Orders.SingleAsync();
        var paymentTask = ManualPaymentServiceTests.CreateService(paymentContext)
            .RecordManualPaymentAsync(orderId, "cash", Actor);
        var statusTask = new OrderService(statusContext, new ConfigurationBuilder().Build())
            .AdvanceStatusAsync(orderId, OrderStatus.Received);
        await Task.WhenAll(paymentTask, statusTask);

        Assert.Equal(OrderStatusAdvanceOutcome.Advanced, (await statusTask).Outcome);
        await using var verification = database.CreateContext();
        var order = await verification.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Preparing, order.OrderStatus);
        Assert.Equal("cash", order.PaymentProvider);
        Assert.Equal((await paymentTask).PaidUtc, order.PaidUtc);
        Assert.Single(await verification.Payments.ToListAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task ConcurrentManualAndProviderSettlement_PreserveTheFirstOrderPayment()
    {
        await using var database = await CreateDatabaseAsync();
        if (database == null) return;
        var orderId = await SeedOrderAsync(database);
        var pendingPaymentId = await SeedPendingPaymentAsync(database, orderId);
        await using var manualContext = database.CreateContext();
        await using var providerContext = database.CreateContext();
        await manualContext.Orders.SingleAsync();
        await providerContext.Orders.SingleAsync();
        var manualTask = AttemptAsync(manualContext, orderId, "cash");
        var providerTask = ManualPaymentServiceTests.CreateService(providerContext, new ConfirmedGateway(pendingPaymentId))
            .HandleWebhookAsync("fake", "{}", new Dictionary<string, string>());
        await Task.WhenAll(manualTask, providerTask);

        await using var verification = database.CreateContext();
        var order = await verification.Orders.SingleAsync();
        var providerPayment = await verification.Payments.SingleAsync(payment => payment.Id == pendingPaymentId);
        Assert.Equal(PaymentStatuses.Paid, providerPayment.Status);
        var manual = await manualTask;
        if (manual == null)
        {
            Assert.Equal("fake", order.PaymentProvider);
            Assert.Equal("confirmed-provider-payment", order.PaymentReference);
            Assert.Empty(await verification.Payments.Where(payment => payment.Provider == "manual").ToListAsync());
            Assert.Empty(await verification.AuditEvents.ToListAsync());
        }
        else
        {
            Assert.Equal("cash", order.PaymentProvider);
            Assert.Equal(manual.PaidUtc, order.PaidUtc);
            Assert.Equal(manual.PaymentId.ToString("N"), order.PaymentReference);
            Assert.Single(await verification.Payments.Where(payment => payment.Provider == "manual").ToListAsync());
            Assert.Single(await verification.AuditEvents.ToListAsync());
        }
        Assert.Equal(OrderStatus.Received, order.OrderStatus);
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task StaleProviderConfirmation_PreservesTheExistingManualOrderSettlement()
    {
        await using var database = await CreateDatabaseAsync();
        if (database == null) return;
        var orderId = await SeedOrderAsync(database);
        var pendingPaymentId = await SeedPendingPaymentAsync(database, orderId);
        await using var providerContext = database.CreateContext();
        await providerContext.Orders.SingleAsync();
        await providerContext.Payments.SingleAsync();
        await using var manualContext = database.CreateContext();
        var manual = await ManualPaymentServiceTests.CreateService(manualContext)
            .RecordManualPaymentAsync(orderId, "cash", Actor);
        var gateway = new ConfirmedGateway(pendingPaymentId);
        await ManualPaymentServiceTests.CreateService(providerContext, gateway)
            .HandleWebhookAsync("fake", "{}", new Dictionary<string, string>());

        await using var verification = database.CreateContext();
        var order = await verification.Orders.SingleAsync();
        Assert.Equal(manual.PaidUtc, order.PaidUtc);
        Assert.Equal("cash", order.PaymentProvider);
        Assert.Equal(manual.PaymentId.ToString("N"), order.PaymentReference);
        // The authoritative provider event still records an actual provider payment;
        // cancelling or reconciling a pre-existing online checkout is a separate step.
        Assert.Equal(PaymentStatuses.Paid, (await verification.Payments.SingleAsync(payment => payment.Id == pendingPaymentId)).Status);
        Assert.Single(await verification.Payments.Where(payment => payment.Provider == "manual").ToListAsync());
        Assert.Single(await verification.AuditEvents.ToListAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task FailedCommit_RollsBackOrderPaymentAndAuditAndAllowsRetry()
    {
        await using var database = await CreateDatabaseAsync();
        if (database == null) return;
        var orderId = await SeedOrderAsync(database);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString)
            .AddInterceptors(new RejectCommit()).Options;
        await using (var failing = new ApplicationDbContext(options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ManualPaymentServiceTests.CreateService(failing).RecordManualPaymentAsync(orderId, "cash", Actor));
        }
        await using var verification = database.CreateContext();
        Assert.Null((await verification.Orders.SingleAsync()).PaidUtc);
        Assert.Empty(await verification.Payments.ToListAsync());
        Assert.Empty(await verification.AuditEvents.ToListAsync());
        var retry = await ManualPaymentServiceTests.CreateService(verification)
            .RecordManualPaymentAsync(orderId, "cash", Actor);
        Assert.False(retry.WasReplay);
        Assert.Single(await verification.Payments.ToListAsync());
        Assert.Single(await verification.AuditEvents.ToListAsync());
    }

    private static async Task<ManualPaymentResult?> AttemptAsync(ApplicationDbContext context, int orderId, string method)
    {
        try { return await ManualPaymentServiceTests.CreateService(context).RecordManualPaymentAsync(orderId, method, Actor); }
        catch (ManualPaymentConflictException) { return null; }
    }

    private static async Task<PostgresTestDatabase?> CreateDatabaseAsync()
    {
        var database = await PostgresTestDatabase.CreateAsync("roast66_manual_payment");
        if (database != null)
        {
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync();
        }
        return database;
    }

    private static async Task<int> SeedOrderAsync(PostgresTestDatabase database)
    {
        await using var context = database.CreateContext();
        return (await ManualPaymentServiceTests.AddOrderAsync(context)).Id;
    }

    private static async Task<Guid> SeedPendingPaymentAsync(PostgresTestDatabase database, int orderId)
    {
        await using var context = database.CreateContext();
        var payment = new Payment
        {
            Provider = "fake", Method = "online", Status = PaymentStatuses.Pending,
            OrderId = orderId, Amount = 17.05m, ProviderCheckoutId = "existing-online-checkout",
            IdempotencyKey = "online-checkout-key", CustomerName = "Stored customer", CustomerPhone = "",
            PayloadJson = JsonSerializer.Serialize(new { ExistingOrderId = orderId })
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        return payment.Id;
    }

    private sealed class RejectCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated commit failure after writes.");
    }

    private sealed class ConfirmedGateway(Guid paymentId) : IPaymentGateway
    {
        public string ProviderName => "fake";
        public bool IsConfigured() => true;
        public Task<GatewayCheckoutResult> CreateCheckoutAsync(GatewayCheckoutRequest request, string idempotencyKey,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("No checkout should be launched.");
        public Task<GatewayCheckoutResult> GetCheckoutAsync(string providerCheckoutId,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("No checkout should be fetched.");
        public Task<GatewayPaymentEvent?> ParseWebhookAsync(string body, IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken = default) => Task.FromResult<GatewayPaymentEvent?>(
                new(paymentId, "existing-online-checkout", "confirmed-provider-payment", GatewayPaymentStatus.Paid, "card"));
    }
}
