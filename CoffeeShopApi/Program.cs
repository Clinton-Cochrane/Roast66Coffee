// Program.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoffeeShopApi.Data;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace CoffeeShopApi
{
    /// <summary>Entry point. Exposed for integration testing via WebApplicationFactory.</summary>
    public class Program
    {
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
                ApplyMigrations(host);
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

        private static void ApplyMigrations(IHost host)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                var env = services.GetRequiredService<IWebHostEnvironment>();

                if (env.IsEnvironment("Testing"))
                {
                    context.Database.EnsureCreated();
                    DbInitializer.Initialize(context);
                }
                else
                {
                    Console.WriteLine("Applying database migrations...");
                    context.Database.Migrate();
                    DbInitializer.Initialize(context);
                }

                Console.WriteLine("Database initialization successful.");
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
                .UseSerilog((context, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console())
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Probe appsettings + env the same way the host does. Docker/Render images often have no
                    // gitignored appsettings.json; Jwt__Key is easy to miss in the dashboard — inject a key so the app boots.
                    var probe = new ConfigurationBuilder()
                        .SetBasePath(context.HostingEnvironment.ContentRootPath)
                        .AddJsonFile("appsettings.json", optional: true)
                        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                        .AddEnvironmentVariables()
                        .Build();

                    if (context.HostingEnvironment.IsEnvironment("Testing"))
                    {
                        (probe as IDisposable)?.Dispose();
                        return;
                    }

                    var jwtKey = probe["Jwt:Key"];
                    (probe as IDisposable)?.Dispose();

                    if (!string.IsNullOrEmpty(jwtKey) && jwtKey.Length >= 32)
                        return;

                    var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Key"] = generated
                    });
                    Console.WriteLine(
                        "WARN: Jwt:Key was missing or shorter than 32 characters. " +
                        "Using a random signing key for this process. " +
                        "Set Jwt__Key (at least 32 characters) in the environment for stable keys across restarts.");
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    var port = Environment.GetEnvironmentVariable("PORT") ?? "80";
                    webBuilder.UseUrls($"http://0.0.0.0:{port}");
                });
    }
}
