using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoffeeShopApi.Tests.Integration;

public class OrderTrackingApiTests : IClassFixture<WebAppFactory>
{
    private const string UnavailableCode = "order_status_unavailable";
    private const string UnavailableMessage = "This order status is no longer available.";

    private readonly WebAppFactory _factory;
    private readonly HttpClient _client;

    public OrderTrackingApiTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TrackOrder_WithRetainedOrder_ReturnsOrder()
    {
        var created = await CreateOrderAsync();

        var response = await _client.GetAsync($"/api/order/track/{created.TrackingToken}");

        response.EnsureSuccessStatusCode();
        var tracked = await response.Content.ReadFromJsonAsync<TrackedOrderResponse>();
        Assert.NotNull(tracked);
        Assert.Equal(created.Id, tracked!.Id);
        Assert.Equal(created.TrackingToken, tracked.TrackingToken);
    }

    [Fact]
    public async Task TrackOrder_WithMissingMalformedOrUnknownToken_ReturnsSameResponse()
    {
        var missing = await GetUnavailableResponseAsync("/api/order/track");
        var malformed = await GetUnavailableResponseAsync("/api/order/track/not-a-token");
        var unknownToken = new string('a', 43);
        var unknown = await GetUnavailableResponseAsync($"/api/order/track/{unknownToken}");

        Assert.Equal(missing, malformed);
        Assert.Equal(missing, unknown);
    }

    [Fact]
    public async Task TrackOrder_WithPurgedOrder_ReturnsUnavailableResponse()
    {
        var created = await CreateOrderAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var order = await db.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.AddOns)
                .SingleAsync(o => o.Id == created.Id);
            db.Orders.Remove(order);
            await db.SaveChangesAsync();
        }

        await GetUnavailableResponseAsync($"/api/order/track/{created.TrackingToken}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("/notifications")]
    [InlineData("/summary")]
    public async Task PublicTrackingEndpoints_WithUnknownToken_ReturnSameResponse(string suffix)
    {
        var unknownToken = new string('z', 43);

        await GetUnavailableResponseAsync($"/api/order/track/{unknownToken}{suffix}");
    }

    private async Task<CreatedOrderResponse> CreateOrderAsync()
    {
        var order = new CreateOrderRequest
        {
            CustomerName = $"Tracking-{Guid.NewGuid():N}",
            OrderItems = [new CreateOrderItemRequest { MenuItemId = 1, Quantity = 1 }]
        };
        var response = await _client.PostAsJsonAsync("/api/order", order);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreatedOrderResponse>();
        return created ?? throw new InvalidOperationException("Order creation returned no body.");
    }

    private async Task<string> GetUnavailableResponseAsync(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var properties = document.RootElement.EnumerateObject().ToList();
        Assert.Equal(2, properties.Count);
        Assert.Equal(UnavailableCode, document.RootElement.GetProperty("code").GetString());
        Assert.Equal(UnavailableMessage, document.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("customer", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("orderItems", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trackingToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expired", body, StringComparison.OrdinalIgnoreCase);
        return body;
    }

    private sealed class CreatedOrderResponse
    {
        public int Id { get; set; }
        public string TrackingToken { get; set; } = string.Empty;
    }

    private sealed class TrackedOrderResponse
    {
        public int Id { get; set; }
        public string TrackingToken { get; set; } = string.Empty;
    }
}
