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

        policy.Headers.Clear();
        policy.Methods.Clear();

        return Task.FromResult<CorsPolicy?>(policy);
    }
}
