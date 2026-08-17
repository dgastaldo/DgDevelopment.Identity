namespace DgDevelopment.Identity.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Entities;
using Xunit;

public sealed class UserSessionTests
{
    [Fact]
    public void ConstructorSetsFields()
    {
        var expiresAt = DateTime.UtcNow.AddHours(8);
        var session = new UserSession(Guid.NewGuid(), "session-1", expiresAt, ["pwd", "otp"]);

        Assert.False(string.IsNullOrWhiteSpace(session.Id.ToString()));
        Assert.Equal("session-1", session.SessionId);
        Assert.Equal(expiresAt, session.ExpiresAt);
        Assert.False(session.IsRevoked);
        Assert.True(session.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void GetAuthMethodsRoundTripsJsonSerialization()
    {
        var session = new UserSession(Guid.NewGuid(), "session-1", DateTime.UtcNow.AddHours(8), ["pwd", "otp"]);

        Assert.Equal(["pwd", "otp"], session.GetAuthMethods());
    }

    [Fact]
    public void RevokeSetsRevokedFlag()
    {
        var session = new UserSession(Guid.NewGuid(), "session-1", DateTime.UtcNow.AddHours(8), ["pwd"]);

        session.Revoke();

        Assert.True(session.IsRevoked);
    }

    [Fact]
    public void IsExpiredReflectsExpiry()
    {
        var expired = new UserSession(Guid.NewGuid(), "expired", DateTime.UtcNow.AddSeconds(-1), ["pwd"]);
        var active = new UserSession(Guid.NewGuid(), "active", DateTime.UtcNow.AddHours(8), ["pwd"]);

        Assert.True(expired.IsExpired());
        Assert.False(active.IsExpired());
    }
}