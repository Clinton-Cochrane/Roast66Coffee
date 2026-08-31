using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi.Tests;

public class OrderIdempotencyTests
{
    [Fact]
    public void RequestFingerprint_NormalizesCustomerFormattingAndCollectionOrder()
    {
        var first = new Order
        {
            CustomerName = "  Ada   Lovelace ",
            CustomerPhone = "(555) 123-4567",
            CustomerEmail = " ADA@EXAMPLE.COM ",
            CustomerNotificationOptIn = true,
            OrderItems =
            [
                new OrderItem
                {
                    MenuItemId = 2,
                    Quantity = 1,
                    Notes = "  light   ice ",
                    AddOns =
                    [
                        new AddOn { MenuItemId = 4, Quantity = 2 },
                        new AddOn { MenuItemId = 3, Quantity = 1 }
                    ]
                },
                new OrderItem { MenuItemId = 1, Quantity = 2 }
            ]
        };
        var equivalent = new Order
        {
            CustomerName = "ada lovelace",
            CustomerPhone = "5551234567",
            CustomerEmail = "ada@example.com",
            CustomerNotificationOptIn = true,
            OrderItems =
            [
                new OrderItem { MenuItemId = 1, Quantity = 2 },
                new OrderItem
                {
                    MenuItemId = 2,
                    Quantity = 1,
                    Notes = "light ice",
                    AddOns =
                    [
                        new AddOn { MenuItemId = 3, Quantity = 1 },
                        new AddOn { MenuItemId = 4, Quantity = 2 }
                    ]
                }
            ]
        };

        Assert.Equal(
            OrderService.ComputeRequestFingerprint(first),
            OrderService.ComputeRequestFingerprint(equivalent));
    }

    [Fact]
    public void RequestFingerprint_DistinguishesDifferentCustomersAndContent()
    {
        var first = CreateOrder("Customer One", 1);
        var differentCustomer = CreateOrder("Customer Two", 1);
        var differentQuantity = CreateOrder("Customer One", 2);

        var fingerprint = OrderService.ComputeRequestFingerprint(first);
        Assert.NotEqual(fingerprint, OrderService.ComputeRequestFingerprint(differentCustomer));
        Assert.NotEqual(fingerprint, OrderService.ComputeRequestFingerprint(differentQuantity));
    }

    [Fact]
    public async Task SubmitOrder_KeyDoesNotExpireAndReplaysBeforeMenuValidation()
    {
        await using var context = CreateContext();
        var menuItem = new MenuItem
        {
            Name = "Durable Key Latte",
            Description = "Available for the winning request",
            Price = 5m,
            CategoryType = CategoryType.COFFEE
        };
        context.MenuItems.Add(menuItem);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var key = Guid.NewGuid().ToString("N");

        var first = await service.SubmitOrderAsync(CreateOrder("Durable Customer", 1, menuItem.Id), key);
        first.Order.OrderDate = DateTime.UtcNow.AddYears(-1);
        menuItem.IsArchived = true;
        await context.SaveChangesAsync();

        var replay = await service.SubmitOrderAsync(CreateOrder("Durable Customer", 1, menuItem.Id), key);

        Assert.True(first.WasCreated);
        Assert.False(replay.WasCreated);
        Assert.Equal(first.Order.Id, replay.Order.Id);
        Assert.Single(await context.Orders.ToListAsync());
    }

    [Fact]
    public async Task SubmitOrder_SameKeyWithDifferentCustomer_ThrowsConflict()
    {
        await using var context = CreateContext();
        var menuItem = new MenuItem
        {
            Name = "Conflict Latte",
            Price = 4m,
            CategoryType = CategoryType.COFFEE
        };
        context.MenuItems.Add(menuItem);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var key = Guid.NewGuid().ToString("N");
        await service.SubmitOrderAsync(CreateOrder("First Customer", 1, menuItem.Id), key);

        await Assert.ThrowsAsync<IdempotencyKeyConflictException>(() =>
            service.SubmitOrderAsync(CreateOrder("Second Customer", 1, menuItem.Id), key));
    }

    private static Order CreateOrder(string customerName, int quantity, int menuItemId = 1) =>
        new()
        {
            CustomerName = customerName,
            CustomerPhone = "5551234567",
            OrderItems = [new OrderItem { MenuItemId = menuItemId, Quantity = quantity }]
        };

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
            new ConfigurationBuilder().AddInMemoryCollection().Build());
}
