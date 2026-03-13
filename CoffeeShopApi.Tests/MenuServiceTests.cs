using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoffeeShopApi.Tests;

public class MenuServiceTests
{
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetMenuItemsAsync_ReturnsEmpty_WhenNoItems()
    {
        await using var context = CreateInMemoryContext();
        var service = new MenuService(context);

        var result = await service.GetMenuItemsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateMenuItemAsync_AddsItem()
    {
        await using var context = CreateInMemoryContext();
        var service = new MenuService(context);
        var item = new MenuItem { Name = "Espresso", Price = 2.50m, Description = "Strong shot", CategoryType = CategoryType.COFFEE };

        var created = await service.CreateMenuItemAsync(item);

        Assert.True(created.Id > 0);
        Assert.Equal("Espresso", created.Name);
        var all = await service.GetMenuItemsAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task BulkReplaceAsync_ReplacesAllItems()
    {
        await using var context = CreateInMemoryContext();
        context.MenuItems.Add(new MenuItem { Name = "Old", Price = 1m, Description = "x", CategoryType = CategoryType.COFFEE });
        await context.SaveChangesAsync();

        var service = new MenuService(context);
        var newItems = new List<MenuItem>
        {
            new() { Name = "Latte", Price = 3.50m, Description = "Creamy", CategoryType = CategoryType.COFFEE },
            new() { Name = "Mocha", Price = 4m, Description = "Chocolate", CategoryType = CategoryType.COFFEE }
        };

        await service.BulkReplaceAsync(newItems);

        var result = await service.GetMenuItemsAsync();
        Assert.Equal(2, result.Count());
        Assert.Contains(result, r => r.Name == "Latte");
        Assert.Contains(result, r => r.Name == "Mocha");
    }
}
