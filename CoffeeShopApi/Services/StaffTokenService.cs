using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoffeeShopApi.Models;
using CoffeeShopApi.Security;
using Microsoft.IdentityModel.Tokens;

namespace CoffeeShopApi.Services;

public sealed class StaffTokenService(IConfiguration configuration)
{
    private readonly IConfiguration _configuration = configuration;

    public string Create(StaffUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(StaffClaimTypes.Username, user.UserName ?? string.Empty),
            new(StaffClaimTypes.SecurityStamp, user.SecurityStamp ?? string.Empty)
        };
        claims.AddRange(roles.Select(role => new Claim(StaffClaimTypes.Role, role)));
        return Create(claims);
    }

    public string CreateLegacy()
    {
        return Create(
        [
            new Claim(JwtRegisteredClaimNames.Sub, "legacy-shared-admin"),
            new Claim(JwtRegisteredClaimNames.Name, "Legacy shared admin"),
            new Claim(StaffClaimTypes.Username, "legacy-shared-admin"),
            new Claim(StaffClaimTypes.Role, StaffRoles.Admin),
            new Claim(StaffClaimTypes.LegacyShared, "true")
        ]);
    }

    private string Create(IEnumerable<Claim> claims)
    {
        var jwtKey = _configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
        {
            throw new InvalidOperationException("JWT key is invalid or too short. It must be at least 32 characters long.");
        }

        var expiryHours = int.TryParse(_configuration["Jwt:TokenExpiryInHours"], out var parsedHours) && parsedHours > 0
            ? parsedHours
            : 1;
        var token = new JwtSecurityToken(
            JwtTokenSettings.GetIssuer(_configuration),
            JwtTokenSettings.GetAudience(_configuration),
            claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
