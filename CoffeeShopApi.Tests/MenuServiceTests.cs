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

    [Fact]
    public async Task SetHomepageSpecialAsync_RejectsFourthSelection()
    {
        await using var context = CreateInMemoryContext();
        context.MenuItems.AddRange(
            new MenuItem { Name = "One", Price = 1m, CategoryType = CategoryType.SPECIALS, IsFeaturedOnHome = true },
            new MenuItem { Name = "Two", Price = 2m, CategoryType = CategoryType.SPECIALS, IsFeaturedOnHome = true },
            new MenuItem { Name = "Three", Price = 3m, CategoryType = CategoryType.SPECIALS, IsFeaturedOnHome = true },
            new MenuItem { Name = "Four", Price = 4m, CategoryType = CategoryType.SPECIALS });
        await context.SaveChangesAsync();
        var fourth = await context.MenuItems.SingleAsync(item => item.Name == "Four");
        var service = new MenuService(context);

        var result = await service.SetHomepageSpecialAsync(fourth.Id, true);

        Assert.Equal(HomepageSpecialSelectionResult.LimitReached, result);
        Assert.False(fourth.IsFeaturedOnHome);
    }

    [Fact]
    public async Task SetHomepageSpecialAsync_AllowsSelectionAfterOneIsRemoved()
    {
        await using var context = CreateInMemoryContext();
        var selected = new MenuItem { Name = "Selected", Price = 1m, CategoryType = CategoryType.SPECIALS, IsFeaturedOnHome = true };
        var replacement = new MenuItem { Name = "Replacement", Price = 2m, CategoryType = CategoryType.SPECIALS };
        context.MenuItems.AddRange(selected, replacement);
        await context.SaveChangesAsync();
        var service = new MenuService(context);

        Assert.Equal(HomepageSpecialSelectionResult.Updated, await service.SetHomepageSpecialAsync(selected.Id, false));
        Assert.Equal(HomepageSpecialSelectionResult.Updated, await service.SetHomepageSpecialAsync(replacement.Id, true));
        Assert.False(selected.IsFeaturedOnHome);
        Assert.True(replacement.IsFeaturedOnHome);
    }

    [Fact]
    public async Task UpdateMenuItemAsync_PreservesHomepageSelection()
    {
        await using var context = CreateInMemoryContext();
        var selected = new MenuItem { Name = "Old name", Price = 1m, CategoryType = CategoryType.SPECIALS, IsFeaturedOnHome = true };
        context.MenuItems.Add(selected);
        await context.SaveChangesAsync();
        var service = new MenuService(context);

        var updated = await service.UpdateMenuItemAsync(new MenuItem
        {
            Id = selected.Id,
            Name = "New name",
            Price = 2m,
            Description = "Updated",
            CategoryType = CategoryType.DRINKS
        });

        Assert.True(updated);
        Assert.True(selected.IsFeaturedOnHome);
        Assert.Equal("New name", selected.Name);
    }
}
