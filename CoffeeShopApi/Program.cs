// Program.cs
using Microsoft.AspNetCore.Hosting;
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
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<ApplicationDbContext>();
                    context.Database.Migrate(); // Ensure the database is up-to-date
                    DbInitializer.Initialize(context);
                    Console.WriteLine("Database migration successful.");
                    Console.WriteLine("Database Migration Skipped for now");

                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred seeding the DB.");
                    Console.WriteLine($"Error during database migration: {ex.Message}");

                }
            }
            host.Run();
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
            webBuilder.UseUrls("http://0.0.0.0:80"); // Explicitly listen on port 80
        });
    }
}
