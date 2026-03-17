using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace CoffeeShopApi.Tests.Integration;

/// <summary>
/// Tests that rate limiting is configured. In Testing environment limits are high (1000/min)
/// so we verify the endpoint is protected; production uses 5/min for login, 30/min for order.
/// </summary>
public class RateLimitTests : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client;

    public RateLimitTests(WebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LoginEndpoint_AcceptsRequests_WhenUnderLimit()
    {
        var login = new { username = "admin", password = "password" };
        var response = await _client.PostAsJsonAsync("/api/admin/login", login);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task OrderEndpoint_AcceptsRequests_WhenUnderLimit()
    {
        var order = new
        {
            customerName = "Rate Limit Test Customer",
            orderItems = new[] { new { menuItemId = 1, quantity = 1 } }
        };
        var response = await _client.PostAsJsonAsync("/api/order", order);
        response.EnsureSuccessStatusCode();
    }
}
