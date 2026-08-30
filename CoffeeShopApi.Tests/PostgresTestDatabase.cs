using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoffeeShopApi.Tests;

internal sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly string _baseConnectionString;

    private PostgresTestDatabase(
        string baseConnectionString,
        string databaseName,
        string connectionString)
    {
        _baseConnectionString = baseConnectionString;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string DatabaseName { get; }
    public string ConnectionString { get; }

    public static async Task<PostgresTestDatabase?> CreateAsync(string namePrefix)
    {
        var baseConnectionString = Environment.GetEnvironmentVariable(
            "POSTGRES_INTEGRATION_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            Assert.False(
                string.Equals(
                    Environment.GetEnvironmentVariable("REQUIRE_POSTGRES_INTEGRATION_TESTS"),
                    "true",
                    StringComparison.OrdinalIgnoreCase),
                "POSTGRES_INTEGRATION_CONNECTION_STRING is required for this test run.");
            return null;
        }

        var databaseName = $"{namePrefix}_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };

        await using (var connection = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                        CREATE ROLE anon NOLOGIN;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        CREATE ROLE authenticated NOLOGIN;
                    END IF;
                END $$;
                """;
            await command.ExecuteNonQueryAsync();

            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        var databaseBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };
        return new PostgresTestDatabase(
            baseConnectionString,
            databaseName,
            databaseBuilder.ConnectionString);
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        var builder = new NpgsqlConnectionStringBuilder(_baseConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
