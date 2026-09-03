using CoffeeShopApi.Data;
using Npgsql;

namespace CoffeeShopApi.Tests;

public class PostgresConnectionStringTests
{
    [Fact]
    public void Build_EnforcesApplicationPoolLimit()
    {
        var result = PostgresConnectionString.Build(
            "Host=localhost;Database=coffeedb;Username=roast66;Password=test;Pooling=false;Maximum Pool Size=100");

        var parsed = new NpgsqlConnectionStringBuilder(result);

        Assert.True(parsed.Pooling);
        Assert.Equal(20, parsed.MaxPoolSize);
    }

    [Fact]
    public void Build_ConvertsPostgresUrlAndDecodesCredentials()
    {
        var result = PostgresConnectionString.Build(
            "postgresql://render_user:p%40ssword@roast66-db.internal:5432/coffeedb?sslmode=Require");

        var parsed = new NpgsqlConnectionStringBuilder(result);

        Assert.Equal("roast66-db.internal", parsed.Host);
        Assert.Equal(5432, parsed.Port);
        Assert.Equal("coffeedb", parsed.Database);
        Assert.Equal("render_user", parsed.Username);
        Assert.Equal("p@ssword", parsed.Password);
        Assert.Equal(SslMode.Require, parsed.SslMode);
        Assert.True(parsed.Pooling);
        Assert.Equal(20, parsed.MaxPoolSize);
    }

    [Fact]
    public void Build_RejectsMissingConnectionString()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            PostgresConnectionString.Build(null));

        Assert.Contains("DefaultConnection", error.Message, StringComparison.Ordinal);
    }
}
