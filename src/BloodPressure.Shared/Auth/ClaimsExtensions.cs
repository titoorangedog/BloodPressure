using System.Security.Claims;
using BloodPressure.Shared.Domain;

namespace BloodPressure.Shared.Auth;

public static class ClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Missing user id claim.");
    }

    public static UserRole GetUserRole(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(AuthConstants.RoleClaim)?.Value;
        return Enum.TryParse<UserRole>(value, out var role)
            ? role
            : UserRole.User;
    }
}
