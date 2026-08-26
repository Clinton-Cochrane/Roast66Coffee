using CoffeeShopApi.Data;
using CoffeeShopApi.Migrations;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CoffeeShopApi.Tests;

public class MenuSeedDataTests
{
    [Fact]
    public void DbInitializer_AddsOneWatermelonShot()
    {
        using var context = CreateContext();

        DbInitializer.Initialize(context);

        Assert.Single(context.MenuItems.Where(item => item.Name == "Watermelon Shot"));
        AssertFlavorDescriptionsDoNotContainShot(context);
    }

    [Fact]
    public async Task AdminSeed_AddsOneWatermelonShot()
    {
        await using var context = CreateContext();

        await SeedMenuItems.SeedAsync(context);

        Assert.Single(context.MenuItems.Where(item => item.Name == "Watermelon Shot"));
        AssertFlavorDescriptionsDoNotContainShot(context);
    }

    [Fact]
    public async Task LocalSeed_SeedsOnlyWhenMenuIsEmpty()
    {
        await using var context = CreateContext();

        Assert.True(await SeedMenuItems.SeedIfEmptyAsync(context));
        var seededCount = await context.MenuItems.CountAsync();

        Assert.False(await SeedMenuItems.SeedIfEmptyAsync(context));
        Assert.Equal(seededCount, await context.MenuItems.CountAsync());
        Assert.Single(context.MenuItems.Where(item => item.Name == "Watermelon Shot"));
    }

    [Fact]
    public void DuplicateCleanupMigration_ReassignsReferencesBeforeDeletingDuplicates()
    {
        var operations = new TestableRemoveDuplicateWatermelonShot().BuildOperations();
        var operation = Assert.IsType<SqlOperation>(Assert.Single(operations));

        Assert.Contains("UPDATE addons", operation.Sql);
        Assert.Contains("UPDATE orderitems", operation.Sql);
        Assert.Contains("regexp_replace", operation.Sql);
        Assert.Contains("DELETE FROM menuitems", operation.Sql);
        Assert.Contains("MIN(id)", operation.Sql);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void AssertFlavorDescriptionsDoNotContainShot(ApplicationDbContext context)
    {
        var flavors = context.MenuItems
            .Where(item => item.CategoryType == CategoryType.FLAVORS)
            .ToList();

        Assert.NotEmpty(flavors);
        Assert.All(
            flavors,
            flavor => Assert.DoesNotContain(
                "shot",
                flavor.Description,
                StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TestableRemoveDuplicateWatermelonShot : RemoveDuplicateWatermelonShot
    {
        public IReadOnlyList<MigrationOperation> BuildOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            base.Up(builder);
            return builder.Operations;
        }
    }
}
