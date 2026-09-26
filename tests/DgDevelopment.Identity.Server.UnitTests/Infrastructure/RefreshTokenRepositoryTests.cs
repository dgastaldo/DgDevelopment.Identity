namespace DgDevelopment.Identity.Server.UnitTests.Infrastructure;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class RefreshTokenRepositoryTests : IClassFixture<DatabaseFixture<RefreshTokenRepositoryTests>>
{
    private readonly DatabaseFixture<RefreshTokenRepositoryTests> _fixture;

    public RefreshTokenRepositoryTests(DatabaseFixture<RefreshTokenRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    private static string UniqueHash(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private async Task<Guid> CreateSessionAsync(Guid userId)
    {
        await using var context = _fixture.CreateContext();
        var session = new UserSession(userId, $"session-{Guid.NewGuid():N}", DateTime.UtcNow.AddDays(1), ["pwd"]);
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();
        return session.Id;
    }

    [Fact]
    public async Task AddAndGetByTokenHashRoundTrips()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var sessionId = await CreateSessionAsync(userId);
        var token = new RefreshToken(tenantId, UniqueHash("token"), clientId, userId, sessionId, ["openid", "profile"]);
        await using var context = _fixture.CreateContext();
        var repo = new RefreshTokenRepository(context);
        await repo.AddAsync(token);

        var stored = await repo.GetByTokenHashAsync(token.TokenHash);

        Assert.NotNull(stored);
        Assert.Equal(clientId, stored.ClientId);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal(["openid", "profile"], stored.GetScopes());
        Assert.Null(stored.PreviousTokenId);
        Assert.False(stored.IsRevoked);
    }

    [Fact]
    public async Task GetByTokenHashReturnsNullForUnknownToken()
    {
        await using var context = _fixture.CreateContext();
        var repo = new RefreshTokenRepository(context);

        var stored = await repo.GetByTokenHashAsync("unknown-hash");

        Assert.Null(stored);
    }

    [Fact]
    public async Task RevokePersistsFlag()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var sessionId = await CreateSessionAsync(userId);
        var token = new RefreshToken(tenantId, UniqueHash("token"), clientId, userId, sessionId, ["openid"]);
        await using var context = _fixture.CreateContext();
        var repo = new RefreshTokenRepository(context);
        await repo.AddAsync(token);

        await repo.RevokeAsync(token.Id);

        var stored = await repo.GetByTokenHashAsync(token.TokenHash);
        Assert.NotNull(stored);
        Assert.True(stored.IsRevoked);
    }

    [Fact]
    public async Task RevokeChainRevokesTheEntireFamily()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var sessionId = await CreateSessionAsync(userId);
        await using var context = _fixture.CreateContext();
        var repo = new RefreshTokenRepository(context);

        var first = new RefreshToken(tenantId, UniqueHash("chain"), clientId, userId, sessionId, ["openid"]);
        await repo.AddAsync(first);
        var second = new RefreshToken(tenantId, UniqueHash("chain"), clientId, userId, sessionId, ["openid"], first.Id);
        await repo.AddAsync(second);
        var third = new RefreshToken(tenantId, UniqueHash("chain"), clientId, userId, sessionId, ["openid"], second.Id);
        await repo.AddAsync(third);

        await repo.RevokeChainAsync(third.TokenHash);

        Assert.True((await repo.GetByTokenHashAsync(first.TokenHash))!.IsRevoked);
        Assert.True((await repo.GetByTokenHashAsync(second.TokenHash))!.IsRevoked);
        Assert.True((await repo.GetByTokenHashAsync(third.TokenHash))!.IsRevoked);
    }

    [Fact]
    public async Task DeleteExpiredRemovesOnlyExpiredTokens()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var sessionId = await CreateSessionAsync(userId);
        await using var context = _fixture.CreateContext();
        var repo = new RefreshTokenRepository(context);
        var expired = new RefreshToken(tenantId, UniqueHash("expired"), clientId, userId, sessionId, ["openid"], lifetimeDays: 0);
        var valid = new RefreshToken(tenantId, UniqueHash("valid"), clientId, userId, sessionId, ["openid"]);
        await repo.AddAsync(expired);
        await repo.AddAsync(valid);

        await repo.DeleteExpiredAsync();

        Assert.Null(await repo.GetByTokenHashAsync(expired.TokenHash));
        Assert.NotNull(await repo.GetByTokenHashAsync(valid.TokenHash));
    }
}