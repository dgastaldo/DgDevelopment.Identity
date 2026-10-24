namespace DgDevelopment.Identity.Server.Security;

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// Bound from the "RateLimiting" config section. Split into two tiers: a generous global
/// baseline applied to every request, and a stricter policy ("auth") applied only to
/// login/registration/password-recovery/MFA pages and /connect/token - the endpoints an
/// attacker would actually want to brute-force or spam. Both are per-client-IP sliding
/// windows (Microsoft.AspNetCore.RateLimiting.SlidingWindowRateLimiterOptions), not a fixed
/// count over the process lifetime.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitPolicyOptions Global { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };
    public RateLimitPolicyOptions Auth { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };
}

public sealed class RateLimitPolicyOptions
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}

/// <summary>
/// Builds the per-client-IP sliding-window partitioner shared by both the global limiter and the
/// "auth" policy - pulled out on its own so unit tests can exercise the actual accept/reject
/// logic deterministically (a fake HttpContext, no live server) instead of only relying on an
/// integration test to trip a real 429, which would fight the shared test fixture's own request
/// volume across the whole suite.
/// </summary>
public static class RateLimiterFactory
{
    public static Func<HttpContext, RateLimitPartition<string>> CreatePartitioner(RateLimitPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return context => RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = TimeSpan.FromSeconds(options.WindowSeconds),
                SegmentsPerWindow = 4,
                QueueLimit = 0,
            });
    }
}
