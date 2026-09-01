using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi.Tests;

public class OrderStatusTransitionTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 31, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OrderStatusConcurrencyMigration_AddsTokenWithoutRewritingOrders()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=migration-script-only;Username=test;Password=test")
            .Options;
        using var context = new ApplicationDbContext(options);
        var migrator = context.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            "20260831000000_AddAdminOrderHistoryPagination",
            "20260831010000_AddOrderStatusConcurrencyToken");

        Assert.Contains("ADD statusconcurrencytoken uuid NOT NULL", script);
        Assert.DoesNotContain("DROP TABLE orders", script);
    }

    [Theory]
    [InlineData(OrderStatus.Received, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Preparing, OrderStatus.ReadyForPickup)]
    [InlineData(OrderStatus.ReadyForPickup, OrderStatus.Completed)]
    public async Task AdvanceStatus_AllValidTransitionsAdvanceExactlyOneStage(
        OrderStatus current,
        OrderStatus expectedNext)
    {
        await using var context = CreateContext();
        var order = CreateOrder(current);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var result = await CreateService(context).AdvanceStatusAsync(order.Id, current, NowUtc);

        Assert.Equal(OrderStatusAdvanceOutcome.Advanced, result.Outcome);
        Assert.Equal(expectedNext, order.OrderStatus);
        Assert.Equal(
            expectedNext == OrderStatus.Completed ? NowUtc : null,
            order.CompletedUtc);
    }

    [Theory]
    [InlineData(OrderStatus.Preparing, OrderStatus.Received)]
    [InlineData(OrderStatus.ReadyForPickup, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Completed, OrderStatus.ReadyForPickup)]
    public async Task AdvanceStatus_RepeatedRequestIsIdempotent(
        OrderStatus current,
        OrderStatus repeatedExpectedStatus)
    {
        await using var context = CreateContext();
        DateTime? completedUtc = current == OrderStatus.Completed ? NowUtc.AddMinutes(-1) : null;
        var order = CreateOrder(current, completedUtc);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var result = await CreateService(context).AdvanceStatusAsync(
            order.Id,
            repeatedExpectedStatus,
            NowUtc);

        Assert.Equal(OrderStatusAdvanceOutcome.Replayed, result.Outcome);
        Assert.Equal(current, order.OrderStatus);
        Assert.Equal(completedUtc, order.CompletedUtc);
    }

    [Fact]
    public async Task AdvanceStatus_CompletedExpectedCompletedIsTerminalAndUnchanged()
    {
        await using var context = CreateContext();
        var completedUtc = NowUtc.AddMinutes(-5);
        var order = CreateOrder(OrderStatus.Completed, completedUtc);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var result = await CreateService(context).AdvanceStatusAsync(
            order.Id,
            OrderStatus.Completed,
            NowUtc);

        Assert.Equal(OrderStatusAdvanceOutcome.Terminal, result.Outcome);
        Assert.Equal(OrderStatus.Completed, order.OrderStatus);
        Assert.Equal(completedUtc, order.CompletedUtc);
    }

    [Theory]
    [InlineData(OrderStatus.Received, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Received, OrderStatus.ReadyForPickup)]
    [InlineData(OrderStatus.Preparing, OrderStatus.ReadyForPickup)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Completed)]
    [InlineData(OrderStatus.ReadyForPickup, OrderStatus.Received)]
    [InlineData(OrderStatus.ReadyForPickup, OrderStatus.Completed)]
    [InlineData(OrderStatus.Completed, OrderStatus.Received)]
    [InlineData(OrderStatus.Completed, OrderStatus.Preparing)]
    public async Task AdvanceStatus_MismatchedExpectedStateReturnsConflictWithoutMutation(
        OrderStatus current,
        OrderStatus expected)
    {
        await using var context = CreateContext();
        DateTime? completedUtc = current == OrderStatus.Completed ? NowUtc.AddMinutes(-5) : null;
        var order = CreateOrder(current, completedUtc);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var result = await CreateService(context).AdvanceStatusAsync(order.Id, expected, NowUtc);

        Assert.Equal(OrderStatusAdvanceOutcome.Conflict, result.Outcome);
        Assert.Equal(current, order.OrderStatus);
        Assert.Equal(completedUtc, order.CompletedUtc);
    }

    [Fact]
    public async Task AdvanceStatus_UndefinedExpectedStatusIsRejected()
    {
        await using var context = CreateContext();
        var order = CreateOrder(OrderStatus.Received);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var result = await CreateService(context).AdvanceStatusAsync(
            order.Id,
            (OrderStatus)99,
            NowUtc);

        Assert.Equal(OrderStatusAdvanceOutcome.InvalidExpectedStatus, result.Outcome);
        Assert.Equal(OrderStatus.Received, order.OrderStatus);
    }

    [Fact]
    public async Task AdvanceStatus_UndefinedStoredStatusIsRejectedWithoutCoercion()
    {
        await using var context = CreateContext();
        var order = CreateOrder((OrderStatus)99);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var result = await CreateService(context).AdvanceStatusAsync(
            order.Id,
            OrderStatus.Received,
            NowUtc);

        Assert.Equal(OrderStatusAdvanceOutcome.InvalidCurrentStatus, result.Outcome);
        Assert.Equal((OrderStatus)99, order.OrderStatus);
    }

    [Fact]
    public async Task GeneralOrderUpdate_CannotBypassTerminalStatusRule()
    {
        await using var context = CreateContext();
        var completedUtc = NowUtc.AddMinutes(-5);
        var completed = CreateOrder(OrderStatus.Completed, completedUtc);
        context.Orders.Add(completed);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var replacement = CreateOrder(OrderStatus.Received);
        replacement.CustomerName = "Updated Customer";

        Assert.True(await CreateService(context).UpdateOrderAsync(replacement));

        context.ChangeTracker.Clear();
        var persisted = await context.Orders.SingleAsync();
        Assert.Equal("Updated Customer", persisted.CustomerName);
        Assert.Equal(OrderStatus.Completed, persisted.OrderStatus);
        Assert.Equal(completedUtc, persisted.CompletedUtc);
    }

    private static Order CreateOrder(OrderStatus status, DateTime? completedUtc = null) =>
        new()
        {
            Id = 1,
            TrackingToken = "0000000000000000000000000000000000000000001",
            CustomerName = "Transition Customer",
            CustomerPhone = "555-111-2222",
            OrderStatus = status,
            CompletedUtc = completedUtc,
            OrderItems =
            [
                new OrderItem
                {
                    Id = 1,
                    Quantity = 1,
                    ItemName = "Coffee",
                    ItemDescription = "Snapshot",
                    AddOns = []
                }
            ]
        };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"OrderStatusTransitionTests-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static OrderService CreateService(ApplicationDbContext context) =>
        new(context, new ConfigurationBuilder().Build());
}
