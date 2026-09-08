using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class OrderQuerySplittingPostgresTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task SubmissionReplayAndTracking_LoadCompleteGraphWithoutMultipleCollectionWarning()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_order_query_splitting");
        if (database == null)
        {
            return;
        }

        await using (var migrationContext = database.CreateContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .ConfigureWarnings(warnings =>
                warnings.Throw(RelationalEventId.MultipleCollectionIncludeWarning))
            .Options;
        await using var context = new ApplicationDbContext(options);

        var drink = new MenuItem
        {
            Name = "Split Query Latte",
            Description = "Regression test drink",
            Price = 5.25m,
            CategoryType = CategoryType.COFFEE
        };
        var addOn = new MenuItem
        {
            Name = "Split Query Vanilla",
            Description = "Regression test add-on",
            Price = 0.75m,
            CategoryType = CategoryType.FLAVORS
        };
        context.MenuItems.AddRange(drink, addOn);
        await context.SaveChangesAsync();

        var service = new OrderService(context, new ConfigurationBuilder().Build());
        const string idempotencyKey = "split-query-regression-key";
        var created = await service.SubmitOrderAsync(
            CreateOrder(drink.Id, addOn.Id),
            idempotencyKey);

        Assert.True(created.WasCreated);
        AssertCompleteOrder(created.Order);

        context.ChangeTracker.Clear();
        var reloaded = await service.GetOrderByIdAsync(created.Order.Id);
        AssertCompleteOrder(Assert.IsType<Order>(reloaded));

        context.ChangeTracker.Clear();
        var replayed = await service.SubmitOrderAsync(
            CreateOrder(drink.Id, addOn.Id),
            idempotencyKey);
        Assert.False(replayed.WasCreated);
        Assert.Equal(created.Order.Id, replayed.Order.Id);
        AssertCompleteOrder(replayed.Order);

        context.ChangeTracker.Clear();
        var tracked = await service.GetOrderByTrackingTokenAsync(created.Order.TrackingToken);
        AssertCompleteOrder(Assert.IsType<Order>(tracked));
        Assert.Equal(1, await context.Orders.CountAsync());
    }

    private static Order CreateOrder(int menuItemId, int addOnId) =>
        new()
        {
            CustomerName = "Split Query Customer",
            CustomerPhone = "555-0243",
            OrderItems =
            [
                new OrderItem
                {
                    MenuItemId = menuItemId,
                    Quantity = 2,
                    Notes = "Extra hot",
                    AddOns =
                    [
                        new AddOn
                        {
                            MenuItemId = addOnId,
                            Quantity = 1
                        }
                    ]
                }
            ]
        };

    private static void AssertCompleteOrder(Order order)
    {
        var item = Assert.Single(order.OrderItems!);
        Assert.Equal("Split Query Latte", item.ItemName);
        Assert.Equal(2, item.Quantity);
        Assert.Equal("Extra hot", item.Notes);
        Assert.Equal("Split Query Latte", item.MenuItem!.Name);

        var addOn = Assert.Single(item.AddOns!);
        Assert.Equal("Split Query Vanilla", addOn.ItemName);
        Assert.Equal(1, addOn.Quantity);
        Assert.Equal("Split Query Vanilla", addOn.MenuItem!.Name);
    }
}
