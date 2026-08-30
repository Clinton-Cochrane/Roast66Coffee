using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShopApi.Models;
using Xunit;

namespace CoffeeShopApi.Tests.Integration;

/// <summary>
/// API tests for validation: invalid MenuItem, invalid Order.
/// </summary>
public class ValidationApiTests : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ValidationApiTests(WebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostMenuItem_InvalidItem_EmptyName_ReturnsBadRequest()
    {
        var token = await GetAdminToken();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var invalidItem = new MenuItem
        {
            Name = "",
            Price = 2.50m,
            Description = "Valid description",
            CategoryType = CategoryType.COFFEE
        };

        var response = await _client.PostAsJsonAsync("/api/menu", invalidItem, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostMenuItem_InvalidItem_NegativePrice_ReturnsBadRequest()
    {
        var token = await GetAdminToken();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var invalidItem = new MenuItem
        {
            Name = "Valid Name",
            Price = -1m,
            Description = "Valid description",
            CategoryType = CategoryType.COFFEE
        };

        var response = await _client.PostAsJsonAsync("/api/menu", invalidItem, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostOrder_EmptyOrderItems_ReturnsBadRequest()
    {
        var order = new Order
        {
            CustomerName = "Test Customer",
            OrderItems = []
        };

        var response = await _client.PostAsJsonAsync("/api/order", order, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostOrder_NullOrderItems_ReturnsBadRequest()
    {
        var order = new Order
        {
            CustomerName = "Test Customer",
            OrderItems = null!
        };

        var response = await _client.PostAsJsonAsync("/api/order", order, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostOrder_EmptyCustomerName_ReturnsBadRequest()
    {
        var order = new Order
        {
            CustomerName = "",
            OrderItems = [new OrderItem { MenuItemId = 1, Quantity = 1 }]
        };

        var response = await _client.PostAsJsonAsync("/api/order", order, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostOrder_InvalidQuantity_Zero_ReturnsBadRequest()
    {
        var order = new Order
        {
            CustomerName = "Test Customer",
            OrderItems = [new OrderItem { MenuItemId = 1, Quantity = 0 }]
        };

        var response = await _client.PostAsJsonAsync("/api/order", order, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminLogin_EmptyCredentials_ReturnsBadRequest()
    {
        var login = new { username = "", password = "" };
        var response = await _client.PostAsJsonAsync("/api/admin/login", login);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> GetAdminToken()
    {
        var login = new { username = "admin", password = "password" };
        var response = await _client.PostAsJsonAsync("/api/admin/login", login);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        return content!.Token;
    }

    private class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}
