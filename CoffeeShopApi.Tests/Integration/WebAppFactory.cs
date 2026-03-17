using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoffeeShopApi.Tests.Integration;

/// <summary>
/// Test web application factory. Uses in-memory database when ASPNETCORE_ENVIRONMENT=Testing.
/// </summary>
public class WebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
