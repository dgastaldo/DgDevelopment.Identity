namespace DgDevelopment.Identity.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Entities;

public sealed class DeviceCodeTests
{
    private static readonly Guid ClientId = Guid.NewGuid();

    [Fact]
    public void ConstructorStoresHashesClientAndScopesWithDefaults()
    {
        var code = new DeviceCode("device-hash", "user-hash", ClientId, ["openid", "profile"]);

        Assert.Equal("device-hash", code.DeviceCodeHash);
        Assert.Equal("user-hash", code.UserCodeHash);
        Assert.Equal(ClientId, code.ClientId);
        Assert.Null(code.UserId);
        Assert.False(code.IsAuthorized);
        Assert.False(code.IsUsed);
        Assert.Null(code.LastPolledAt);
        Assert.True(code.ExpiresAt > DateTime.UtcNow);
        Assert.False(code.IsExpired());
    }

    [Fact]
    public void AuthorizeAssociatesUserAndMarksAuthorized()
    {
        var code = new DeviceCode("d", "u", ClientId, ["openid"]);
        var userId = Guid.NewGuid();

        code.Authorize(userId);

        Assert.True(code.IsAuthorized);
        Assert.Equal(userId, code.UserId);
    }

    [Fact]
    public void MarkUsedSetsUsedFlag()
    {
        var code = new DeviceCode("d", "u", ClientId, ["openid"]);

        code.MarkUsed();

        Assert.True(code.IsUsed);
    }

    [Fact]
    public void RecordPollStoresTimestamp()
    {
        var code = new DeviceCode("d", "u", ClientId, ["openid"]);
        var now = DateTime.UtcNow;

        code.RecordPoll(now);

        Assert.Equal(now, code.LastPolledAt);
    }

    [Fact]
    public void IsExpiredFalseForDefaultLifetime()
    {
        var code = new DeviceCode("d", "u", ClientId, ["openid"]);

        Assert.False(code.IsExpired());
    }

    [Fact]
    public void IsExpiredTrueForExpiredLifetime()
    {
        var code = new DeviceCode("d", "u", ClientId, ["openid"], lifetimeSeconds: 0);

        Assert.True(code.IsExpired());
    }

    [Fact]
    public void GetScopesRoundTripsTheStoredScopes()
    {
        var code = new DeviceCode("d", "u", ClientId, ["openid", "profile"]);

        Assert.Equal(["openid", "profile"], code.GetScopes());
    }
}