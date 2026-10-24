using DgDevelopment.Identity.Application.Common;

namespace DgDevelopment.Identity.Server.UnitTests.Application;

public sealed class PasswordExpirationPolicyTests
{
    [Fact]
    public void IsExpiredReturnsFalseForARecentlyChangedPassword()
    {
        Assert.False(PasswordExpirationPolicy.IsExpired(DateTime.UtcNow.AddDays(-1)));
    }

    [Fact]
    public void IsExpiredReturnsTrueOncePastTheExpirationPeriod()
    {
        Assert.True(PasswordExpirationPolicy.IsExpired(DateTime.UtcNow - PasswordExpirationPolicy.ExpirationPeriod - TimeSpan.FromMinutes(1)));
    }
}
