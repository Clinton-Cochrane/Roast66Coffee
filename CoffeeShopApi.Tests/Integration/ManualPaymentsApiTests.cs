using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShopApi.Tests.Integration;

public class ManualPaymentsApiTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    [Fact]
    public async Task Recording_RequiresStaffAuthorization()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/payments/manual", new { orderId = 66, method = "cash" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedTokenWithoutAdminRole_CannotRecordPayments()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>()
            .FindByNameAsync("integration-admin");
        Assert.NotNull(user);
        var token = scope.ServiceProvider.GetRequiredService<StaffTokenService>().Create(user, []);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/payments/manual", new { orderId = 66, method = "cash" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("cash")]
    [InlineData("other")]
    public async Task RecordingAndReplay_ReturnOneServerPricedStaffReceipt(string method)
    {
        int orderId;
        using (var scope = factory.Services.CreateScope())
        {
            var order = await ManualPaymentServiceTests.AddOrderAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
            orderId = order.Id;
        }
        using var client = await StaffClientAsync();
        var first = await client.PostAsJsonAsync("/api/payments/manual", new {
            orderId, method, amount = 0.01m, currency = "EUR", provider = "stripe"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var recorded = await first.Content.ReadFromJsonAsync<ManualPaymentResult>();
        Assert.NotNull(recorded);
        Assert.Equal(orderId, recorded.OrderId);
        Assert.Equal(method, recorded.Method);
        Assert.Equal(17.05m, recorded.Amount);
        Assert.Equal("USD", recorded.Currency);
        Assert.False(recorded.WasReplay);
        var repeat = await client.PostAsJsonAsync("/api/payments/manual", new { orderId, method });
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        var replay = await repeat.Content.ReadFromJsonAsync<ManualPaymentResult>();
        Assert.NotNull(replay);
        Assert.True(replay.WasReplay);
        Assert.Equal(recorded.PaymentId, replay.PaymentId);
        Assert.Equal(recorded.PaidUtc, replay.PaidUtc);

        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await context.Payments.Where(payment => payment.OrderId == orderId).ToListAsync());
        var audit = await context.AuditEvents.SingleAsync(entry => entry.EntityId == orderId.ToString() && entry.Action == "payment.manual.recorded");
        Assert.Equal("Integration Admin", audit.ActorDisplayName);
        Assert.NotNull(audit.ActorUserId);
    }

    [Theory]
    [InlineData("card")]
    [InlineData("stripe")]
    [InlineData("")]
    [InlineData("Cash")]
    public async Task UnsupportedMethod_ReturnsBadRequest(string method)
    {
        using var client = await StaffClientAsync();
        var response = await client.PostAsJsonAsync("/api/payments/manual", new { orderId = 66, method });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingMethodAndInvalidOrderId_ReturnBadRequest()
    {
        using var client = await StaffClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/payments/manual", new { orderId = 66 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/payments/manual", new { orderId = 0, method = "cash" })).StatusCode);
    }

    [Fact]
    public async Task MissingOrder_ReturnsNotFound()
    {
        using var client = await StaffClientAsync();
        var response = await client.PostAsJsonAsync("/api/payments/manual", new { orderId = int.MaxValue, method = "cash" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AlreadyPaidOrderAndChangedMethod_ReturnConflict()
    {
        int orderId;
        using (var scope = factory.Services.CreateScope())
        {
            orderId = (await ManualPaymentServiceTests.AddOrderAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())).Id;
        }
        using var client = await StaffClientAsync();
        var first = await client.PostAsJsonAsync("/api/payments/manual", new { orderId, method = "cash" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var changed = await client.PostAsJsonAsync("/api/payments/manual", new { orderId, method = "other" });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);

        int paidOrderId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var paid = await ManualPaymentServiceTests.AddOrderAsync(context);
            paid.PaidUtc = DateTime.UtcNow;
            paid.PaymentProvider = "stripe";
            await context.SaveChangesAsync();
            paidOrderId = paid.Id;
        }
        var paidResponse = await client.PostAsJsonAsync("/api/payments/manual", new { orderId = paidOrderId, method = "cash" });
        Assert.Equal(HttpStatusCode.Conflict, paidResponse.StatusCode);
    }

    private async Task<HttpClient> StaffClientAsync()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/admin/login", new {
            username = "integration-admin", password = "IntegrationPassword1!"
        });
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);
        return client;
    }

    private sealed record LoginResponse(string Token);
}
