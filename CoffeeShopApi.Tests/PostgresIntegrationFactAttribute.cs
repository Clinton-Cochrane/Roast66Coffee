namespace CoffeeShopApi.Tests;

internal sealed class PostgresIntegrationFactAttribute : FactAttribute
{
    public PostgresIntegrationFactAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "POSTGRES_INTEGRATION_CONNECTION_STRING");
        var isRequired = string.Equals(
            Environment.GetEnvironmentVariable("REQUIRE_POSTGRES_INTEGRATION_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(connectionString) && !isRequired)
        {
            Skip = "Run through scripts/ci/with-postgres.sh to enable PostgreSQL integration tests.";
        }
    }
}
