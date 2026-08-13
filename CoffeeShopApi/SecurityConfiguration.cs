using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CoffeeShopApi;

public static class SecurityConfiguration
{
    public const string DevelopmentUsername = "admin";
    public const string DevelopmentPassword = "password";
    public const string DevelopmentJwtKey = "DevelopmentOnlyJwtSigningKey_ChangeMe_32Chars";

    public static void ApplyDevelopmentDefaults(
        HostBuilderContext context,
        IConfigurationBuilder configuration)
    {
        if (!context.HostingEnvironment.IsDevelopment() &&
            !context.HostingEnvironment.IsEnvironment("Testing"))
        {
            return;
        }

        var current = configuration.Build();
        var defaults = new Dictionary<string, string?>();
        AddDefault(defaults, current, "Admin:Username", DevelopmentUsername);
        AddDefault(defaults, current, "Admin:Password", DevelopmentPassword);
        AddDefault(defaults, current, "Jwt:Key", DevelopmentJwtKey);
        AddDefault(defaults, current, "Jwt:Issuer", "Roast66Coffee");
        AddDefault(defaults, current, "Jwt:Audience", "Roast66Coffee");
        (current as IDisposable)?.Dispose();
        configuration.AddInMemoryCollection(defaults);
    }

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        Require(configuration, "Admin:Username", "Admin__Username");
        var password = Require(configuration, "Admin:Password", "Admin__Password");
        var jwtKey = Require(configuration, "Jwt:Key", "Jwt__Key");

        if (string.Equals(password, DevelopmentPassword, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Admin:Password cannot use the development default in Production. Set Admin__Password.");
        }

        if (jwtKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key must be at least 32 characters in Production. Set Jwt__Key.");
        }
    }

    private static string Require(IConfiguration configuration, string key, string environmentKey)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} is required in Production. Set {environmentKey}.");
        }

        return value;
    }

    private static void AddDefault(
        IDictionary<string, string?> defaults,
        IConfiguration configuration,
        string key,
        string value)
    {
        if (string.IsNullOrWhiteSpace(configuration[key])) defaults[key] = value;
    }
}
