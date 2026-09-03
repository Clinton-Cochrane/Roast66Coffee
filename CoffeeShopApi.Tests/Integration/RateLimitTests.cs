using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace CoffeeShopApi.Tests.Integration;

/// <summary>
/// Exercises the fixed-window policies with deliberately small Testing-only limits.
/// Normal integration tests keep high limits so shared factory traffic cannot make them flaky.
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
        var response = await _client.PostOrderAsync(order);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task LoginEndpoint_RejectsRequests_WhenFixedWindowIsExhausted()
    {
        await using var factory = new WebAppFactory(loginPermitLimit: 2);
        using var client = factory.CreateClient();
        var invalidLogin = new { username = "wrong", password = "wrong" };

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/admin/login", invalidLogin)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/admin/login", invalidLogin)).StatusCode);

        var rejected = await client.PostAsJsonAsync("/api/admin/login", invalidLogin);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Contains("Too many requests", await rejected.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OrderEndpoint_RejectsRequests_WhenFixedWindowIsExhausted()
    {
        await using var factory = new WebAppFactory(orderPermitLimit: 1);
        using var client = factory.CreateClient();
        var invalidOrder = new
        {
            customerName = "Rate Limit Rejection Customer",
            orderItems = Array.Empty<object>()
        };

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PostOrderAsync(invalidOrder)).StatusCode);

        var rejected = await client.PostOrderAsync(invalidOrder);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task LoginEndpoint_UsesTrustedForwardedClientIpForSeparatePartitions()
    {
        await using var factory = new WebAppFactory(loginPermitLimit: 1);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.SendAsync(LoginRequest("127.0.0.1", "203.0.113.10"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.SendAsync(LoginRequest("127.0.0.1", "203.0.113.10"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.SendAsync(LoginRequest("127.0.0.1", "203.0.113.11"))).StatusCode);
    }

    [Fact]
    public async Task LoginEndpoint_IgnoresForwardedClientIpFromUntrustedPeer()
    {
        await using var factory = new WebAppFactory(loginPermitLimit: 1);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.SendAsync(LoginRequest("192.0.2.20", "203.0.113.10", "198.51.100.10"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.SendAsync(LoginRequest("192.0.2.20", "203.0.113.11", "198.51.100.11"))).StatusCode);
    }

    private static HttpRequestMessage LoginRequest(
        string transportIp,
        string forwardedClientIp,
        string? spoofedXForwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/login")
        {
            Content = JsonContent.Create(new { username = "wrong", password = "wrong" })
        };
        request.Headers.Add("X-Test-Transport-IP", transportIp);
        request.Headers.Add("CF-Connecting-IP", forwardedClientIp);
        if (spoofedXForwardedFor != null)
        {
            request.Headers.Add("X-Forwarded-For", spoofedXForwardedFor);
        }

        return request;
    }
}
