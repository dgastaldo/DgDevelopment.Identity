namespace DgDevelopment.Identity.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Entities;

public sealed class AuthorizationCodeTests
{
    private static readonly Uri RedirectUri = new("https://client.example/callback");

    [Fact]
    public void ConstructorStoresFieldsWithDefaults()
    {
        var userId = Guid.NewGuid();
        var code = new AuthorizationCode("code-hash", Guid.NewGuid(), userId, RedirectUri, ["openid"]);

        Assert.Equal("code-hash", code.CodeHash);
        Assert.Equal(userId, code.UserId);
        Assert.Equal(RedirectUri, code.RedirectUri);
        Assert.Null(code.CodeChallengeHash);
        Assert.Null(code.CodeChallengeMethod);
        Assert.False(code.IsUsed);
        Assert.True(code.ExpiresAt > DateTime.UtcNow);
        Assert.False(code.IsExpired());
    }

    [Fact]
    public void ConstructorStoresPkceChallenge()
    {
        var code = new AuthorizationCode("ch", Guid.NewGuid(), Guid.NewGuid(), RedirectUri, ["openid"],
            codeChallengeHash: "challenge-hash", codeChallengeMethod: "S256");

        Assert.Equal("challenge-hash", code.CodeChallengeHash);
        Assert.Equal("S256", code.CodeChallengeMethod);
    }

    [Fact]
    public void MarkUsedSetsUsedFlag()
    {
        var code = new AuthorizationCode("h", Guid.NewGuid(), Guid.NewGuid(), RedirectUri, ["openid"]);

        code.MarkUsed();

        Assert.True(code.IsUsed);
    }

    [Fact]
    public void IsExpiredFalseForDefaultLifetime()
    {
        var code = new AuthorizationCode("h", Guid.NewGuid(), Guid.NewGuid(), RedirectUri, ["openid"]);

        Assert.False(code.IsExpired());
    }

    [Fact]
    public void IsExpiredTrueForExpiredLifetime()
    {
        var code = new AuthorizationCode("h", Guid.NewGuid(), Guid.NewGuid(), RedirectUri, ["openid"], lifetimeSeconds: 0);

        Assert.True(code.IsExpired());
    }

    [Fact]
    public void ConstructorRejectsNullRedirectUri()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AuthorizationCode("h", Guid.NewGuid(), Guid.NewGuid(), null!, ["openid"]));
    }

    [Fact]
    public void GetScopesRoundTripsTheStoredScopes()
    {
        var code = new AuthorizationCode("h", Guid.NewGuid(), Guid.NewGuid(), RedirectUri, ["openid", "profile"]);

        Assert.Equal(["openid", "profile"], code.GetScopes());
    }
}