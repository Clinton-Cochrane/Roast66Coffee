using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class ForeignKeyIndexPostgresTests
{
    private const string PreviousMigration = "20260901153615_AddStaffIdentityAndAudit";
    private const string CorrectiveMigration = "20260902000000_RestoreMenuForeignKeyIndexes";

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task LatestMigration_MenuForeignKeyIndexesMatchTheEfModel()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_fk_indexes_fresh");
        if (database == null)
        {
            return;
        }

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();

        var expected = GetExpectedContracts(context);
        var actual = await GetActualContractsAsync(database.ConnectionString, expected);

        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task CorrectiveMigration_HandlesExistingIndexesAndRestoresMissingIndexes()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_fk_indexes_upgrade");
        if (database == null)
        {
            return;
        }

        await using var context = database.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        var expected = GetExpectedContracts(context);

        await CreateCanonicalIndexesAsync(database.ConnectionString);
        await migrator.MigrateAsync(CorrectiveMigration);
        var existingIndexUpgrade = await GetActualContractsAsync(database.ConnectionString, expected);
        Assert.Equal(expected, existingIndexUpgrade);

        await migrator.MigrateAsync(PreviousMigration);
        await DropMenuForeignKeyIndexesAsync(database.ConnectionString);
        await migrator.MigrateAsync(CorrectiveMigration);
        var missingIndexUpgrade = await GetActualContractsAsync(database.ConnectionString, expected);

        Assert.Equal(existingIndexUpgrade, missingIndexUpgrade);
    }

    private static IReadOnlyList<IndexContract> GetExpectedContracts(ApplicationDbContext context)
    {
        return
        [
            GetExpectedContract<AddOn>(context, nameof(AddOn.MenuItemId)),
            GetExpectedContract<OrderItem>(context, nameof(OrderItem.MenuItemId))
        ];
    }

    private static IndexContract GetExpectedContract<TEntity>(
        ApplicationDbContext context,
        string propertyName)
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not in the EF model.");
        var property = entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException($"{propertyName} is not mapped.");
        var index = entityType.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1 && candidate.Properties[0] == property);
        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped to a table.");
        var schema = entityType.GetSchema() ?? "public";
        var table = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());

        return new IndexContract(
            schema,
            tableName,
            index.GetDatabaseName(table)
                ?? throw new InvalidOperationException($"{propertyName} has no database index name."),
            property.GetColumnName(table)
                ?? throw new InvalidOperationException($"{propertyName} has no database column name."),
            IsUnique: index.IsUnique,
            IsValid: true,
            IsReady: true,
            Predicate: index.GetFilter());
    }

    private static async Task<IReadOnlyList<IndexContract>> GetActualContractsAsync(
        string connectionString,
        IReadOnlyList<IndexContract> expected)
    {
        var actual = new List<IndexContract>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var contract in expected)
        {
            await using var aliases = connection.CreateCommand();
            aliases.CommandText =
                """
                SELECT indexname
                FROM pg_indexes
                WHERE schemaname = @schema
                  AND tablename = @table
                  AND lower(indexname) = lower(@indexName)
                ORDER BY indexname;
                """;
            aliases.Parameters.AddWithValue("schema", contract.Schema);
            aliases.Parameters.AddWithValue("table", contract.Table);
            aliases.Parameters.AddWithValue("indexName", contract.Name);
            var matchingNames = new List<string>();
            await using (var aliasReader = await aliases.ExecuteReaderAsync())
            {
                while (await aliasReader.ReadAsync())
                {
                    matchingNames.Add(aliasReader.GetString(0));
                }
            }
            Assert.Equal([contract.Name], matchingNames);

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    indexed.indisunique,
                    indexed.indisvalid,
                    indexed.indisready,
                    pg_get_expr(indexed.indpred, indexed.indrelid),
                    ARRAY(
                        SELECT attribute.attname
                        FROM unnest(indexed.indkey) WITH ORDINALITY AS key_column(attnum, position)
                        JOIN pg_attribute AS attribute
                          ON attribute.attrelid = indexed.indrelid
                         AND attribute.attnum = key_column.attnum
                        WHERE key_column.position <= indexed.indnkeyatts
                        ORDER BY key_column.position)
                FROM pg_index AS indexed
                JOIN pg_class AS table_class ON table_class.oid = indexed.indrelid
                JOIN pg_namespace AS schema_namespace ON schema_namespace.oid = table_class.relnamespace
                JOIN pg_class AS index_class ON index_class.oid = indexed.indexrelid
                WHERE schema_namespace.nspname = @schema
                  AND table_class.relname = @table
                  AND index_class.relname = @indexName;
                """;
            command.Parameters.AddWithValue("schema", contract.Schema);
            command.Parameters.AddWithValue("table", contract.Table);
            command.Parameters.AddWithValue("indexName", contract.Name);

            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), $"Index {contract.Name} does not exist.");
            var columns = reader.GetFieldValue<string[]>(4);
            actual.Add(new IndexContract(
                contract.Schema,
                contract.Table,
                contract.Name,
                Assert.Single(columns),
                reader.GetBoolean(0),
                reader.GetBoolean(1),
                reader.GetBoolean(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
            Assert.False(await reader.ReadAsync(), $"Index {contract.Name} is not unique in the catalog.");
        }

        return actual;
    }

    private static async Task CreateCanonicalIndexesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE INDEX IF NOT EXISTS "IX_addons_menuitemid" ON public.addons (menuitemid);
            CREATE INDEX IF NOT EXISTS "IX_orderitems_menuitemid" ON public.orderitems (menuitemid);
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropMenuForeignKeyIndexesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DROP INDEX IF EXISTS public."IX_addons_menuitemid";
            DROP INDEX IF EXISTS public.ix_addons_menuitemid;
            DROP INDEX IF EXISTS public."IX_orderitems_menuitemid";
            DROP INDEX IF EXISTS public.ix_orderitems_menuitemid;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private sealed record IndexContract(
        string Schema,
        string Table,
        string Name,
        string Column,
        bool IsUnique,
        bool IsValid,
        bool IsReady,
        string? Predicate);
}
