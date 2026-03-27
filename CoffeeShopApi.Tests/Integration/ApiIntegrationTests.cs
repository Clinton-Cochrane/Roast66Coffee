using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CoffeeShopApi.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CoffeeShopApi.Tests.Integration;

/// <summary>
/// Integration tests for critical API flows: order creation, menu CRUD, admin login.
/// </summary>
public class ApiIntegrationTests : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ApiIntegrationTests(WebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AdminLogin_ValidCredentials_ReturnsToken()
    {
        var login = new { username = "admin", password = "password" };
        var response = await _client.PostAsJsonAsync("/api/admin/login", login);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        Assert.NotNull(content);
        Assert.False(string.IsNullOrEmpty(content!.Token));
    }

    [Fact]
    public async Task AdminLogin_InvalidCredentials_ReturnsUnauthorized()
    {
        var login = new { username = "wrong", password = "wrong" };
        var response = await _client.PostAsJsonAsync("/api/admin/login", login);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_WithoutEmailConfig_ReturnsServiceUnavailable()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/forgot-password", new { });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task GetMenuItems_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/menu");

        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>(JsonOptions);
        Assert.NotNull(items);
    }

    [Fact]
    public async Task PostOrder_ValidOrder_CreatesOrder()
    {
        var order = CreateValidOrder();
        var response = await _client.PostAsJsonAsync("/api/order", order, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Order>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created!.Id > 0);
        Assert.Equal(order.CustomerName, created.CustomerName);
    }

    [Fact]
    public async Task PostOrder_CreatesNotificationLogEntries()
    {
        var order = CreateValidOrder(
            $"Notify-{Guid.NewGuid():N}",
            $"555{Random.Shared.Next(1000000, 9999999)}");
        var post = await _client.PostAsJsonAsync("/api/order", order, JsonOptions);
        post.EnsureSuccessStatusCode();
        var created = await post.Content.ReadFromJsonAsync<Order>(JsonOptions);
        Assert.NotNull(created);

        var token = await GetAdminToken();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/orders/{created!.Id}/notifications");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var notificationsResponse = await _client.SendAsync(request);
        notificationsResponse.EnsureSuccessStatusCode();
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<List<NotificationLogResponse>>(JsonOptions);

        Assert.NotNull(notifications);
        Assert.NotEmpty(notifications!);
        Assert.Contains(notifications!, n => n.EventType == "order.created");
    }

    [Fact]
    public async Task PostOrder_DuplicateOrderWithinWindow_Returns409Conflict()
    {
        var order = new Order
        {
            CustomerName = "Duplicate Test Customer",
            CustomerPhone = "5559876543",
            OrderItems = [new OrderItem { MenuItemId = 1, Quantity = 2 }]
        };
        var firstResponse = await _client.PostAsJsonAsync("/api/order", order, JsonOptions);
        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await _client.PostAsJsonAsync("/api/order", order, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        var conflictBody = await secondResponse.Content.ReadFromJsonAsync<DuplicateOrderResponse>(JsonOptions);
        Assert.NotNull(conflictBody);
        Assert.NotNull(conflictBody!.Message);
        Assert.True(conflictBody.ExistingOrderId > 0);
        Assert.NotNull(conflictBody.Order);
    }

    [Fact]
    public async Task PostOrder_ThenGetOrders_WithAdminToken_ReturnsOrder()
    {
        var token = await GetAdminToken();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var order = new Order
        {
            CustomerName = "Admin Token Test Customer",
            CustomerPhone = "5559998888",
            OrderItems = [new OrderItem { MenuItemId = 1, Quantity = 1 }]
        };
        var postResponse = await _client.PostAsJsonAsync("/api/order", order, JsonOptions);
        postResponse.EnsureSuccessStatusCode();

        var getResponse = await _client.GetAsync("/api/order");
        getResponse.EnsureSuccessStatusCode();
        var orders = await getResponse.Content.ReadFromJsonAsync<List<Order>>(JsonOptions);
        Assert.NotNull(orders);
        Assert.Contains(orders, o => o.CustomerName == "Admin Token Test Customer");
    }

    [Fact]
    public async Task GetAdminOrders_WithToken_SameAuthorizationAsOrderController()
    {
        var token = await GetAdminToken();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/orders");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_ToReadyForPickup_LogsCustomerReadyNotification()
    {
        var token = await GetAdminToken();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var order = CreateValidOrder(
            $"Ready-{Guid.NewGuid():N}",
            $"555{Random.Shared.Next(1000000, 9999999)}");
        var post = await _client.PostAsJsonAsync("/api/order", order, JsonOptions);
        post.EnsureSuccessStatusCode();
        var created = await post.Content.ReadFromJsonAsync<Order>(JsonOptions);
        Assert.NotNull(created);

        var firstUpdate = await _client.PutAsync($"/api/admin/updateOrderStatus/{created!.Id}/status", null);
        firstUpdate.EnsureSuccessStatusCode();
        var secondUpdate = await _client.PutAsync($"/api/admin/updateOrderStatus/{created.Id}/status", null);
        secondUpdate.EnsureSuccessStatusCode();

        var notificationsResponse = await _client.GetAsync($"/api/admin/orders/{created.Id}/notifications");
        notificationsResponse.EnsureSuccessStatusCode();
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<List<NotificationLogResponse>>(JsonOptions);

        Assert.NotNull(notifications);
        Assert.Contains(notifications!, n => n.EventType == "order.ready_for_pickup" && n.RecipientRole == "customer");
    }

    [Fact]
    public async Task AdminMenuCrud_CreateUpdateDelete_WithToken()
    {
        var token = await GetAdminToken();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var newItem = new MenuItem
        {
            Name = "Test Drink",
            Price = 4.99m,
            Description = "Integration test item",
            CategoryType = CategoryType.COFFEE
        };

        var createResponse = await _client.PostAsJsonAsync("/api/menu", newItem, JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created!.Id > 0);

        created!.Name = "Updated Drink";
        var updateResponse = await _client.PutAsJsonAsync($"/api/menu/{created.Id}", created, JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var deleteResponse = await _client.DeleteAsync($"/api/menu/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task KeepAliveHeartbeat_WithAdminToken_ReturnsAccepted()
    {
        var token = await GetAdminToken();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ops/keepalive/heartbeat");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { source = "integration-test" });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task CreateCheckoutSession_WithoutStripeConfig_Returns503()
    {
        var payload = new
        {
            customerName = "Stripe Test",
            customerPhone = "5550001234",
            orderItems = new[]
            {
                new
                {
                    menuItemId = 1,
                    quantity = 1,
                    notes = "No sugar",
                    addOns = Array.Empty<object>()
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/payments/checkout-session", payload);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task CreateCheckoutSession_EmptyOrderItemsWithoutExistingOrder_ReturnsBadRequest()
    {
        var payload = new
        {
            customerName = "Stripe Test",
            customerPhone = "5550001234",
            orderItems = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/api/payments/checkout-session", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Order CreateValidOrder(string? customerName = null, string? customerPhone = null)
    {
        return new Order
        {
            CustomerName = customerName ?? "Integration Test Customer",
            CustomerPhone = customerPhone ?? "5551234567",
            OrderItems =
            [
                new OrderItem { MenuItemId = 1, Quantity = 2 }
            ]
        };
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

    private class DuplicateOrderResponse
    {
        public string Message { get; set; } = string.Empty;
        public int ExistingOrderId { get; set; }
        public Order? Order { get; set; }
    }

    private class NotificationLogResponse
    {
        public string EventType { get; set; } = string.Empty;
        public string RecipientRole { get; set; } = string.Empty;
    }
}
