// Program.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoffeeShopApi.Data;
using System;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

namespace CoffeeShopApi
{
    /// <summary>Entry point. Exposed for integration testing via WebApplicationFactory.</summary>
    public class Program
    {
        internal const long MigrationLockKey = 7266677001;

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .Enrich.FromLogContext()
                .CreateBootstrapLogger();

            try
            {
                Log.Information("Starting the application...");
                var host = CreateHostBuilder(args).Build();
                if (args.Length == 1 && string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyMigrations(host);
                    return;
                }
                if (args.Length == 1 && string.Equals(args[0], "initialize-local", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyMigrations(host, seedMenuIfEmpty: true);
                    return;
                }

                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void ApplyMigrations(IHost host, bool seedMenuIfEmpty = false)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                var env = services.GetRequiredService<IWebHostEnvironment>();

                if (env.IsEnvironment("Testing"))
                    throw new InvalidOperationException("The migrate command cannot run in Testing.");
                if (seedMenuIfEmpty && !env.IsDevelopment())
                    throw new InvalidOperationException(
                        "The initialize-local command can only run in Development.");

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

                Console.WriteLine("Database initialization successful.");
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(
                    "Database migration failed. Failure type: {FailureType}.",
                    ex.GetType().Name);
                Console.WriteLine($"Database migration failed ({ex.GetType().Name}).");

                // The deployment entrypoint must fail closed instead of serving against
                // a partially migrated or unreachable database.
                Environment.Exit(1);
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog((context, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
                    .MinimumLevel.Override(
                        "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware",
                        LogEventLevel.Fatal)
                    .MinimumLevel.Override(
                        "Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware",
                        LogEventLevel.Fatal)
                    .Enrich.FromLogContext()
                    .WriteTo.Console())
                .ConfigureAppConfiguration(SecurityConfiguration.ApplyDevelopmentDefaults)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    var port = Environment.GetEnvironmentVariable("PORT") ?? "80";
                    webBuilder.UseUrls($"http://0.0.0.0:{port}");
                });
    }
}
