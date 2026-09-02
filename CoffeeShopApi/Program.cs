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
using Microsoft.AspNetCore.Identity;
using CoffeeShopApi.Models;
using CoffeeShopApi.Security;
using CoffeeShopApi.Services;

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
                if (args.Length == 1 && string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyMigrations(host);
                    return;
                }
                if (args.Length == 1 && string.Equals(args[0], "initialize-local", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyMigrations(host, seedMenuIfEmpty: true);
                    InitializeOwner(host, allowDevelopmentDefaults: true, recover: false);
                    return;
                }
                if (args.Length == 1 && string.Equals(args[0], "initialize-owner", StringComparison.OrdinalIgnoreCase))
                {
                    InitializeOwner(host, allowDevelopmentDefaults: false, recover: false);
                    return;
                }
                if (args.Length == 1 && string.Equals(args[0], "recover-owner", StringComparison.OrdinalIgnoreCase))
                {
                    InitializeOwner(host, allowDevelopmentDefaults: false, recover: true);
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

        private static void InitializeOwner(IHost host, bool allowDevelopmentDefaults, bool recover)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var configuration = services.GetRequiredService<IConfiguration>();
            var environment = services.GetRequiredService<IWebHostEnvironment>();
            var username = configuration["Bootstrap:Username"];
            var displayName = configuration["Bootstrap:DisplayName"];
            var password = configuration["Bootstrap:Password"];
            if (allowDevelopmentDefaults && environment.IsDevelopment())
            {
                username ??= configuration["Admin:Username"];
                displayName ??= "Local Owner";
                password ??= configuration["Admin:Password"];
            }
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "Set Bootstrap__Username, Bootstrap__DisplayName, and Bootstrap__Password.");
            }

            var context = services.GetRequiredService<ApplicationDbContext>();
            using var transaction = context.Database.IsRelational()
                ? context.Database.BeginTransaction()
                : null;
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var roleName in new[] { StaffRoles.Admin, StaffRoles.Owner })
            {
                if (!roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
                {
                    EnsureIdentitySuccess(
                        roleManager.CreateAsync(new IdentityRole(roleName)).GetAwaiter().GetResult());
                }
            }

            var userManager = services.GetRequiredService<UserManager<StaffUser>>();
            var user = userManager.FindByNameAsync(username.Trim()).GetAwaiter().GetResult();
            if (user == null)
            {
                if (recover)
                {
                    throw new InvalidOperationException("The requested Owner account does not exist.");
                }
                user = new StaffUser
                {
                    UserName = username.Trim(),
                    DisplayName = displayName.Trim(),
                    IsActive = true
                };
                EnsureIdentitySuccess(userManager.CreateAsync(user, password).GetAwaiter().GetResult());
            }
            else if (recover)
            {
                user.DisplayName = displayName.Trim();
                user.IsActive = true;
                var resetToken = userManager.GeneratePasswordResetTokenAsync(user).GetAwaiter().GetResult();
                EnsureIdentitySuccess(
                    userManager.ResetPasswordAsync(user, resetToken, password).GetAwaiter().GetResult());
            }

            foreach (var roleName in new[] { StaffRoles.Admin, StaffRoles.Owner })
            {
                if (!userManager.IsInRoleAsync(user, roleName).GetAwaiter().GetResult())
                {
                    EnsureIdentitySuccess(
                        userManager.AddToRoleAsync(user, roleName).GetAwaiter().GetResult());
                }
            }
            EnsureIdentitySuccess(userManager.UpdateSecurityStampAsync(user).GetAwaiter().GetResult());
            services.GetRequiredService<AuditEventFactory>().Add(
                new StaffActor(null, recover ? "System owner recovery" : "System owner initialization"),
                recover ? "staff.owner_recovered" : "staff.owner_initialized",
                "staff",
                user.Id,
                new { user.DisplayName, Username = user.UserName });
            context.SaveChanges();
            transaction?.Commit();
            Console.WriteLine(recover ? "Owner recovery successful." : "Owner initialization successful.");
        }

        private static void EnsureIdentitySuccess(IdentityResult result)
        {
            if (result.Succeeded) return;
            throw new InvalidOperationException(
                "Identity operation failed: " +
                string.Join(" ", result.Errors.Select(error => error.Description)));
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

                new DatabaseMigrationRunner(context).Run(seedMenuIfEmpty);

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
