using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
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

        var archivedOrder = new Order
        {
            CustomerName = "Archived item customer",
            OrderItems = [new OrderItem { MenuItemId = itemId, Quantity = 1 }]
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

    [Fact]
    public async Task FourthHomepageSpecial_ReturnsConflictWithoutChangingPriorSelections()
    {
        int[] itemIds;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var testId = Guid.NewGuid().ToString("N");
            context.MenuItems.AddRange(
                CreateHomepageCandidate($"First {testId}"),
                CreateHomepageCandidate($"Second {testId}"),
                CreateHomepageCandidate($"Third {testId}"),
                CreateHomepageCandidate($"Fourth {testId}"));
            await context.SaveChangesAsync();
            itemIds = await context.MenuItems
                .Where(item => item.Name.EndsWith(testId))
                .OrderBy(item => item.Id)
                .Select(item => item.Id)
                .ToArrayAsync();
        }

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAdminTokenAsync());
        foreach (var itemId in itemIds[..3])
        {
            var response = await _client.PutAsJsonAsync(
                $"/api/admin/menu/{itemId}/homepage-special",
                new { isSelected = true });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var conflict = await _client.PutAsJsonAsync(
            $"/api/admin/menu/{itemIds[3]}/homepage-special",
            new { isSelected = true });

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var conflictBody = await conflict.Content.ReadFromJsonAsync<ConflictResponse>(JsonOptions);
        Assert.Equal("Only 3 homepage specials can be selected.", conflictBody!.Message);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var selectedIds = await verification.MenuItems
            .Where(item => item.IsFeaturedOnHome && itemIds.Contains(item.Id))
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToArrayAsync();
        Assert.Equal(itemIds[..3], selectedIds);
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

    private static MenuItem CreateHomepageCandidate(string name) =>
        new()
        {
            Name = name,
            Description = "Homepage-special conflict integration test",
            Price = 4m,
            CategoryType = CategoryType.SPECIALS
        };

    private sealed record ConflictResponse(string Message);

    private sealed class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}
