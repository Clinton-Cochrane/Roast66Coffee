using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Services;
using CoffeeShopApi.Services.Payments;
using CoffeeShopApi.Services.Sms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShopApi.Tests;

public class PaymentServiceTests
{
    [Fact]
    public void GeneralizePaymentProvidersMigration_GeneratesNonDestructiveSql()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=migration-script-only;Username=test;Password=test")
            .Options;
        using var context = new ApplicationDbContext(options);
        var migrator = context.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            "20260823000000_AddPromotionsAndOrderPriceSnapshots",
            "20260827000000_GeneralizePaymentProviders");

        Assert.Contains("ALTER TABLE paymentcheckoutdrafts RENAME TO payments", script);
        Assert.Contains("RENAME COLUMN stripepaymentintentid TO paymentreference", script);
        Assert.DoesNotContain("DROP TABLE paymentcheckoutdrafts", script);
    }

    [Fact]
    public void PaymentConcurrencyMigration_AddsTokenWithoutRewritingPaymentData()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=migration-script-only;Username=test;Password=test")
            .Options;
        using var context = new ApplicationDbContext(options);
        var migrator = context.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            "20260827010000_GeneralizeSmsProvider",
            "20260828000000_AddPaymentConcurrencyToken");

        Assert.Contains("ADD concurrencytoken uuid NOT NULL", script);
        Assert.DoesNotContain("DROP TABLE payments", script);
    }

    [Fact]
    public async Task ExistingOrderCheckoutAndPaidWebhook_AreProviderNeutralAndIdempotent()
    {
        var gateway = new FakePaymentGateway();
        await using var services = BuildServices(gateway);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.MenuItems.Add(new MenuItem
        {
            Id = 42,
            Name = "Test Latte",
            Description = "Provider-neutral test drink",
            Price = 4.50m,
            CategoryType = CategoryType.COFFEE
        });
        var order = new Order
        {
            CustomerName = "Gateway Customer",
            CustomerPhone = "5551234567",
            TrackingToken = "test-tracking-token-12345678901234567890123",
            OrderItems =
            [
                new OrderItem
                {
                    MenuItemId = 42,
                    Quantity = 2,
                    UnitPrice = 4.50m
                }
            ]
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var paymentService = scope.ServiceProvider.GetRequiredService<PaymentService>();
        var checkout = await paymentService.CreateCheckoutAsync(
            new CheckoutSessionRequest
            {
                ExistingOrderId = order.Id,
                CustomerName = "Gateway Customer",
                CustomerPhone = "5551234567"
            },
            "provider-neutral-key");

        Assert.Equal(FakePaymentGateway.Name, checkout.Provider);
        Assert.Equal("https://payments.example/checkout", checkout.CheckoutUrl);
        Assert.NotNull(gateway.LastCheckoutRequest);
        Assert.Equal(9.00m, gateway.LastCheckoutRequest!.LineItems.Sum(
            item => item.UnitPrice * item.Quantity));

        var payment = await context.Payments.SingleAsync();
        Assert.Equal(FakePaymentGateway.Name, payment.Provider);
        Assert.Equal(PaymentStatuses.Pending, payment.Status);
        Assert.Equal(9.00m, payment.Amount);

        gateway.NextWebhookEvent = new GatewayPaymentEvent(
            payment.Id,
            payment.ProviderCheckoutId,
            "fake-payment-123",
            GatewayPaymentStatus.Paid,
            "wallet");

        await paymentService.HandleWebhookAsync(
            FakePaymentGateway.Name,
            "{}",
            new Dictionary<string, string>());
        await paymentService.HandleWebhookAsync(
            FakePaymentGateway.Name,
            "{}",
            new Dictionary<string, string>());

        var savedOrder = await context.Orders.SingleAsync();
        await context.Entry(payment).ReloadAsync();
        Assert.NotNull(savedOrder.PaidUtc);
        Assert.Equal(FakePaymentGateway.Name, savedOrder.PaymentProvider);
        Assert.Equal("fake-payment-123", savedOrder.PaymentReference);
        Assert.Equal(savedOrder.Id, payment.OrderId);
        Assert.Equal(PaymentStatuses.Paid, payment.Status);
        Assert.Equal("wallet", payment.Method);
    }

    [Fact]
    public async Task PaymentConcurrencyToken_RejectsStaleWebhookUpdate()
    {
        var gateway = new FakePaymentGateway();
        await using var services = BuildServices(gateway);

        await using (var setupScope = services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            setupContext.Payments.Add(new Payment
            {
                Provider = FakePaymentGateway.Name,
                Status = PaymentStatuses.Pending,
                Amount = 4.50m,
                ProviderCheckoutId = "concurrent-checkout",
                IdempotencyKey = "concurrent-key",
                CustomerName = "Concurrent Customer",
                CustomerPhone = string.Empty,
                PayloadJson = "{}"
            });
            await setupContext.SaveChangesAsync();
        }

        await using var firstScope = services.CreateAsyncScope();
        await using var secondScope = services.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var firstPayment = await firstContext.Payments.SingleAsync();
        var secondPayment = await secondContext.Payments.SingleAsync();

        firstPayment.Status = PaymentStatuses.Paid;
        firstPayment.ConcurrencyToken = Guid.NewGuid();
        secondPayment.Status = PaymentStatuses.Failed;
        secondPayment.ConcurrencyToken = Guid.NewGuid();

        await firstContext.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondContext.SaveChangesAsync());
    }

    private static ServiceProvider BuildServices(IPaymentGateway gateway)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payments:DefaultProvider"] = FakePaymentGateway.Name,
                ["Payments:FrontendBaseUrl"] = "https://roast66.example"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddHttpClient();
        var databaseName = $"payment-tests-{Guid.NewGuid():N}";
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddScoped<OrderService>();
        services.AddScoped<NotificationSettingsService>();
        services.AddScoped<ISmsSender, DisabledSmsSender>();
        services.AddScoped<OrderEmailNotificationService>();
        services.AddScoped<StaffPushNotificationService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<PaymentService>();
        services.AddSingleton(gateway);
        return services.BuildServiceProvider();
    }

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        public const string Name = "fake";

        public string ProviderName => Name;
        public GatewayCheckoutRequest? LastCheckoutRequest { get; private set; }
        public GatewayPaymentEvent? NextWebhookEvent { get; set; }

        public bool IsConfigured() => true;

        public Task<GatewayCheckoutResult> CreateCheckoutAsync(
            GatewayCheckoutRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            LastCheckoutRequest = request;
            return Task.FromResult(new GatewayCheckoutResult(
                "https://payments.example/checkout",
                "fake-checkout-123"));
        }

        public Task<GatewayCheckoutResult> GetCheckoutAsync(
            string providerCheckoutId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GatewayCheckoutResult(
                "https://payments.example/checkout",
                providerCheckoutId));

        public Task<GatewayPaymentEvent?> ParseWebhookAsync(
            string body,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(NextWebhookEvent);
    }
}
