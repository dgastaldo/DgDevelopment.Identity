using Microsoft.AspNetCore.Cors.Infrastructure;

namespace DgDevelopment.Identity.Server.Services;

public sealed class DynamicCorsPolicyProvider(ICorsOriginCache originCache) : ICorsPolicyProvider
{
    public Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var policy = new CorsPolicy
        {
            IsOriginAllowed = origin => originCache.IsAllowedOrigin(origin),
            SupportsCredentials = true,
        };

        policy.Headers.Add("*");
        policy.Methods.Add("*");

        return Task.FromResult<CorsPolicy?>(policy);
    }
}
