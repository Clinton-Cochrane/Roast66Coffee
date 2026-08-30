using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShopApi.Models;

namespace CoffeeShopApi.Tests.Integration;

public class OrderDeletionApiTests : IClassFixture<WebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;

    public OrderDeletionApiTests(WebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeleteOrder_IsUnavailableToAnonymousAndAdminCallers()
    {
        var order = new CreateOrderRequest
        {
            CustomerName = $"Deletion regression {Guid.NewGuid():N}",
            OrderItems =
            [
                new CreateOrderItemRequest { MenuItemId = 1, Quantity = 1 }
            ]
        };
        var createResponse = await _client.PostAsJsonAsync("/api/order", order, JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<PublicOrderDto>(JsonOptions);
        Assert.NotNull(created);

        var anonymousDelete = await _client.DeleteAsync($"/api/order/{created!.Id}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, anonymousDelete.StatusCode);

        var token = await GetAdminToken();
        using var adminDeleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/order/{created.Id}");
        adminDeleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var adminDelete = await _client.SendAsync(adminDeleteRequest);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, adminDelete.StatusCode);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/order/{created.Id}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getResponse = await _client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    private async Task<string> GetAdminToken()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/admin/login",
            new { username = "admin", password = "password" });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        Assert.NotNull(login);
        return login!.Token;
    }

    private sealed class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}
