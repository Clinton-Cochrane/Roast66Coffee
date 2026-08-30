using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi.Tests;

public class OrderSnapshotTests
{
    [Fact]
    public async Task CreateOrder_StampsServerOwnedSnapshots()
    {
        await using var context = CreateContext();
        var menuItem = new MenuItem
        {
            Name = "Snapshot Latte",
            Description = "The description at order time",
            Price = 5m,
            PromotionType = PromotionType.Dollar,
            PromotionValue = 1m,
            CategoryType = CategoryType.COFFEE
        };
        context.MenuItems.Add(menuItem);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var order = new Order
        {
            CustomerName = "Snapshot Customer",
            OrderItems =
            [
                new OrderItem
                {
                    MenuItemId = menuItem.Id,
                    Quantity = 1,
                    ItemName = "Client supplied name",
                    UnitPrice = 0.01m
                }
            ]
        };

        var created = await service.CreateOrderAsync(order);

        var line = Assert.Single(created.OrderItems);
        Assert.Equal("Snapshot Latte", line.ItemName);
        Assert.Equal("The description at order time", line.ItemDescription);
        Assert.Equal(CategoryType.COFFEE, line.ItemCategoryType);
        Assert.Equal(4m, line.UnitPrice);
    }

    [Fact]
    public async Task CreateOrder_RejectsArchivedMenuItem()
    {
        await using var context = CreateContext();
        var menuItem = new MenuItem
        {
            Name = "Archived Latte",
            Price = 5m,
            CategoryType = CategoryType.COFFEE,
            IsArchived = true
        };
        context.MenuItems.Add(menuItem);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var order = new Order
        {
            CustomerName = "Snapshot Customer",
            OrderItems = [new OrderItem { MenuItemId = menuItem.Id, Quantity = 1 }]
        };

        await Assert.ThrowsAsync<UnavailableMenuItemsException>(
            () => service.CreateOrderAsync(order));
        Assert.Empty(context.Orders);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static OrderService CreateService(ApplicationDbContext context) =>
        new(
            context,
            new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build());
}
