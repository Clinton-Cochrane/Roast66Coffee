using System.ComponentModel.DataAnnotations;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class HomepageSpecialPostgresTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task SelectionLimitAndReplacement_AreEnforcedAgainstPostgres()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        if (database == null)
        {
            return;
        }

        int[] itemIds;
        await using (var context = database.CreateContext())
        {
            context.MenuItems.AddRange(
                CreateMenuItem("One"),
                CreateMenuItem("Two"),
                CreateMenuItem("Three"),
                CreateMenuItem("Four"));
            await context.SaveChangesAsync();
            itemIds = await context.MenuItems.OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync();

            var service = new MenuService(context);
            foreach (var itemId in itemIds[..3])
            {
                Assert.Equal(
                    HomepageSpecialSelectionResult.Updated,
                    await service.SetHomepageSpecialAsync(itemId, true));
            }

            Assert.Equal(
                HomepageSpecialSelectionResult.LimitReached,
                await service.SetHomepageSpecialAsync(itemIds[3], true));
        }

        await AssertSelectedIdsAsync(database, itemIds[..3]);

        await using (var context = database.CreateContext())
        {
            var service = new MenuService(context);
            Assert.Equal(
                HomepageSpecialSelectionResult.Updated,
                await service.SetHomepageSpecialAsync(itemIds[0], false));
            Assert.Equal(
                HomepageSpecialSelectionResult.Updated,
                await service.SetHomepageSpecialAsync(itemIds[3], true));
        }

        await AssertSelectedIdsAsync(database, [itemIds[1], itemIds[2], itemIds[3]]);
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task BulkReplacementAndSeed_PreserveTheSameLimitAgainstPostgres()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        if (database == null)
        {
            return;
        }

        await using (var context = database.CreateContext())
        {
            var service = new MenuService(context);
            await service.BulkReplaceAsync(
            [
                CreateMenuItem("Bulk one", isFeatured: true),
                CreateMenuItem("Bulk two", isFeatured: true),
                CreateMenuItem("Bulk three", isFeatured: true),
                CreateMenuItem("Bulk four")
            ]);

            await Assert.ThrowsAsync<ValidationException>(() => service.BulkReplaceAsync(
            [
                CreateMenuItem("Invalid one", isFeatured: true),
                CreateMenuItem("Invalid two", isFeatured: true),
                CreateMenuItem("Invalid three", isFeatured: true),
                CreateMenuItem("Invalid four", isFeatured: true)
            ]));
        }

        await using (var verification = database.CreateContext())
        {
            Assert.Equal(4, await verification.MenuItems.CountAsync());
            Assert.Equal(3, await verification.MenuItems.CountAsync(item => item.IsFeaturedOnHome));
            Assert.False(await verification.MenuItems.AnyAsync(item => item.Name.StartsWith("Invalid")));
        }

        await using (var context = database.CreateContext())
        {
            await SeedMenuItems.SeedAsync(context);
        }

        await using (var verification = database.CreateContext())
        {
            Assert.Equal(3, await verification.MenuItems.CountAsync(item => item.IsFeaturedOnHome));
        }
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task ConcurrentWriters_CannotBothClaimTheFinalSlot()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        if (database == null)
        {
            return;
        }

        int firstCandidateId;
        int secondCandidateId;
        await using (var setup = database.CreateContext())
        {
            setup.MenuItems.AddRange(
                CreateMenuItem("Selected one", isFeatured: true),
                CreateMenuItem("Selected two", isFeatured: true),
                CreateMenuItem("First candidate"),
                CreateMenuItem("Second candidate"));
            await setup.SaveChangesAsync();
            firstCandidateId = await setup.MenuItems
                .Where(item => item.Name == "First candidate")
                .Select(item => item.Id)
                .SingleAsync();
            secondCandidateId = await setup.MenuItems
                .Where(item => item.Name == "Second candidate")
                .Select(item => item.Id)
                .SingleAsync();
        }

        await using var firstContext = database.CreateContext();
        await using var firstTransaction = await firstContext.Database.BeginTransactionAsync();
        var firstService = new MenuService(firstContext);
        Assert.Equal(
            HomepageSpecialSelectionResult.Updated,
            await firstService.SetHomepageSpecialAsync(firstCandidateId, true));

        await using var secondContext = database.CreateContext();
        var secondService = new MenuService(secondContext);
        var secondSelection = secondService.SetHomepageSpecialAsync(secondCandidateId, true);

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        Assert.False(secondSelection.IsCompleted);
        await firstTransaction.CommitAsync();

        Assert.Equal(
            HomepageSpecialSelectionResult.LimitReached,
            await secondSelection.WaitAsync(TimeSpan.FromSeconds(5)));
        await using var verification = database.CreateContext();
        Assert.Equal(3, await verification.MenuItems.CountAsync(item => item.IsFeaturedOnHome));
        Assert.True((await verification.MenuItems.FindAsync(firstCandidateId))!.IsFeaturedOnHome);
        Assert.False((await verification.MenuItems.FindAsync(secondCandidateId))!.IsFeaturedOnHome);
    }

    private static async Task<PostgresTestDatabase?> CreateMigratedDatabaseAsync()
    {
        var database = await PostgresTestDatabase.CreateAsync("roast66_homepage_specials");
        if (database == null)
        {
            return null;
        }

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        return database;
    }

    private static MenuItem CreateMenuItem(string name, bool isFeatured = false) =>
        new()
        {
            Name = name,
            Description = $"{name} description",
            Price = 4m,
            CategoryType = CategoryType.SPECIALS,
            IsFeaturedOnHome = isFeatured
        };

    private static async Task AssertSelectedIdsAsync(
        PostgresTestDatabase database,
        IReadOnlyCollection<int> expectedIds)
    {
        var selectedIds = await GetSelectedIdsAsync(database);
        Assert.Equal(expectedIds.OrderBy(id => id), selectedIds);
    }

    private static async Task<int[]> GetSelectedIdsAsync(PostgresTestDatabase database)
    {
        await using var context = database.CreateContext();
        return await context.MenuItems
            .Where(item => item.IsFeaturedOnHome)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToArrayAsync();
    }
}
