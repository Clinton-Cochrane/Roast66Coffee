using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Security;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi.Tests;

public class AuditEventTests
{
    private static readonly DateTime NowUtc = new(2026, 9, 1, 17, 43, 12, DateTimeKind.Utc);

    [Fact]
    public async Task StatusAdvanceAndAuditCommitOnce_AndReplayAddsNothing()
    {
        await using var context = CreateContext();
        context.Orders.Add(new Order
        {
            TrackingToken = "audit-order-status-token-000000000000000000",
            CustomerName = "Audit Customer",
            OrderStatus = OrderStatus.Received,
            OrderItems = []
        });
        await context.SaveChangesAsync();
        var orderId = await context.Orders.Select(order => order.Id).SingleAsync();
        var service = new OrderService(
            context,
            new ConfigurationBuilder().Build(),
            new AuditEventFactory(context));
        var actor = new StaffActor("staff-42", "Mary");

        var advanced = await service.AdvanceStatusAsync(
            orderId,
            OrderStatus.Received,
            actor,
            NowUtc);
        var replayed = await service.AdvanceStatusAsync(
            orderId,
            OrderStatus.Received,
            actor,
            NowUtc.AddSeconds(1));

        Assert.Equal(OrderStatusAdvanceOutcome.Advanced, advanced.Outcome);
        Assert.Equal(OrderStatusAdvanceOutcome.Replayed, replayed.Outcome);
        var audit = Assert.Single(await context.AuditEvents.AsNoTracking().ToListAsync());
        Assert.Equal("staff-42", audit.ActorUserId);
        Assert.Equal("Mary", audit.ActorDisplayName);
        Assert.Equal("order.status.changed", audit.Action);
        Assert.Equal(orderId.ToString(), audit.EntityId);
        using var details = JsonDocument.Parse(audit.DetailsJson);
        Assert.Equal("Received", details.RootElement.GetProperty("from").GetString());
        Assert.Equal("Preparing", details.RootElement.GetProperty("to").GetString());
    }

    [Fact]
    public async Task AuditEventsCannotBeUpdatedOrDeleted()
    {
        await using var context = CreateContext();
        var audit = new AuditEventFactory(context).Add(
            new StaffActor("staff-1", "Owner"),
            "staff.created",
            "staff",
            "staff-2");
        await context.SaveChangesAsync();

        audit.ActorDisplayName = "Rewritten";
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"AuditEventTests-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
