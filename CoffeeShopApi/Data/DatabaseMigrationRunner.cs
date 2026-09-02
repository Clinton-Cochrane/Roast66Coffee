using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Data;

/// <summary>Serializes schema migration and optional local seed work per PostgreSQL database.</summary>
internal sealed class DatabaseMigrationRunner(ApplicationDbContext context)
{
    internal const long MigrationLockKey = 7266677001;

    public void Run(bool seedMenuIfEmpty)
    {
        Console.WriteLine("Acquiring the database migration lock...");
        context.Database.OpenConnection();
        context.Database.ExecuteSqlRaw($"SELECT pg_advisory_lock({MigrationLockKey})");
        try
        {
            Console.WriteLine("Applying database migrations...");
            context.Database.Migrate();
            if (seedMenuIfEmpty)
            {
                Console.WriteLine("Ensuring the local menu snapshot is seeded...");
                var seeded = SeedMenuItems.SeedIfEmptyAsync(context)
                    .GetAwaiter()
                    .GetResult();
                Console.WriteLine(
                    seeded
                        ? "Local menu snapshot seeded."
                        : "Local menu already exists; leaving it unchanged.");
            }
        }
        finally
        {
            context.Database.ExecuteSqlRaw($"SELECT pg_advisory_unlock({MigrationLockKey})");
            context.Database.CloseConnection();
        }
    }
}
