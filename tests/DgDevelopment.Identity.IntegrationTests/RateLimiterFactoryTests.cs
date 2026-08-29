namespace DgDevelopment.Identity.IntegrationTests;

using System.Net;
using System.Threading.RateLimiting;
using DgDevelopment.Identity.Server.Security;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Exercises the actual accept/reject logic behind [EnableRateLimiting("auth")] and the global
/// limiter deterministically - a fake HttpContext, no live server - rather than trying to trip a
/// real 429 against IdentityWebApplicationFactory, whose limits are deliberately set very high
/// (see IdentityWebApplicationFactory's constructor) so the other ~40 tests sharing that one host
/// instance across the whole "Integration" collection don't hit them by accident.
/// </summary>
public sealed class RateLimiterFactoryTests
{
    [Fact]
    public void CreatePartitionerAllowsUpToThePermitLimitThenRejects()
    {
        var options = new RateLimitPolicyOptions { PermitLimit = 3, WindowSeconds = 60 };
        using var limiter = PartitionedRateLimiter.Create(RateLimiterFactory.CreatePartitioner(options));
        var context = ContextFromIp("203.0.113.5");

        for (var i = 0; i < 3; i++)
            Assert.True(limiter.AttemptAcquire(context).IsAcquired);

        Assert.False(limiter.AttemptAcquire(context).IsAcquired);
    }

    [Fact]
    public void CreatePartitionerTracksEachClientIpIndependently()
    {
        var options = new RateLimitPolicyOptions { PermitLimit = 1, WindowSeconds = 60 };
        using var limiter = PartitionedRateLimiter.Create(RateLimiterFactory.CreatePartitioner(options));
        var contextA = ContextFromIp("203.0.113.5");
        var contextB = ContextFromIp("203.0.113.6");

        Assert.True(limiter.AttemptAcquire(contextA).IsAcquired);
        Assert.False(limiter.AttemptAcquire(contextA).IsAcquired);
        // A different IP has its own, untouched budget.
        Assert.True(limiter.AttemptAcquire(contextB).IsAcquired);
    }

    private static DefaultHttpContext ContextFromIp(string ip)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }
}
