using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Backend.Api.Auth;

internal static class UserClaims {
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
