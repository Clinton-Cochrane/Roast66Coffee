namespace CoffeeShopApi.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresIntegrationCollection
{
    public const string Name = "PostgreSQL integration";
}
