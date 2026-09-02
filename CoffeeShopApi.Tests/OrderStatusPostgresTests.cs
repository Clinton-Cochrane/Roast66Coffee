using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class OrderStatusPostgresTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task ConcurrentDuplicateAdvances_MoveExactlyOneStage()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        if (database == null)
        {
            return;
        }

        await using (var setup = database.CreateContext())
        {
            setup.Orders.Add(CreateOrder());
            await setup.SaveChangesAsync();
        }

        await AssertConcurrentAdvanceAsync(
            database,
            OrderStatus.Received,
            OrderStatus.Preparing);
        await AssertConcurrentAdvanceAsync(
            database,
            OrderStatus.Preparing,
            OrderStatus.ReadyForPickup);
        await AssertConcurrentAdvanceAsync(
            database,
            OrderStatus.ReadyForPickup,
            OrderStatus.Completed);

        await using var verification = database.CreateContext();
        var completed = await verification.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Completed, completed.OrderStatus);
        Assert.NotNull(completed.CompletedUtc);
    }

    private static async Task AssertConcurrentAdvanceAsync(
        PostgresTestDatabase database,
        OrderStatus expectedStatus,
        OrderStatus finalStatus)
    {
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstOrder = await firstContext.Orders.SingleAsync();
        var secondOrder = await secondContext.Orders.SingleAsync();
        Assert.Equal(expectedStatus, firstOrder.OrderStatus);
        Assert.Equal(expectedStatus, secondOrder.OrderStatus);

        var firstService = CreateService(firstContext);
        var secondService = CreateService(secondContext);
        var results = await Task.WhenAll(
            firstService.AdvanceStatusAsync(firstOrder.Id, expectedStatus),
            secondService.AdvanceStatusAsync(secondOrder.Id, expectedStatus));

        Assert.Single(results, result => result.Outcome == OrderStatusAdvanceOutcome.Advanced);
        Assert.Single(results, result => result.Outcome == OrderStatusAdvanceOutcome.Replayed);
        await using var verification = database.CreateContext();
        Assert.Equal(finalStatus, (await verification.Orders.SingleAsync()).OrderStatus);
    }

    private static async Task<PostgresTestDatabase?> CreateMigratedDatabaseAsync()
    {
        var database = await PostgresTestDatabase.CreateAsync("roast66_order_status");
        if (database == null)
        {
            return null;
        }

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        return database;
    }

    private static Order CreateOrder() =>
        new()
        {
            TrackingToken = "concurrent-order-status-token-0000000000000",
            CustomerName = "Concurrent Customer",
            OrderStatus = OrderStatus.Received,
            OrderItems =
            [
                new OrderItem
                {
                    Quantity = 1,
                    ItemName = "Coffee",
                    ItemDescription = "Snapshot",
                    AddOns = []
                }
            ]
        };

    private static OrderService CreateService(ApplicationDbContext context) =>
        new(context, new ConfigurationBuilder().Build());
}
