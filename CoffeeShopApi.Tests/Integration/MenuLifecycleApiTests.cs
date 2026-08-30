using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShopApi.Tests.Integration;

public class MenuLifecycleApiTests : IClassFixture<WebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly WebAppFactory _factory;
    private readonly HttpClient _client;

    public MenuLifecycleApiTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ArchiveRestoreAndPermanentDelete_UseDistinctAdminWorkflows()
    {
        int itemId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = new MenuItem
            {
                Name = $"Lifecycle {Guid.NewGuid():N}",
                Description = "Lifecycle integration test",
                Price = 5m,
                CategoryType = CategoryType.COFFEE,
                IsFeaturedOnHome = true
            };
            context.MenuItems.Add(item);
            await context.SaveChangesAsync();
            itemId = item.Id;
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/admin/menu")).StatusCode);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAdminTokenAsync());

        var archive = await _client.PutAsync($"/api/admin/menu/{itemId}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var publicMenu = await _client.GetFromJsonAsync<List<MenuItem>>("/api/menu", JsonOptions);
        Assert.DoesNotContain(publicMenu!, item => item.Id == itemId);

        var adminMenu = await _client.GetFromJsonAsync<List<MenuItem>>("/api/admin/menu", JsonOptions);
        var archivedItem = Assert.Single(adminMenu!, item => item.Id == itemId);
        Assert.True(archivedItem.IsArchived);
        Assert.False(archivedItem.IsFeaturedOnHome);

        var archivedOrder = new CreateOrderRequest
        {
            CustomerName = "Archived item customer",
            OrderItems = [new CreateOrderItemRequest { MenuItemId = itemId, Quantity = 1 }]
        };
        var archivedOrderResponse = await _client.PostAsJsonAsync(
            "/api/order",
            archivedOrder,
            JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, archivedOrderResponse.StatusCode);

        var restore = await _client.PutAsync($"/api/admin/menu/{itemId}/restore", null);
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);
        publicMenu = await _client.GetFromJsonAsync<List<MenuItem>>("/api/menu", JsonOptions);
        Assert.Contains(publicMenu!, item => item.Id == itemId);

        var delete = await _client.DeleteAsync($"/api/admin/menu/{itemId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        adminMenu = await _client.GetFromJsonAsync<List<MenuItem>>("/api/admin/menu", JsonOptions);
        Assert.DoesNotContain(adminMenu!, item => item.Id == itemId);
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/login",
            new { username = "admin", password = "password" });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        return login!.Token;
    }

    private sealed class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}
