namespace DgDevelopment.Identity.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Entities;

public sealed class RefreshTokenTests
{
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();

    [Fact]
    public void ConstructorStoresFieldsWithDefaults()
    {
        var token = new RefreshToken("hash", ClientId, UserId, SessionId, ["openid"]);

        Assert.Equal("hash", token.TokenHash);
        Assert.Equal(ClientId, token.ClientId);
        Assert.Equal(UserId, token.UserId);
        Assert.Equal(SessionId, token.SessionId);
        Assert.Null(token.PreviousTokenId);
        Assert.False(token.IsRevoked);
        Assert.True(token.ExpiresAt > DateTime.UtcNow);
        Assert.False(token.IsExpired());
    }

    [Fact]
    public void ConstructorStoresPreviousTokenForRotation()
    {
        var previousId = Guid.NewGuid();

        var token = new RefreshToken("h", ClientId, UserId, SessionId, ["openid"], previousId);

        Assert.Equal(previousId, token.PreviousTokenId);
    }

    [Fact]
    public void RevokeSetsRevokedFlag()
    {
        var token = new RefreshToken("h", ClientId, UserId, SessionId, ["openid"]);

        token.Revoke();

        Assert.True(token.IsRevoked);
    }

    [Fact]
    public void IsExpiredFalseForDefaultLifetime()
    {
        var token = new RefreshToken("h", ClientId, UserId, SessionId, ["openid"]);

        Assert.False(token.IsExpired());
    }

    [Fact]
    public void IsExpiredTrueForZeroDayLifetime()
    {
        var token = new RefreshToken("h", ClientId, UserId, SessionId, ["openid"], lifetimeDays: 0);

        Assert.True(token.IsExpired());
    }

    [Fact]
    public void GetScopesRoundTripsTheStoredScopes()
    {
        var token = new RefreshToken("h", ClientId, UserId, SessionId, ["openid", "profile"]);

        Assert.Equal(["openid", "profile"], token.GetScopes());
    }
}