using Npgsql;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class PostgresMigrationLockTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task MigrationLock_BlocksAConcurrentMigrationConnection()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "POSTGRES_INTEGRATION_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.False(
                string.Equals(
                    Environment.GetEnvironmentVariable("REQUIRE_POSTGRES_INTEGRATION_TESTS"),
                    "true",
                    StringComparison.OrdinalIgnoreCase),
                "POSTGRES_INTEGRATION_CONNECTION_STRING is required for this test run.");
            return;
        }

        await using var holder = new NpgsqlConnection(connectionString);
        await holder.OpenAsync();
        await ExecuteLockCommand(holder, "pg_advisory_lock");

        var waiterAcquiredLock = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var waiter = Task.Run(async () =>
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await ExecuteLockCommand(connection, "pg_advisory_lock");
            waiterAcquiredLock.SetResult();
            await ExecuteLockCommand(connection, "pg_advisory_unlock");
        });

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            Assert.False(waiterAcquiredLock.Task.IsCompleted);

            await ExecuteLockCommand(holder, "pg_advisory_unlock");
            await waiterAcquiredLock.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await waiter;
        }
        finally
        {
            if (!waiterAcquiredLock.Task.IsCompleted)
            {
                await ExecuteLockCommand(holder, "pg_advisory_unlock");
            }
        }
    }

    private static async Task ExecuteLockCommand(
        NpgsqlConnection connection,
        string functionName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {functionName}({Program.MigrationLockKey})";
        await command.ExecuteNonQueryAsync();
    }
}
