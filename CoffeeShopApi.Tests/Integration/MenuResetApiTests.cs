using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShopApi.Controllers;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CoffeeShopApi.Tests.Integration;

public class MenuResetApiTests : IClassFixture<WebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly WebAppFactory _factory;
    private readonly HttpClient _client;

    public MenuResetApiTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Reset_RequiresAdminAuthorization()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/menu/reset-to-defaults",
            new { confirmation = AdminController.DefaultMenuResetConfirmation });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reset_RejectsMissingConfirmationWithoutChangingMenu()
    {
        await ReplaceMenuAsync("Confirmation sentinel");
        await AuthenticateAsync(_client, "password");

        var response = await _client.PostAsJsonAsync(
            "/api/admin/menu/reset-to-defaults",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["Confirmation sentinel"], await GetMenuNamesAsync());
    }

    [Fact]
    public async Task LegacyGet_IsUnavailableAndCannotChangeMenu()
    {
        await ReplaceMenuAsync("GET sentinel");
        await AuthenticateAsync(_client, "password");

        var response = await _client.GetAsync("/api/admin/seed-menu?confirm=true");
        var newRouteResponse = await _client.GetAsync("/api/admin/menu/reset-to-defaults");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, newRouteResponse.StatusCode);
        Assert.Equal(["GET sentinel"], await GetMenuNamesAsync());
    }

    [Fact]
    public async Task InvalidDefaultData_IsRejectedWithoutLeakingDetailsOrChangingMenu()
    {
        using var invalidFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDefaultMenuProvider>();
                services.AddSingleton<IDefaultMenuProvider, InvalidDefaultMenuProvider>();
            }));
        using var client = invalidFactory.CreateClient();
        await using (var scope = invalidFactory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.MenuItems.RemoveRange(context.MenuItems);
            await context.SaveChangesAsync();
            context.MenuItems.Add(new MenuItem
            {
                Name = "Invalid-seed sentinel",
                Price = 2m,
                CategoryType = CategoryType.COFFEE
            });
            await context.SaveChangesAsync();
        }
        await AuthenticateAsync(client, "password");

        var response = await client.PostAsJsonAsync(
            "/api/admin/menu/reset-to-defaults",
            new { confirmation = AdminController.DefaultMenuResetConfirmation });
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("existing menu was left unchanged", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Name is required", responseBody, StringComparison.OrdinalIgnoreCase);
        await using var verificationScope = invalidFactory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Invalid-seed sentinel", (await verification.MenuItems.SingleAsync()).Name);
    }

    [Fact]
    public async Task Reset_ReturnsCounts_CanBeRepeated_AndPreservesOrderLines()
    {
        int orderId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.MenuItems.RemoveRange(context.MenuItems);
            await context.SaveChangesAsync();
            var item = new MenuItem
            {
                Name = "Ordered sentinel",
                Description = "Historical description",
                Price = 6m,
                CategoryType = CategoryType.COFFEE
            };
            var order = new Order
            {
                CustomerName = "Reset history customer",
                TrackingToken = new string('r', 43),
                OrderItems =
                [
                    new OrderItem
                    {
                        MenuItem = item,
                        Quantity = 1,
                        UnitPrice = 6m,
                        ItemName = item.Name,
                        ItemDescription = item.Description,
                        ItemCategoryType = item.CategoryType
                    }
                ]
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            orderId = order.Id;
        }
        await AuthenticateAsync(_client, "password");

        var firstResponse = await _client.PostAsJsonAsync(
            "/api/admin/menu/reset-to-defaults",
            new { confirmation = AdminController.DefaultMenuResetConfirmation });
        var firstSummary = await firstResponse.Content.ReadFromJsonAsync<MenuResetResponse>(JsonOptions);
        var secondResponse = await _client.PostAsJsonAsync(
            "/api/admin/menu/reset-to-defaults",
            new { confirmation = AdminController.DefaultMenuResetConfirmation });
        var secondSummary = await secondResponse.Content.ReadFromJsonAsync<MenuResetResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(1, firstSummary!.PreviousItemCount);
        Assert.True(firstSummary.NewItemCount > 1);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstSummary.NewItemCount, secondSummary!.PreviousItemCount);
        Assert.Equal(firstSummary.NewItemCount, secondSummary.NewItemCount);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var names = await verification.MenuItems.Select(item => item.Name).ToListAsync();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var retainedOrder = await verification.Orders
            .Include(order => order.OrderItems)
            .SingleAsync(order => order.Id == orderId);
        var retainedLine = Assert.Single(retainedOrder.OrderItems!);
        Assert.Equal("Ordered sentinel", retainedLine.ItemName);
        Assert.Equal(6m, retainedLine.UnitPrice);
    }

    private async Task ReplaceMenuAsync(string name)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.MenuItems.RemoveRange(context.MenuItems);
        await context.SaveChangesAsync();
        context.MenuItems.Add(new MenuItem
        {
            Name = name,
            Price = 1m,
            CategoryType = CategoryType.COFFEE
        });
        await context.SaveChangesAsync();
    }

    private async Task<string[]> GetMenuNamesAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.MenuItems.Select(item => item.Name).ToArrayAsync();
    }

    private static async Task AuthenticateAsync(HttpClient client, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/admin/login",
            new { username = "admin", password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
    }

    private sealed record MenuResetResponse(
        string Message,
        int PreviousItemCount,
        int NewItemCount);

    private sealed record LoginResponse(string Token);

    private sealed class InvalidDefaultMenuProvider : IDefaultMenuProvider
    {
        public IReadOnlyList<MenuItem> GetMenuItems() =>
        [
            new MenuItem
            {
                Name = null!,
                Price = 4m,
                CategoryType = CategoryType.COFFEE
            }
        ];
    }

    public class ProductionBehavior
    {
        [Fact]
        public async Task Reset_IsNotAvailableInProduction()
        {
            await using var factory = new ProductionWebAppFactory();
            using var client = factory.CreateClient();
            await AuthenticateAsync(client, ProductionWebAppFactory.AdminPassword);

            var response = await client.PostAsJsonAsync(
                "/api/admin/menu/reset-to-defaults",
                new { confirmation = AdminController.DefaultMenuResetConfirmation });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            await using var scope = factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("Production sentinel", (await context.MenuItems.SingleAsync()).Name);
        }
    }

    private sealed class ProductionWebAppFactory : WebApplicationFactory<Program>
    {
        public const string AdminPassword = "production-test-password";
        private readonly string _databaseName = $"ProductionMenuReset-{Guid.NewGuid():N}";

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            using var scope = host.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.MenuItems.Add(new MenuItem
            {
                Name = "Production sentinel",
                Price = 1m,
                CategoryType = CategoryType.COFFEE
            });
            context.SaveChanges();
            return host;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Production);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Admin:Username"] = "admin",
                    ["Admin:Password"] = AdminPassword,
                    ["Authentication:LegacySharedLoginEnabled"] = "true",
                    ["Jwt:Key"] = "ProductionMenuResetTestSigningKey_Minimum32Chars___",
                    ["Jwt:Issuer"] = "Roast66Coffee",
                    ["Jwt:Audience"] = "Roast66Coffee",
                    ["AllowedOrigins"] = "http://localhost"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }
    }
}
