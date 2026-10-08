using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Services;
using CoffeeShopApi.Services.Payments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShopApi.Tests.Integration;

public class InPersonPaymentsApiTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    [Fact]
    public async Task BothEndpoints_RequireStaffAuthorization()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/payments/in-person", new { orderId = 66 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync($"/api/payments/in-person/{Guid.NewGuid()}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>().FindByNameAsync("integration-admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            scope.ServiceProvider.GetRequiredService<StaffTokenService>().Create(user!, []));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/payments/in-person", new { orderId = 66 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync($"/api/payments/in-person/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task ProductionRegistration_HasNoFakeAndReturnsUseful503()
    {
        using var scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetServices<IInPersonPaymentGateway>());
        var order = await ManualPaymentServiceTests.AddOrderAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        using var client = await StaffClientAsync(factory);
        var response = await client.PostAsJsonAsync("/api/payments/in-person", new { orderId = order.Id });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("not configured", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StartAndPoll_IgnoreBrowserPricingAndExposeOnlyNeutralStatus()
    {
        var gateway = new FakeInPersonGateway();
        using var app = WithGateway(gateway);
        int orderId;
        using (var scope = app.Services.CreateScope())
            orderId = (await ManualPaymentServiceTests.AddOrderAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())).Id;
        using var client = await StaffClientAsync(app);
        var response = await client.PostAsJsonAsync("/api/payments/in-person", new
        {
            orderId, amount = 0.01m, currency = "EUR", provider = "browser-provider", status = "paid"
        });
        response.EnsureSuccessStatusCode();
        var start = (await response.Content.ReadFromJsonAsync<InPersonPaymentResult>())!;
        Assert.Equal(17.05m, start.Amount);
        Assert.Equal("USD", start.Currency);
        Assert.Equal(gateway.ProviderName, start.Provider);
        Assert.Equal(PaymentStatuses.Pending, start.Status);
        var repeated = await client.PostAsJsonAsync("/api/payments/in-person", new { orderId });
        Assert.Equal(start.PaymentId, (await repeated.Content.ReadFromJsonAsync<InPersonPaymentResult>())!.PaymentId);
        var poll = await client.GetAsync($"/api/payments/in-person/{start.PaymentId}");
        poll.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await poll.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "amount", "currency", "orderId", "paidUtc", "paymentId", "provider", "status" },
            json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Single(gateway.StartCalls);

        gateway.WebhookEvent = new(start.PaymentId, null, "provider-transaction", GatewayPaymentStatus.Paid, "card");
        (await client.PostAsync($"/api/payments/{gateway.ProviderName}/webhook", new StringContent("adapter-verified"))).EnsureSuccessStatusCode();
        var paid = await client.GetFromJsonAsync<InPersonPaymentResult>($"/api/payments/in-person/{start.PaymentId}");
        Assert.Equal(PaymentStatuses.Paid, paid!.Status);
        Assert.NotNull(paid.PaidUtc);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync("/api/payments/in-person", new { orderId })).StatusCode);
        Assert.Single(gateway.StartCalls);
    }

    [Fact]
    public async Task InvalidAndMissingTargets_ReturnUsefulClientErrors()
    {
        using var app = WithGateway(new FakeInPersonGateway());
        using var client = await StaffClientAsync(app);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/payments/in-person", new { orderId = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync("/api/payments/in-person", new { orderId = int.MaxValue })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/payments/in-person/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task AdapterRejectedWebhook_CannotMarkCardPaid()
    {
        var gateway = new FakeInPersonGateway { RejectWebhook = true };
        using var app = WithGateway(gateway);
        int orderId;
        using (var scope = app.Services.CreateScope())
            orderId = (await ManualPaymentServiceTests.AddOrderAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())).Id;
        using var client = await StaffClientAsync(app);
        var response = await client.PostAsJsonAsync("/api/payments/in-person", new { orderId });
        response.EnsureSuccessStatusCode();
        var start = (await response.Content.ReadFromJsonAsync<InPersonPaymentResult>())!;
        gateway.WebhookEvent = new(start.PaymentId, null, "provider-transaction", GatewayPaymentStatus.Paid, "card");
        var unverified = await client.PostAsync($"/api/payments/{gateway.ProviderName}/webhook", new StringContent("unverified"));
        Assert.Equal(HttpStatusCode.BadRequest, unverified.StatusCode);
        Assert.Equal(PaymentStatuses.Pending,
            (await client.GetFromJsonAsync<InPersonPaymentResult>($"/api/payments/in-person/{start.PaymentId}"))!.Status);
    }

    private WebApplicationFactory<Program> WithGateway(FakeInPersonGateway gateway) => factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Payments:InPersonProvider"] = gateway.ProviderName }));
        builder.ConfigureTestServices(services => services.AddSingleton<IInPersonPaymentGateway>(gateway));
    });

    private static async Task<HttpClient> StaffClientAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/admin/login", new
        {
            username = "integration-admin", password = "IntegrationPassword1!"
        });
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);
        return client;
    }

    private sealed record LoginResponse(string Token);
}
