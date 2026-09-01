using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShopApi.Tests.Integration;

public class AdminOrderHistoryApiTests : IClassFixture<WebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly WebAppFactory _factory;
    private readonly HttpClient _client;

    public AdminOrderHistoryApiTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AdminRoute_ReturnsThePagedContract()
    {
        var marker = $"History-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Orders.Add(new Order
            {
                TrackingToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                    .PadRight(43, 'x')[..43],
                CustomerName = marker,
                OrderDate = DateTime.UtcNow,
                OrderItems =
                [
                    new OrderItem
                    {
                        Quantity = 1,
                        ItemName = "Superman",
                        ItemDescription = "Snapshot",
                        AddOns = []
                    }
                ]
            });
            await context.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAdminToken());

        var admin = await _client.GetFromJsonAsync<AdminOrderHistoryResponse>(
            $"/api/admin/orders?search={marker}",
            JsonOptions);
        Assert.NotNull(admin);
        Assert.Equal(50, admin.PageSize);
        Assert.Equal(marker, Assert.Single(admin.Items).CustomerName);
    }

    [Fact]
    public async Task AdminRoute_RequiresAdminAuthorization()
    {
        var response = await _client.GetAsync("/api/admin/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LegacyOrderHistoryRoute_IsRemoved()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAdminToken());

        var response = await _client.GetAsync("/api/order");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/orders?page=0")]
    [InlineData("/api/admin/orders?page=2147483647")]
    [InlineData("/api/admin/orders?status=unknown")]
    [InlineData("/api/admin/orders?fromUtc=2026-08-31T12:00:00Z&toUtc=2026-08-30T12:00:00Z")]
    public async Task AdminOrderHistoryRoute_RejectsInvalidFilters(string path)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAdminToken());

        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> GetAdminToken()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/login",
            new { Username = "admin", Password = "password" });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }
}
