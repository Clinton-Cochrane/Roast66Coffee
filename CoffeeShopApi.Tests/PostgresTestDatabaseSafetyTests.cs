namespace CoffeeShopApi.Tests;

public class PostgresTestDatabaseSafetyTests
{
    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void ValidateBaseConnectionString_AcceptsDedicatedLoopbackDatabase(string host)
    {
        var connectionString =
            $"Host={host};Port=5432;Database={PostgresTestDatabase.AdminDatabaseName};" +
            $"Username={PostgresTestDatabase.AdminUsername};Password=not-a-secret";

        var result = PostgresTestDatabase.ValidateBaseConnectionString(connectionString);

        Assert.Equal(host, result.Host);
        Assert.Equal(PostgresTestDatabase.AdminDatabaseName, result.Database);
        Assert.Equal(PostgresTestDatabase.AdminUsername, result.Username);
    }

    [Theory]
    [InlineData("db.example.com", "roast66_integration_admin", "roast66_test_admin")]
    [InlineData("localhost", "coffeedb", "roast66_test_admin")]
    [InlineData("localhost", "roast66_integration_admin", "postgres")]
    public void ValidateBaseConnectionString_RejectsConnectionsOutsideTheDisposableHarness(
        string host,
        string database,
        string username)
    {
        var connectionString =
            $"Host={host};Port=5432;Database={database};Username={username};Password=client-secret";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PostgresTestDatabase.ValidateBaseConnectionString(connectionString));

        Assert.Contains("disposable local PostgreSQL test container", exception.Message);
        Assert.DoesNotContain("client-secret", exception.Message);
    }
}
