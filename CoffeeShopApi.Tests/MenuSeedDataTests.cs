using CoffeeShopApi.Data;
using CoffeeShopApi.Migrations;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Text.Json;

namespace CoffeeShopApi.Tests;

public class MenuSeedDataTests
{
    private static readonly IReadOnlyDictionary<string, (string Description, decimal Price)> CurrentSignatureDrinks =
        new Dictionary<string, (string Description, decimal Price)>
        {
            ["RUSTEZ"] = ("Toasted hazelnut, white mocha", 4.25m),
            ["MRS.BROWNIE"] = ("Coconut, caramel, and chocolate drizzle", 4.25m),
            ["PHILTHY305"] = ("Vanilla, caramel drizzle", 4.25m),
            ["MIDNIGHT MCQUEEN"] = ("English toffee, red raspberry, white mocha", 4.25m),
            ["OFF-ROAD"] = ("Chocolate, cinnamon powder", 4.25m),
            ["RUSTY MATER"] = ("White mocha", 4.25m),
            ["MR.BROWNIE"] = ("Banana, chocolate drizzle", 4.25m),
            ["BURNOUT"] = ("Toasted marshmallows, chocolate drizzle, and cinnamon powder", 4.25m),
            ["BLACK CHEVY SS"] = ("Red raspberry, blue raspberry", 4.25m),
            ["DIESEL"] = ("Pomegranate, strawberry, vanilla", 4.25m),
            ["BLUE FLAME NITRO"] = ("Blue raspberry, coconut", 4.25m),
            ["CLUTCH STOP"] = ("Strawberry, white chocolate", 4.25m),
            ["SIDEWAYS RX"] = ("Vanilla, mango puree", 4.25m),
            ["SUKI 2 FAST"] = ("Strawberry puree", 4.25m),
            ["BLOWN HEAD GASKET"] = ("4 shots espresso; pick your flavor", 5.50m),
            ["CHECK ENGINE LIGHT"] = ("6 shots espresso; pick your flavor", 7.50m),
        };

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
    public void DefaultMenu_PassesBulkReplacementValidation()
    {
        var menuItems = SeedMenuItems.CreateDefaultMenuItems();

        MenuService.ValidateReplacement(menuItems);

        Assert.NotEmpty(menuItems);
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
    public void DbInitializer_IncludesCurrentSignatureDrinks()
    {
        using var context = CreateContext();

        DbInitializer.Initialize(context);

        AssertCurrentSignatureDrinks(context.MenuItems.ToList());
    }

    [Fact]
    public async Task AdminSeed_IncludesCurrentSignatureDrinks()
    {
        await using var context = CreateContext();

        await SeedMenuItems.SeedAsync(context);

        AssertCurrentSignatureDrinks(context.MenuItems.ToList());
    }

    [Theory]
    [InlineData("roast66/public/data/menu.json")]
    [InlineData("roast66-menu-2026-08-28.json")]
    public void JsonMenu_IncludesCurrentSignatureDrinks(string relativePath)
    {
        var path = FindRepositoryFile(relativePath.Split('/'));
        var menuItems = JsonSerializer.Deserialize<List<MenuItem>>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(menuItems);
        AssertCurrentSignatureDrinks(menuItems);
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

    private static void AssertCurrentSignatureDrinks(IEnumerable<MenuItem> menuItems)
    {
        var items = menuItems.ToList();

        foreach (var (name, expected) in CurrentSignatureDrinks)
        {
            var item = Assert.Single(items, item => item.Name == name);
            Assert.Equal(expected.Description, item.Description);
            Assert.Equal(expected.Price, item.Price);
            Assert.Equal(CategoryType.SPECIALS, item.CategoryType);
        }

        Assert.DoesNotContain(items, item => item.Name == "Mr. Brownie Shaken Espresso");
        Assert.DoesNotContain(items, item => item.Name == "Mrs. Brownie Latte");
        Assert.DoesNotContain(items, item => item.Name == "Blue Flame Nitro");
        Assert.DoesNotContain(items, item => item.Name == "Black SS Lemonade");
    }

    private static string FindRepositoryFile(params string[] relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativePath]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(relativePath)}");
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
