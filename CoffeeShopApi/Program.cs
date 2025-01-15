// Program.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoffeeShopApi.Data;
using System;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting the application...");

            var host = CreateHostBuilder(args).Build();

            ApplyMigrations(host);

            host.Run();
        }

        private static void ApplyMigrations(IHost host)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                Console.WriteLine("Applying database migrations...");
                context.Database.Migrate(); // Ensure the database is up-to-date

                // Optional: Seed the database if needed
                DbInitializer.Initialize(context);

                Console.WriteLine("Database migration and initialization successful.");
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred during database migration.");
                Console.WriteLine($"Error during database migration: {ex.Message}");

                // Optionally, stop the application if migration fails
                Environment.Exit(1);
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Load environment variables
                    config.AddEnvironmentVariables();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://0.0.0.0:8080"); // Explicitly listen on port 8080
                });
    }
}
