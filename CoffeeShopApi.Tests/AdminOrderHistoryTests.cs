using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi.Tests;

public class AdminOrderHistoryTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 31, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DefaultPage_PrioritizesActiveThenRetainedCompletedNewestFirst()
    {
        await using var context = CreateContext();
        context.Orders.AddRange(
            CreateOrder(1, OrderStatus.Completed, NowUtc.AddHours(-3), NowUtc.AddHours(-2)),
            CreateOrder(2, OrderStatus.Received, NowUtc.AddHours(-4)),
            CreateOrder(3, OrderStatus.Preparing, NowUtc.AddHours(-1)),
            CreateOrder(4, OrderStatus.Completed, NowUtc.AddHours(-2), NowUtc.AddHours(-1)),
            CreateOrder(5, OrderStatus.Completed, NowUtc.AddHours(-60), NowUtc.AddHours(-49)));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetOrderHistoryAsync(
            new AdminOrderHistoryRequest(),
            NowUtc);

        Assert.Equal([3, 2, 4, 1], result.Items.Select(order => order.Id));
        Assert.Equal(4, result.TotalItems);
        Assert.Equal(1, result.Page);
        Assert.Equal(OrderService.AdminOrderHistoryPageSize, result.PageSize);
        Assert.False(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public async Task CompletedRetention_IncludesExactFortyEightHourBoundaryOnly()
    {
        await using var context = CreateContext();
        context.Orders.AddRange(
            CreateOrder(1, OrderStatus.Completed, NowUtc.AddDays(-5), NowUtc.AddHours(-48)),
            CreateOrder(
                2,
                OrderStatus.Completed,
                NowUtc.AddDays(-5),
                NowUtc.AddHours(-48).AddTicks(-1)));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetOrderHistoryAsync(
            new AdminOrderHistoryRequest(),
            NowUtc);

        Assert.Equal(1, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Pages_AreFixedAtFiftyAndUseIdAsStableTieBreaker()
    {
        await using var context = CreateContext();
        context.Orders.AddRange(
            Enumerable.Range(1, 55)
                .Select(id => CreateOrder(id, OrderStatus.Received, NowUtc.AddMinutes(-5))));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var first = await service.GetOrderHistoryAsync(new AdminOrderHistoryRequest(), NowUtc);
        var second = await service.GetOrderHistoryAsync(
            new AdminOrderHistoryRequest { Page = 2 },
            NowUtc);

        Assert.Equal(50, first.Items.Count);
        Assert.Equal(55, first.Items[0].Id);
        Assert.Equal(6, first.Items[^1].Id);
        Assert.True(first.HasNextPage);
        Assert.Equal([5, 4, 3, 2, 1], second.Items.Select(order => order.Id));
        Assert.True(second.HasPreviousPage);
        Assert.False(second.HasNextPage);
        Assert.Equal(2, second.TotalPages);
    }

    [Fact]
    public async Task Search_MatchesOrderCustomerPhoneDrinkAndAddOnSnapshots()
    {
        await using var context = CreateContext();
        context.Orders.AddRange(
            CreateOrder(
                66,
                OrderStatus.Received,
                NowUtc.AddHours(-1),
                customerName: "Mia Thompson",
                customerPhone: "+1 (555) 867-5309",
                drinkName: "Superman",
                addOnName: "Blue Raspberry"),
            CreateOrder(
                67,
                OrderStatus.Received,
                NowUtc.AddHours(-2),
                customerName: "Alex",
                customerPhone: "555-000-0000",
                drinkName: "Bloop Bloop Bloop",
                addOnName: "Vanilla"));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await AssertSearchReturns(service, "66", 66);
        await AssertSearchReturns(service, "thompson", 66);
        await AssertSearchReturns(service, "5558675309", 66);
        await AssertSearchReturns(service, "superman", 66);
        await AssertSearchReturns(service, "raspberry", 66);
    }

    [Fact]
    public async Task StatusAndOrderDateFilters_ComposeWithRetention()
    {
        await using var context = CreateContext();
        context.Orders.AddRange(
            CreateOrder(1, OrderStatus.Received, NowUtc.AddDays(-2)),
            CreateOrder(2, OrderStatus.Preparing, NowUtc.AddHours(-2)),
            CreateOrder(3, OrderStatus.Completed, NowUtc.AddHours(-3), NowUtc.AddHours(-1)),
            CreateOrder(4, OrderStatus.Completed, NowUtc.AddDays(-10), NowUtc.AddHours(-49)));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetOrderHistoryAsync(
            new AdminOrderHistoryRequest
            {
                Status = "active",
                FromUtc = NowUtc.AddDays(-1),
                ToUtc = NowUtc
            },
            NowUtc);

        var order = Assert.Single(result.Items);
        Assert.Equal(2, order.Id);
    }

    [Fact]
    public async Task AdvanceStatus_CompletesOnceAndTreatsRepeatAsReplay()
    {
        await using var context = CreateContext();
        var order = CreateOrder(1, OrderStatus.ReadyForPickup, NowUtc.AddHours(-1));
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var advanced = await service.AdvanceStatusAsync(
            order.Id,
            OrderStatus.ReadyForPickup,
            NowUtc);

        Assert.Equal(OrderStatusAdvanceOutcome.Advanced, advanced.Outcome);
        Assert.Equal(OrderStatus.Completed, order.OrderStatus);
        Assert.Equal(NowUtc, order.CompletedUtc);

        var replayed = await service.AdvanceStatusAsync(
            order.Id,
            OrderStatus.ReadyForPickup,
            NowUtc.AddMinutes(1));

        Assert.Equal(OrderStatusAdvanceOutcome.Replayed, replayed.Outcome);
        Assert.Equal(OrderStatus.Completed, order.OrderStatus);
        Assert.Equal(NowUtc, order.CompletedUtc);
    }

    private static async Task AssertSearchReturns(OrderService service, string search, int expectedOrderId)
    {
        var result = await service.GetOrderHistoryAsync(
            new AdminOrderHistoryRequest { Search = search },
            NowUtc);
        var order = Assert.Single(result.Items);
        Assert.Equal(expectedOrderId, order.Id);
    }

    private static Order CreateOrder(
        int id,
        OrderStatus status,
        DateTime orderDate,
        DateTime? completedUtc = null,
        string customerName = "Customer",
        string customerPhone = "555-111-2222",
        string drinkName = "Coffee",
        string addOnName = "Vanilla") =>
        new()
        {
            Id = id,
            TrackingToken = $"{id:D43}",
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            OrderDate = orderDate,
            OrderStatus = status,
            CompletedUtc = completedUtc,
            OrderItems =
            [
                new OrderItem
                {
                    Id = id,
                    Quantity = 1,
                    ItemName = drinkName,
                    ItemDescription = "Snapshot",
                    AddOns =
                    [
                        new AddOn
                        {
                            Id = id,
                            Quantity = 1,
                            ItemName = addOnName,
                            ItemDescription = "Snapshot"
                        }
                    ]
                }
            ]
        };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"AdminOrderHistoryTests-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static OrderService CreateService(ApplicationDbContext context) =>
        new(context, new ConfigurationBuilder().Build());
}
