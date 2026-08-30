using System.Collections.Generic;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoffeeShopApi.Tests.Integration;

/// <summary>
/// Test web application factory. Uses in-memory database when ASPNETCORE_ENVIRONMENT=Testing.
/// </summary>
public class WebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"IntegrationTestDb-{Guid.NewGuid():N}";

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
        return host;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Ensure CORS startup succeeds even if the host process sets a non-Testing environment.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // CI and fresh clones have no gitignored appsettings.json; login requires Jwt:Key (32+ chars).
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedOrigins"] = "http://localhost",
                ["Jwt:Key"] = "IntegrationTestSigningKey_NotForProduction_Min32Chars___",
                ["Jwt:Issuer"] = "Roast66Coffee",
                ["Jwt:Audience"] = "Roast66Coffee",
                ["Testing:DatabaseName"] = _databaseName
            });
        });
    }
}
