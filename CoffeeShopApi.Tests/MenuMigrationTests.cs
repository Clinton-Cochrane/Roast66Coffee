using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CoffeeShopApi.Tests;

public class MenuMigrationTests
{
    [Fact]
    public void PreserveOrderHistoryMigration_UsesSnapshotsAndSetNullReferences()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=migration-script-only;Username=test;Password=test")
            .Options;
        using var context = new ApplicationDbContext(options);
        var migrator = context.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            "20260828000000_AddPaymentConcurrencyToken",
            "20260829000000_PreserveOrderHistoryFromMenuChanges");

        Assert.Contains("item_name", script);
        Assert.Contains("is_archived", script);
        Assert.Contains("ON DELETE SET NULL", script);
        Assert.Contains("Cannot backfill menu snapshots", script);
    }
}
