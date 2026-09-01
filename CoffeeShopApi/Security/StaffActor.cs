using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CoffeeShopApi.Security;

public sealed record StaffActor(string? UserId, string DisplayName)
{
    public static StaffActor FromPrincipal(ClaimsPrincipal principal)
    {
        var displayName = principal.FindFirstValue(JwtRegisteredClaimNames.Name);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException("The authenticated staff name claim is missing.");
        }

        return new StaffActor(
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub),
            displayName);
    }
}
