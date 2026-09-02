using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class PostgresMigrationLockTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task MigrationRunner_WaitsForTheAdvisoryLockBeforeApplyingMigrations()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_migration_lock");
        if (database == null)
        {
            return;
        }

        await using var holder = new NpgsqlConnection(database.ConnectionString);
        await holder.OpenAsync();
        await ExecuteLockCommand(holder, "pg_advisory_lock");

        await using var migrationContext = database.CreateContext();
        var migration = Task.Run(() =>
            new DatabaseMigrationRunner(migrationContext).Run(seedMenuIfEmpty: false));

        try
        {
            await WaitForAdvisoryLockWaiterAsync(database.ConnectionString);
            Assert.False(migration.IsCompleted);

            await ExecuteLockCommand(holder, "pg_advisory_unlock");
            await migration.WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            if (!migration.IsCompleted)
            {
                await ExecuteLockCommand(holder, "pg_advisory_unlock");
            }
        }

        await using var verification = database.CreateContext();
        Assert.Empty(await verification.Database.GetPendingMigrationsAsync());
    }

    private static async Task WaitForAdvisoryLockWaiterAsync(string connectionString)
    {
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync();
        await using var command = observer.CreateCommand();
        command.CommandText =
            "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted";

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if ((long)(await command.ExecuteScalarAsync())! > 0)
            {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        Assert.Fail("The application migration runner did not wait on the advisory lock.");
    }

    private static async Task ExecuteLockCommand(
        NpgsqlConnection connection,
        string functionName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT {functionName}({DatabaseMigrationRunner.MigrationLockKey})";
        await command.ExecuteNonQueryAsync();
    }
}
