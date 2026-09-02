using System.Data.Common;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class AdminOrderHistoryPostgresTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task Migration_BackfillsCompletionTimeAndCreatesHistoryIndex()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_order_history_migration");
        if (database == null)
        {
            return;
        }

        var orderDate = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        await using (var context = database.CreateContext())
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260830000000_AddOrderIdempotency");
        }

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO orders (
                    customername, orderdate, orderstatus,
                    customernotificationoptin, trackingtoken)
                VALUES ('Completed before migration', @orderDate, 3, false, repeat('h', 43));
                """;
            insert.Parameters.AddWithValue("orderDate", orderDate);
            await insert.ExecuteNonQueryAsync();
        }

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            var order = await context.Orders.SingleAsync();
            Assert.Equal(orderDate, order.CompletedUtc);
        }

        await using var verification = new NpgsqlConnection(database.ConnectionString);
        await verification.OpenAsync();
        await using var indexCommand = verification.CreateCommand();
        indexCommand.CommandText =
            "SELECT count(*) FROM pg_indexes WHERE indexname = 'ix_orders_admin_history'";
        Assert.Equal(1L, await indexCommand.ExecuteScalarAsync());
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task RealisticDrinkSearch_UsesTwoQueriesAndReturnsAtMostFiftyDtos()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_order_history_query");
        if (database == null)
        {
            return;
        }

        await using (var migrationContext = database.CreateContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var counter = new ReaderCommandCounter();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .AddInterceptors(counter)
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Orders.AddRange(Enumerable.Range(1, 120).Select(number => new Order
        {
            TrackingToken = $"{number:D43}",
            CustomerName = $"Volume Customer {number}",
            OrderDate = DateTime.UtcNow.AddMinutes(-number),
            OrderItems =
            [
                new OrderItem
                {
                    Quantity = 1,
                    ItemName = "Superman",
                    ItemDescription = "Historical snapshot",
                    AddOns =
                    [
                        new AddOn
                        {
                            Quantity = 1,
                            ItemName = "Blue Raspberry",
                            ItemDescription = "Historical snapshot"
                        }
                    ]
                }
            ]
        }));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        counter.Reset();

        var service = new OrderService(context, new ConfigurationBuilder().Build());
        var result = await service.GetOrderHistoryAsync(
            new AdminOrderHistoryRequest { Search = "superman" },
            DateTime.UtcNow);

        Assert.Equal(120, result.TotalItems);
        Assert.Equal(50, result.Items.Count);
        Assert.All(result.Items, order => Assert.Equal("Superman", Assert.Single(order.OrderItems).ItemName));
        Assert.Equal(2, counter.ReaderCommandCount);
    }

    private sealed class ReaderCommandCounter : DbCommandInterceptor
    {
        private int _readerCommandCount;
        public int ReaderCommandCount => Volatile.Read(ref _readerCommandCount);

        public void Reset() => Volatile.Write(ref _readerCommandCount, 0);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _readerCommandCount);
            return ValueTask.FromResult(result);
        }
    }
}
