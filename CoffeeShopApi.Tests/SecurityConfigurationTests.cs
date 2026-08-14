using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CoffeeShopApi.Tests;

public class SecurityConfigurationTests
{
    [Theory]
    [InlineData(null, "strong-password", "01234567890123456789012345678901", "Admin:Username")]
    [InlineData("owner", null, "01234567890123456789012345678901", "Admin:Password")]
    [InlineData("owner", "strong-password", null, "Jwt:Key")]
    [InlineData("owner", "password", "01234567890123456789012345678901", "development default")]
    [InlineData("owner", "strong-password", "too-short", "at least 32")]
    public void ProductionRejectsMissingOrUnsafeAuthenticationSettings(
        string? username,
        string? password,
        string? jwtKey,
        string expectedMessage)
    {
        var configuration = Configuration(username, password, jwtKey);
        var error = Assert.Throws<InvalidOperationException>(() =>
            SecurityConfiguration.Validate(configuration, Environment("Production")));
        Assert.Contains(expectedMessage, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionAcceptsExplicitAuthenticationSettings()
    {
        var configuration = Configuration(
            "owner",
            "a-strong-production-password",
            "01234567890123456789012345678901");

        SecurityConfiguration.Validate(configuration, Environment("Production"));
    }

    [Fact]
    public void DevelopmentDoesNotRequireProductionSecrets()
    {
        SecurityConfiguration.Validate(new ConfigurationBuilder().Build(), Environment("Development"));
    }

    private static IConfiguration Configuration(string? username, string? password, string? jwtKey) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Admin:Username"] = username,
            ["Admin:Password"] = password,
            ["Jwt:Key"] = jwtKey
        }).Build();

    private static IHostEnvironment Environment(string name) => new TestEnvironment(name);

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "CoffeeShopApi.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
