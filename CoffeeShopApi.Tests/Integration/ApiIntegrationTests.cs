using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoffeeShopApi.Tests.Integration;

/// <summary>
/// Integration tests for critical API flows: order creation, menu CRUD, admin login.
/// </summary>
public class ApiIntegrationTests : IClassFixture<WebAppFactory>
{
    private readonly WebAppFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ApiIntegrationTests(WebAppFactory factory)
    {
        _factory = factory;
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
        var response = await _client.PostOrderAsync(order, options: JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Order>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created!.Id > 0);
        Assert.Equal(order.CustomerName, created.CustomerName);
    }

    [Fact]
    public async Task PostOrder_DoesNotCreateCustomerEmailOrSmsNotifications()
    {
        var order = CreateValidOrder(
            $"Notify-{Guid.NewGuid():N}",
            $"555{Random.Shared.Next(1000000, 9999999)}");
        order.CustomerEmail = "customer@example.com";
        order.CustomerNotificationOptIn = true;
        var post = await _client.PostOrderAsync(order, options: JsonOptions);
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
        Assert.Empty(notifications!);
    }

    [Fact]
    public async Task PostOrder_SameIdempotencyKey_ReturnsOriginalOrder()
    {
        var order = new Order
        {
            CustomerName = "Duplicate Test Customer",
            CustomerPhone = "5559876543",
            OrderItems = [new OrderItem { MenuItemId = 1, Quantity = 2 }]
        };
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var firstResponse = await _client.PostOrderAsync(order, idempotencyKey, JsonOptions);
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<PublicOrderResponse>(JsonOptions);

        var secondResponse = await _client.PostOrderAsync(order, idempotencyKey, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.True(secondResponse.Headers.TryGetValues("Idempotency-Replayed", out var replayed));
        Assert.Equal("true", Assert.Single(replayed));

        var second = await secondResponse.Content.ReadFromJsonAsync<PublicOrderResponse>(JsonOptions);
        Assert.NotNull(first);
        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task PostOrder_SameIdempotencyKeyWithDifferentPayload_ReturnsConflict()
    {
        var key = Guid.NewGuid().ToString("N");
        var first = CreateValidOrder($"Conflict-{Guid.NewGuid():N}", "5551234567");
        var firstResponse = await _client.PostOrderAsync(first, key, JsonOptions);
        firstResponse.EnsureSuccessStatusCode();

        var changed = CreateValidOrder(first.CustomerName, first.CustomerPhone!);
        changed.OrderItems[0].Quantity = 3;
        var response = await _client.PostOrderAsync(changed, key, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("different order request", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostOrder_IdenticalPayloadWithDifferentKeys_CreatesDeliberateRepeat()
    {
        var order = CreateValidOrder($"Repeat-{Guid.NewGuid():N}", "5557654321");

        var firstResponse = await _client.PostOrderAsync(order, options: JsonOptions);
        var secondResponse = await _client.PostOrderAsync(order, options: JsonOptions);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<PublicOrderResponse>(JsonOptions);
        var second = await secondResponse.Content.ReadFromJsonAsync<PublicOrderResponse>(JsonOptions);
        Assert.NotEqual(first!.Id, second!.Id);
    }

    [Fact]
    public async Task LegacyAdminOrderRoute_UsesTheSameIdempotencyContract()
    {
        var key = Guid.NewGuid().ToString("N");
        var order = CreateValidOrder($"Legacy-{Guid.NewGuid():N}", "5553456789");

        var first = await _client.PostOrderAsync(
            order,
            key,
            JsonOptions,
            path: "/api/admin/orders");
        var replay = await _client.PostOrderAsync(
            order,
            key,
            JsonOptions,
            path: "/api/admin/orders");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
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
        var postResponse = await _client.PostOrderAsync(order, options: JsonOptions);
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
    public async Task GetAdminOrders_IncludesShotsAndTheirMenuItems()
    {
        int drinkId;
        int shotId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var drink = new MenuItem
            {
                Name = $"Admin drink {Guid.NewGuid():N}",
                Price = 4.00m,
                Description = "Integration test drink",
                CategoryType = CategoryType.DRINKS
            };
            var shot = new MenuItem
            {
                Name = $"Admin shot {Guid.NewGuid():N}",
                Price = 0.50m,
                Description = "Integration test shot",
                CategoryType = CategoryType.FLAVORS
            };
            context.MenuItems.AddRange(drink, shot);
            await context.SaveChangesAsync();
            drinkId = drink.Id;
            shotId = shot.Id;
        }

        var customerName = $"Admin shots {Guid.NewGuid():N}";
        var order = new Order
        {
            CustomerName = customerName,
            CustomerPhone = $"555{Random.Shared.Next(1000000, 9999999)}",
            OrderItems =
            [
                new OrderItem
                {
                    MenuItemId = drinkId,
                    Quantity = 1,
                    AddOns = [new AddOn { MenuItemId = shotId, Quantity = 2 }]
                }
            ]
        };
        var postResponse = await _client.PostOrderAsync(order, options: JsonOptions);
        postResponse.EnsureSuccessStatusCode();

        var token = await GetAdminToken();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/orders");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var orders = await response.Content.ReadFromJsonAsync<List<Order>>(JsonOptions);
        var adminOrder = Assert.Single(orders!, candidate => candidate.CustomerName == customerName);
        var orderItem = Assert.Single(adminOrder.OrderItems);
        var addOn = Assert.Single(orderItem.AddOns!);
        Assert.Equal(2, addOn.Quantity);
        Assert.Equal(shotId, addOn.MenuItemId);
        Assert.NotNull(addOn.MenuItem);
        Assert.StartsWith("Admin shot ", addOn.MenuItem!.Name);
    }

    [Fact]
    public async Task NotificationSettings_SaveAndGet_PersistsSmsFromAddress()
    {
        var token = await GetAdminToken();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var settingsPayload = new
        {
            adminPhoneNumber = "+15551230001",
            baristaPhoneNumber = "+15551230002",
            trailerPhoneNumber = "+15551230003",
            smsFromAddress = "+15551239999"
        };

        var saveResponse = await _client.PutAsJsonAsync("/api/admin/notificationSettings", settingsPayload, JsonOptions);
        saveResponse.EnsureSuccessStatusCode();

        var getResponse = await _client.GetAsync("/api/admin/notificationSettings");
        getResponse.EnsureSuccessStatusCode();
        var saved = await getResponse.Content.ReadFromJsonAsync<NotificationSettingsResponse>(JsonOptions);
        Assert.NotNull(saved);
        Assert.Equal(settingsPayload.adminPhoneNumber, saved!.AdminPhoneNumber);
        Assert.Equal(settingsPayload.smsFromAddress, saved.SmsFromAddress);
    }

    [Fact]
    public async Task CredentialSettings_WithAdminToken_ReturnsEnvKeyMetadata()
    {
        var token = await GetAdminToken();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/credential-settings");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var info = await response.Content.ReadFromJsonAsync<CredentialSettingsResponse>(JsonOptions);

        Assert.NotNull(info);
        Assert.Equal("Admin__Username", info!.UsernameEnvKey);
        Assert.Equal("Admin__Password", info.PasswordEnvKey);
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
        var post = await _client.PostOrderAsync(order, options: JsonOptions);
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
    public async Task PublicTracking_WithToken_ReturnsMinimalOrderAndSummary()
    {
        var order = CreateValidOrder(
            $"Summary-{Guid.NewGuid():N}",
            $"555{Random.Shared.Next(1000000, 9999999)}");
        var post = await _client.PostOrderAsync(order, options: JsonOptions);
        post.EnsureSuccessStatusCode();
        var created = await post.Content.ReadFromJsonAsync<PublicOrderResponse>(JsonOptions);
        Assert.NotNull(created);

        var tracked = await _client.GetAsync($"/api/order/track/{created!.TrackingToken}");
        tracked.EnsureSuccessStatusCode();
        var trackedJson = await tracked.Content.ReadAsStringAsync();
        Assert.DoesNotContain("customerPhone", trackedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customerEmail", trackedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stripePaymentIntentId", trackedJson, StringComparison.OrdinalIgnoreCase);

        var summary = await _client.GetAsync($"/api/order/track/{created.TrackingToken}/summary");
        summary.EnsureSuccessStatusCode();
        var summaryPayload = await summary.Content.ReadFromJsonAsync<OrderSummaryResponse>(JsonOptions);
        Assert.NotNull(summaryPayload);
        Assert.Equal(created.Id, summaryPayload!.OrderId);
        Assert.Equal($"/order-status?token={created.TrackingToken}", summaryPayload.TrackerUrl);

        var oldLookup = await _client.GetAsync(
            $"/api/order/lookup?orderId={created.Id}&customerName={Uri.EscapeDataString(order.CustomerName)}");
        Assert.Equal(HttpStatusCode.NotFound, oldLookup.StatusCode);
    }

    [Fact]
    public async Task PurgeNotificationLogs_RemovesOldRows()
    {
        var order = CreateValidOrder(
            $"Purge-{Guid.NewGuid():N}",
            $"555{Random.Shared.Next(1000000, 9999999)}");
        order.CustomerEmail = "customer@example.com";
        order.CustomerNotificationOptIn = true;
        var post = await _client.PostOrderAsync(order, options: JsonOptions);
        post.EnsureSuccessStatusCode();
        var created = await post.Content.ReadFromJsonAsync<PublicOrderDto>(JsonOptions);
        Assert.NotNull(created);

        var emailNotificationId = Guid.NewGuid();
        var smsNotificationId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.NotificationMessages.Add(new NotificationMessage
            {
                Id = emailNotificationId,
                EventType = "retention.test",
                RecipientRole = "customer",
                RecipientEmail = "customer@example.com",
                Channel = "email",
                TemplateKey = "retention_test",
                OrderId = created!.Id,
                DedupKey = $"retention-{emailNotificationId:N}",
                Status = "sent",
                CreatedUtc = DateTime.UtcNow.AddDays(-40),
                UpdatedUtc = DateTime.UtcNow.AddDays(-40)
            });
            db.NotificationMessages.Add(new NotificationMessage
            {
                Id = smsNotificationId,
                EventType = "retention.test",
                RecipientRole = "customer",
                RecipientPhone = "+15558675309",
                Channel = "sms",
                TemplateKey = "retention_test",
                OrderId = created.Id,
                DedupKey = $"retention-{smsNotificationId:N}",
                Status = "sent",
                CreatedUtc = DateTime.UtcNow.AddDays(-40),
                UpdatedUtc = DateTime.UtcNow.AddDays(-40)
            });
            await db.SaveChangesAsync();
        }

        var token = await GetAdminToken();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/notifications/purge-logs");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null(await verificationDb.NotificationMessages.FindAsync(emailNotificationId));
        Assert.Null(await verificationDb.NotificationMessages.FindAsync(smsNotificationId));
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
    public async Task UpdateMenuItem_WithoutAdminToken_ReturnsUnauthorized()
    {
        var item = new MenuItem
        {
            Id = 1,
            Name = "Unauthorized Update",
            Price = 0.01m,
            Description = "This update must not be accepted.",
            CategoryType = CategoryType.COFFEE
        };

        var response = await _client.PutAsJsonAsync("/api/menu/1", item, JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoints_ReturnOk()
    {
        var livenessResponse = await _client.GetAsync("/api/health");
        var readinessResponse = await _client.GetAsync("/api/health/ready");

        livenessResponse.EnsureSuccessStatusCode();
        readinessResponse.EnsureSuccessStatusCode();
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
    public async Task CreateCheckoutSession_WithoutPaymentProviderConfig_Returns503()
    {
        var payload = new
        {
            existingOrderId = 1,
            customerName = "Payment Test",
            customerPhone = "5550001234"
        };

        var response = await _client.PostAsJsonAsync("/api/payments/checkout-session", payload);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task CreateCheckoutSession_WithoutExistingOrder_ReturnsBadRequest()
    {
        var payload = new
        {
            customerName = "Payment Test",
            customerPhone = "5550001234"
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

    private class NotificationLogResponse
    {
        public string EventType { get; set; } = string.Empty;
        public string RecipientRole { get; set; } = string.Empty;
    }

    private class NotificationSettingsResponse
    {
        public string? AdminPhoneNumber { get; set; }
        public string? SmsFromAddress { get; set; }
    }

    private class CredentialSettingsResponse
    {
        public string UsernameEnvKey { get; set; } = string.Empty;
        public string PasswordEnvKey { get; set; } = string.Empty;
    }

    private class OrderSummaryResponse
    {
        public int OrderId { get; set; }
        public string TrackerUrl { get; set; } = string.Empty;
    }

    private class PublicOrderResponse
    {
        public int Id { get; set; }
        public string TrackingToken { get; set; } = string.Empty;
    }

}
