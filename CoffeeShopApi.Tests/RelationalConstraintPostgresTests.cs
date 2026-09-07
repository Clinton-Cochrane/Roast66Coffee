using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class RelationalConstraintPostgresTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(13)]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task OrderItemQuantityConstraint_RejectsTheEntireOrderGraph(int quantity)
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_orderitem_quantity");
        if (database == null)
        {
            return;
        }

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            context.Orders.Add(new Order
            {
                CustomerName = "Invalid order item quantity",
                TrackingToken = "invalid-orderitem-quantity-token-000000000",
                OrderItems =
                [
                    new OrderItem
                    {
                        Quantity = quantity,
                        ItemName = "Quantity validation",
                        ItemDescription = "Must be rejected by PostgreSQL",
                        AddOns = []
                    }
                ]
            });

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
                context.SaveChangesAsync());
            var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal(PostgresErrorCodes.CheckViolation, postgresException.SqlState);
        }

        await using var verification = database.CreateContext();
        Assert.Empty(await verification.Orders.ToListAsync());
        Assert.Empty(await verification.OrderItems.ToListAsync());
        Assert.Empty(await verification.Set<AddOn>().ToListAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(13)]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task AddOnQuantityConstraint_RejectsTheEntireOrderGraph(int quantity)
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_addon_quantity");
        if (database == null)
        {
            return;
        }

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            context.Orders.Add(new Order
            {
                CustomerName = "Invalid add-on quantity",
                TrackingToken = "invalid-addon-quantity-token-0000000000000",
                OrderItems =
                [
                    new OrderItem
                    {
                        Quantity = 1,
                        ItemName = "Quantity validation",
                        ItemDescription = "Valid parent line",
                        AddOns =
                        [
                            new AddOn
                            {
                                Quantity = quantity,
                                ItemName = "Invalid quantity add-on",
                                ItemDescription = "Must be rejected by PostgreSQL"
                            }
                        ]
                    }
                ]
            });

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
                context.SaveChangesAsync());
            var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal(PostgresErrorCodes.CheckViolation, postgresException.SqlState);
        }

        await using var verification = database.CreateContext();
        Assert.Empty(await verification.Orders.ToListAsync());
        Assert.Empty(await verification.OrderItems.ToListAsync());
        Assert.Empty(await verification.Set<AddOn>().ToListAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task InvalidMenuReference_RejectsTheEntireOrderGraph()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_fk_validation");
        if (database == null)
        {
            return;
        }

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            context.Orders.Add(new Order
            {
                CustomerName = "Malformed reference probe",
                TrackingToken = "malformed-reference-probe-token-00000000000",
                OrderItems =
                [
                    new OrderItem
                    {
                        MenuItemId = int.MaxValue,
                        Quantity = 1,
                        ItemName = "Nonexistent drink",
                        ItemDescription = "Must be rejected by PostgreSQL",
                        AddOns = []
                    }
                ]
            });

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
                context.SaveChangesAsync());
            var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgresException.SqlState);
        }

        await using var verification = database.CreateContext();
        Assert.Empty(await verification.Orders.ToListAsync());
        Assert.Empty(await verification.OrderItems.ToListAsync());
    }
}
