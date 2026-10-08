using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using CoffeeShopApi.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class InPersonPaymentPostgresTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task CardSettlementAndFulfillment_CanCommitWithoutOverwritingEachOther()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_card_fulfillment");
        if (database == null) return;
        int orderId;
        await using (var seed = database.CreateContext())
        {
            await seed.Database.MigrateAsync();
            orderId = (await ManualPaymentServiceTests.AddOrderAsync(seed)).Id;
        }
        await using var cardContext = database.CreateContext();
        await using var statusContext = database.CreateContext();
        await cardContext.Orders.SingleAsync();
        await statusContext.Orders.SingleAsync();
        var card = InPersonPaymentServiceTests.CreateService(cardContext,
            new FakeInPersonGateway { NextStatus = GatewayPaymentStatus.Paid })
            .StartInPersonPaymentAsync(orderId, InPersonPaymentServiceTests.Actor);
        var fulfillment = new OrderService(statusContext, new ConfigurationBuilder().Build())
            .AdvanceStatusAsync(orderId, OrderStatus.Received);
        await Task.WhenAll(card, fulfillment);
        await using var verification = database.CreateContext();
        var order = await verification.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Preparing, order.OrderStatus);
        Assert.Equal(PaymentStatuses.Paid, (await card).Status);
        Assert.Equal((await card).PaidUtc, order.PaidUtc);
        Assert.Equal("fake-terminal", order.PaymentProvider);
        Assert.Single(await verification.Payments.ToListAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task ConcurrentStart_ReservesOnePaymentBeforeCallingTheProvider()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_in_person");
        if (database == null) return;
        int orderId;
        await using (var seed = database.CreateContext())
        {
            await seed.Database.MigrateAsync();
            orderId = (await ManualPaymentServiceTests.AddOrderAsync(seed)).Id;
        }
        var enteredGateway = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseGateway = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new FakeInPersonGateway
        {
            BeforeResult = async () => { enteredGateway.SetResult(); await releaseGateway.Task; }
        };
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var first = InPersonPaymentServiceTests.CreateService(firstContext, gateway)
            .StartInPersonPaymentAsync(orderId, InPersonPaymentServiceTests.Actor);
        await enteredGateway.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var repeat = await InPersonPaymentServiceTests.CreateService(secondContext, gateway)
                .StartInPersonPaymentAsync(orderId, InPersonPaymentServiceTests.Actor).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(PaymentStatuses.Pending, repeat.Status);
            Assert.Single(gateway.StartCalls);
            Assert.Equal(gateway.StartCalls.Single().Request.PaymentId, repeat.PaymentId);
        }
        finally { releaseGateway.TrySetResult(); }
        await first;
        await using var verification = database.CreateContext();
        Assert.Single(await verification.Payments.ToListAsync());
        Assert.Null((await verification.Orders.SingleAsync()).PaidUtc);
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task DatabaseConstraint_RejectsTwoPendingReservationsButAllowsANewAttemptAfterFailure()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_in_person_index");
        if (database == null) return;
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        var order = await ManualPaymentServiceTests.AddOrderAsync(context);
        var service = InPersonPaymentServiceTests.CreateService(context, new FakeInPersonGateway());
        await service.StartInPersonPaymentAsync(order.Id, InPersonPaymentServiceTests.Actor);
        var duplicate = new Payment
        {
            Provider = "another-provider", IsInPerson = true, OrderId = order.Id, Method = "card",
            Status = PaymentStatuses.Pending, IdempotencyKey = Guid.NewGuid().ToString("N"), PayloadJson = "{}"
        };
        context.Payments.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.Entry(duplicate).State = EntityState.Detached;
        var first = await context.Payments.SingleAsync();
        first.Status = PaymentStatuses.Failed;
        await context.SaveChangesAsync();
        await service.StartInPersonPaymentAsync(order.Id, InPersonPaymentServiceTests.Actor);
        Assert.Equal(2, await context.Payments.CountAsync());
    }
}
