using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class MenuHistoryPostgresTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task MigrationAndMenuMaintenance_PreserveHistoryAndRollback()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_menu_history");
        if (database == null)
        {
            return;
        }

        var databaseConnectionString = database.ConnectionString;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(databaseConnectionString)
            .Options;

        await using (var context = new ApplicationDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260828000000_AddPaymentConcurrencyToken");
        }

        await SeedPreMigrationOrderAsync(databaseConnectionString);

        await using (var context = new ApplicationDbContext(options))
        {
            await context.Database.MigrateAsync();
        }

        await AssertSnapshotsAndSetNullDeleteAsync(databaseConnectionString);

        await using (var context = new ApplicationDbContext(options))
        {
            context.MenuItems.Add(new MenuItem
            {
                Name = "Menu before failed import",
                Description = "Must survive rollback",
                Price = 3m,
                CategoryType = CategoryType.COFFEE
            });
            await context.SaveChangesAsync();

            var service = new MenuService(context);
            await Assert.ThrowsAsync<ValidationException>(() =>
                service.BulkReplaceAsync(
                [
                    new MenuItem
                    {
                        Name = null!,
                        Description = "Invalid replacement",
                        Price = 4m,
                        CategoryType = CategoryType.COFFEE
                    }
                ]));
        }

        await using (var verification = new ApplicationDbContext(options))
        {
            Assert.True(await verification.MenuItems.AnyAsync(
                item => item.Name == "Menu before failed import"));
        }

        int seededCount;
        await using (var context = new ApplicationDbContext(options))
        {
            await SeedMenuItems.SeedAsync(context);
            seededCount = await context.MenuItems.CountAsync();
            await SeedMenuItems.SeedAsync(context);
            Assert.Equal(seededCount, await context.MenuItems.CountAsync());
            context.MenuItems.Add(new MenuItem
            {
                Name = "Menu before failed seed",
                Description = "Must survive seed rollback",
                Price = 5m,
                CategoryType = CategoryType.COFFEE
            });
            await context.SaveChangesAsync();
        }

        await using (var verification = new ApplicationDbContext(options))
        {
            Assert.True(seededCount > 1);
            Assert.Equal(1, await verification.Orders.CountAsync());
            Assert.Equal(1, await verification.OrderItems.CountAsync());
            Assert.Equal(1, await verification.Set<AddOn>().CountAsync());
            var retainedLine = await verification.OrderItems.SingleAsync();
            Assert.Null(retainedLine.MenuItemId);
            Assert.Equal("Historical Latte", retainedLine.ItemName);
            Assert.Equal(4.50m, retainedLine.UnitPrice);
        }

        await AddSeedFailureTriggerAsync(databaseConnectionString);
        await using (var context = new ApplicationDbContext(options))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                SeedMenuItems.SeedAsync(context));
        }

        await using (var verification = new ApplicationDbContext(options))
        {
            Assert.True(await verification.MenuItems.AnyAsync(
                item => item.Name == "Menu before failed seed"));
            Assert.Equal(seededCount + 1, await verification.MenuItems.CountAsync());
            Assert.Equal(1, await verification.OrderItems.CountAsync());
            Assert.Equal(1, await verification.Set<AddOn>().CountAsync());
        }
    }

    private static async Task SeedPreMigrationOrderAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            WITH menu AS (
                INSERT INTO menuitems (name, price, description, "CategoryType", is_featured_on_home)
                VALUES ('Historical Latte', 4.50, 'Original description', 0, false)
                RETURNING id
            ), historical_order AS (
                INSERT INTO orders (
                    customername,
                    orderdate,
                    orderstatus,
                    customernotificationoptin,
                    trackingtoken)
                VALUES ('Migration Customer', now(), 0, false, repeat('t', 43))
                RETURNING id
            ), line AS (
                INSERT INTO orderitems (orderid, menuitemid, quantity, unit_price)
                SELECT historical_order.id, menu.id, 1, 4.50
                FROM historical_order, menu
                RETURNING "Id", menuitemid
            )
            INSERT INTO addons (menuitemid, quantity, orderitemid, unit_price)
            SELECT line.menuitemid, 2, line."Id", 4.50
            FROM line;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertSnapshotsAndSetNullDeleteAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var snapshotCommand = connection.CreateCommand())
        {
            snapshotCommand.CommandText =
                """
                SELECT item_name, item_description, item_category_type
                FROM orderitems;
                """;
            await using var reader = await snapshotCommand.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Historical Latte", reader.GetString(0));
            Assert.Equal("Original description", reader.GetString(1));
            Assert.Equal((int)CategoryType.COFFEE, reader.GetInt32(2));
        }

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText = "DELETE FROM menuitems WHERE name = 'Historical Latte';";
            Assert.Equal(1, await deleteCommand.ExecuteNonQueryAsync());
        }

        await using var preservationCommand = connection.CreateCommand();
        preservationCommand.CommandText =
            """
            SELECT
                (SELECT count(*) FROM orderitems),
                (SELECT count(*) FROM addons),
                (SELECT count(*) FROM orderitems WHERE menuitemid IS NULL),
                (SELECT count(*) FROM addons WHERE menuitemid IS NULL);
            """;
        await using var preservationReader = await preservationCommand.ExecuteReaderAsync();
        Assert.True(await preservationReader.ReadAsync());
        Assert.Equal(1L, preservationReader.GetInt64(0));
        Assert.Equal(1L, preservationReader.GetInt64(1));
        Assert.Equal(1L, preservationReader.GetInt64(2));
        Assert.Equal(1L, preservationReader.GetInt64(3));
    }

    private static async Task AddSeedFailureTriggerAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE FUNCTION reject_seed_espresso() RETURNS trigger AS $$
            BEGIN
                IF NEW.name = 'Espresso' THEN
                    RAISE EXCEPTION 'Forced seed failure';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER reject_seed_espresso
            BEFORE INSERT ON menuitems
            FOR EACH ROW EXECUTE FUNCTION reject_seed_espresso();
            """;
        await command.ExecuteNonQueryAsync();
    }
}
