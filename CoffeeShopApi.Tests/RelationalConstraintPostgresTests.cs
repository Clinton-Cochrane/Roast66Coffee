using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class RelationalConstraintPostgresTests
{
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
