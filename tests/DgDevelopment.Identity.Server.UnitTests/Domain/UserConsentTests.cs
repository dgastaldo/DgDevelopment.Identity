namespace DgDevelopment.Identity.Server.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Entities;

public sealed class UserConsentTests
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void ConstructorStoresUserClientAndScopes()
    {
        var userId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var consent = new UserConsent(TenantId,userId, clientId, ["openid", "profile", "email"]);

        Assert.Equal(userId, consent.UserId);
        Assert.Equal(clientId, consent.ClientId);
        Assert.Equal(["openid", "profile", "email"], consent.GetScopes());
        Assert.False(consent.CreatedAt == default);
    }

    [Fact]
    public void ConstructorNoExpiryDefaultsToNeverExpiring()
    {
        var consent = new UserConsent(TenantId,Guid.NewGuid(), Guid.NewGuid(), ["openid"]);

        Assert.Null(consent.ExpiresAt);
        Assert.False(consent.IsExpired());
    }

    [Fact]
    public void ConstructorStoresProvidedExpiry()
    {
        var expiry = Now.AddDays(30);
        var consent = new UserConsent(TenantId,Guid.NewGuid(), Guid.NewGuid(), ["openid"], expiry);

        Assert.Equal(expiry, consent.ExpiresAt);
    }

    [Fact]
    public void IsExpiredFalseWhenExpiryIsInTheFuture()
    {
        var consent = new UserConsent(TenantId,Guid.NewGuid(), Guid.NewGuid(), ["openid"], Now.AddDays(30));

        Assert.False(consent.IsExpired());
    }

    [Fact]
    public void IsExpiredTrueWhenExpiryHasPassed()
    {
        var consent = new UserConsent(TenantId,Guid.NewGuid(), Guid.NewGuid(), ["openid"], Now.AddDays(-1));

        Assert.True(consent.IsExpired());
    }

    [Fact]
    public void GetScopesRoundTripsTheStoredScopes()
    {
        var consent = new UserConsent(TenantId,Guid.NewGuid(), Guid.NewGuid(), ["openid", "profile"]);

        Assert.Equal(["openid", "profile"], consent.GetScopes());
    }
}