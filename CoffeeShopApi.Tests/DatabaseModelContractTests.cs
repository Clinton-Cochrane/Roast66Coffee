using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CoffeeShopApi.Tests;

public class DatabaseModelContractTests
{
    [Fact]
    public void EfModelAndLatestMigrationSnapshot_AreInSync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_contract;Username=unused;Password=unused")
            .Options;
        using var context = new ApplicationDbContext(options);

        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var snapshot = migrationsAssembly.ModelSnapshot
            ?? throw new InvalidOperationException("The EF migration snapshot is missing.");
        var modelDiffer = context.GetService<IMigrationsModelDiffer>();
        var runtimeInitializer = context.GetService<IModelRuntimeInitializer>();
        var snapshotModel = runtimeInitializer.Initialize(snapshot.Model);
        var currentModel = context.GetService<IDesignTimeModel>().Model;
        var operations = modelDiffer.GetDifferences(
            snapshotModel.GetRelationalModel(),
            currentModel.GetRelationalModel());

        Assert.True(
            operations.Count == 0,
            "The EF model has changes that are not represented by the latest migration snapshot: " +
            string.Join(", ", operations.Select(operation => operation.GetType().Name)));
    }
}
