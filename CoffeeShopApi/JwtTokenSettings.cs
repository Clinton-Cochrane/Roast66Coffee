using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi;

/// <summary>
/// Resolves JWT issuer and audience from configuration, with production-safe defaults when
/// <c>Jwt__Issuer</c> / <c>Jwt__Audience</c> are unset (e.g. Render service created without Blueprint env sync).
/// Issuer and audience must match between token creation and <see cref="Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions"/>.
/// </summary>
internal static class JwtTokenSettings
{
    public const string DefaultIssuerAndAudience = "Roast66Coffee";

    public static string GetIssuer(IConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration["Jwt:Issuer"])
            ? DefaultIssuerAndAudience
            : configuration["Jwt:Issuer"]!;

    public static string GetAudience(IConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration["Jwt:Audience"])
            ? DefaultIssuerAndAudience
            : configuration["Jwt:Audience"]!;

}
