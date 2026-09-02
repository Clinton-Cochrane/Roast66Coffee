using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoffeeShopApi.Tests;

internal sealed class PostgresTestDatabase : IAsyncDisposable
{
    internal const string AdminDatabaseName = "roast66_integration_admin";
    internal const string AdminUsername = "roast66_test_admin";

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
        var adminBuilder = ValidateBaseConnectionString(baseConnectionString);
        adminBuilder.Pooling = false;

        await using (var connection = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT current_setting('roast66.test_run_id', true)";
            var actualRunId = (string?)await command.ExecuteScalarAsync();
            var expectedRunId = Environment.GetEnvironmentVariable(
                "POSTGRES_INTEGRATION_RUN_ID");
            if (string.IsNullOrWhiteSpace(expectedRunId) ||
                !string.Equals(actualRunId, expectedRunId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "PostgreSQL integration tests require the active disposable test run identity.");
            }

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

    internal static NpgsqlConnectionStringBuilder ValidateBaseConnectionString(
        string connectionString)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "PostgreSQL integration tests require the disposable local PostgreSQL test container.",
                exception);
        }

        var isLoopback = builder.Host is "localhost" or "127.0.0.1" or "::1";
        if (!isLoopback ||
            !string.Equals(builder.Database, AdminDatabaseName, StringComparison.Ordinal) ||
            !string.Equals(builder.Username, AdminUsername, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PostgreSQL integration tests require the disposable local PostgreSQL test container.");
        }

        return builder;
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
        var builder = ValidateBaseConnectionString(_baseConnectionString);
        builder.Pooling = false;
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
