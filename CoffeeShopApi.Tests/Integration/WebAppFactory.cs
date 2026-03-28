using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi.Tests.Integration;

/// <summary>
/// Test web application factory. Uses in-memory database when ASPNETCORE_ENVIRONMENT=Testing.
/// </summary>
public class WebAppFactory : WebApplicationFactory<Program>
{
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
                ["Jwt:Audience"] = "Roast66Coffee"
            });
        });
    }
}
