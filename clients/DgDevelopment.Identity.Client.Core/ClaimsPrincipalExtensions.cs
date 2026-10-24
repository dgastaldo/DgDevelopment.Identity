namespace DgDevelopment.Identity.Client.Core;

using System.Security.Claims;

public static class ClaimsPrincipalExtensions
{
    public static bool HasPermission(this ClaimsPrincipal user, string permission)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.HasClaim("permission", permission);
    }
}
