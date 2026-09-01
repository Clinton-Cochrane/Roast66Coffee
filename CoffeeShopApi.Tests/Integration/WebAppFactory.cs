using System.Collections.Generic;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using CoffeeShopApi.Security;

namespace CoffeeShopApi.Tests.Integration;

/// <summary>
/// Test web application factory. Uses in-memory database when ASPNETCORE_ENVIRONMENT=Testing.
/// </summary>
public class WebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"IntegrationTestDb-{Guid.NewGuid():N}";
    private readonly int? _loginPermitLimit;
    private readonly int? _orderPermitLimit;

    public WebAppFactory()
    {
    }

    internal WebAppFactory(int? loginPermitLimit = null, int? orderPermitLimit = null)
    {
        _loginPermitLimit = loginPermitLimit;
        _orderPermitLimit = orderPermitLimit;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!context.MenuItems.Any())
        {
            context.MenuItems.Add(new MenuItem
            {
                Id = 1,
                Name = "Integration test coffee",
                Description = "Menu item shared by API integration tests",
                Price = 4m,
                CategoryType = CategoryType.COFFEE
            });
            context.SaveChanges();
        }
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in new[] { StaffRoles.Admin, StaffRoles.Owner })
        {
            if (!roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
            {
                roleManager.CreateAsync(new IdentityRole(roleName)).GetAwaiter().GetResult();
            }
        }
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        EnsureStaffUser(
            userManager,
            "integration-owner",
            "Integration Owner",
            [StaffRoles.Admin, StaffRoles.Owner]);
        EnsureStaffUser(
            userManager,
            "integration-admin",
            "Integration Admin",
            [StaffRoles.Admin]);
        return host;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Ensure CORS startup succeeds even if the host process sets a non-Testing environment.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // CI and fresh clones have no gitignored appsettings.json; login requires Jwt:Key (32+ chars).
            var settings = new Dictionary<string, string?>
            {
                ["AllowedOrigins"] = "http://localhost",
                ["Admin:Username"] = "admin",
                ["Admin:Password"] = "password",
                ["Jwt:Key"] = "IntegrationTestSigningKey_NotForProduction_Min32Chars___",
                ["Jwt:Issuer"] = "Roast66Coffee",
                ["Jwt:Audience"] = "Roast66Coffee",
                ["Jwt:TokenExpiryInHours"] = "8",
                ["Authentication:LegacySharedLoginEnabled"] = "true",
                ["Testing:DatabaseName"] = _databaseName
            };
            if (_loginPermitLimit.HasValue)
            {
                settings["Testing:RateLimits:LoginPermitLimit"] =
                    _loginPermitLimit.Value.ToString();
            }
            if (_orderPermitLimit.HasValue)
            {
                settings["Testing:RateLimits:OrderPermitLimit"] =
                    _orderPermitLimit.Value.ToString();
            }
            config.AddInMemoryCollection(settings);
        });
    }

    private static void EnsureStaffUser(
        UserManager<StaffUser> userManager,
        string username,
        string displayName,
        string[] roles)
    {
        var existing = userManager.FindByNameAsync(username).GetAwaiter().GetResult();
        if (existing != null) return;
        var user = new StaffUser
        {
            UserName = username,
            DisplayName = displayName,
            IsActive = true
        };
        var created = userManager.CreateAsync(user, "IntegrationPassword1!").GetAwaiter().GetResult();
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", created.Errors.Select(error => error.Description)));
        }
        var assigned = userManager.AddToRolesAsync(user, roles).GetAwaiter().GetResult();
        if (!assigned.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", assigned.Errors.Select(error => error.Description)));
        }
    }
}
