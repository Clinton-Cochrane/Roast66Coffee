using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Services;
using CoffeeShopApi.Services.Payments;
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
    public async Task CheckoutAndPaidWebhook_AreProviderNeutralAndIdempotent()
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
        await context.SaveChangesAsync();

        var paymentService = scope.ServiceProvider.GetRequiredService<PaymentService>();
        var checkout = await paymentService.CreateCheckoutAsync(
            new CheckoutSessionRequest
            {
                CustomerName = "Gateway Customer",
                CustomerPhone = "5551234567",
                OrderItems =
                [
                    new CheckoutOrderItemRequest
                    {
                        MenuItemId = 42,
                        Quantity = 2
                    }
                ]
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

        var order = await context.Orders.SingleAsync();
        await context.Entry(payment).ReloadAsync();
        Assert.NotNull(order.PaidUtc);
        Assert.Equal(FakePaymentGateway.Name, order.PaymentProvider);
        Assert.Equal("fake-payment-123", order.PaymentReference);
        Assert.Equal(order.Id, payment.OrderId);
        Assert.Equal(PaymentStatuses.Paid, payment.Status);
        Assert.Equal("wallet", payment.Method);
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
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"payment-tests-{Guid.NewGuid():N}"));
        services.AddScoped<OrderService>();
        services.AddScoped<NotificationSettingsService>();
        services.AddScoped<TwilioService>();
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
