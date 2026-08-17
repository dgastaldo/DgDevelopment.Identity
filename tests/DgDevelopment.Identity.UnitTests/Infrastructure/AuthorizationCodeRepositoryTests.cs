namespace DgDevelopment.Identity.UnitTests.Infrastructure;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.UnitTests.Testing;
using Xunit;

public sealed class AuthorizationCodeRepositoryTests : IClassFixture<DatabaseFixture<AuthorizationCodeRepositoryTests>>
{
    private readonly DatabaseFixture<AuthorizationCodeRepositoryTests> _fixture;

    public AuthorizationCodeRepositoryTests(DatabaseFixture<AuthorizationCodeRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    private static readonly Uri RedirectUri = new("https://client.example/callback");

    private static string UniqueHash(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task AddAndGetByCodeHashRoundTrips()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var code = new AuthorizationCode(UniqueHash("code"), clientId, userId, RedirectUri, ["openid", "profile"],
            codeChallengeHash: "challenge-hash", codeChallengeMethod: "S256");
        await using var context = _fixture.CreateContext();
        var repo = new AuthorizationCodeRepository(context);
        await repo.AddAsync(code);

        var stored = await repo.GetByCodeHashAsync(code.CodeHash);

        Assert.NotNull(stored);
        Assert.Equal(code.Id, stored.Id);
        Assert.Equal(clientId, stored.ClientId);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal(RedirectUri, stored.RedirectUri);
        Assert.Equal(["openid", "profile"], stored.GetScopes());
        Assert.Equal("challenge-hash", stored.CodeChallengeHash);
        Assert.Equal("S256", stored.CodeChallengeMethod);
        Assert.False(stored.IsUsed);
    }

    [Fact]
    public async Task GetByCodeHashReturnsNullForUnknownCode()
    {
        await using var context = _fixture.CreateContext();
        var repo = new AuthorizationCodeRepository(context);

        var stored = await repo.GetByCodeHashAsync("unknown-hash");

        Assert.Null(stored);
    }

    [Fact]
    public async Task MarkAsUsedPersistsFlag()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var code = new AuthorizationCode(UniqueHash("code"), clientId, userId, RedirectUri, ["openid"]);
        await using var context = _fixture.CreateContext();
        var repo = new AuthorizationCodeRepository(context);
        await repo.AddAsync(code);

        await repo.MarkAsUsedAsync(code.Id);

        var stored = await repo.GetByCodeHashAsync(code.CodeHash);
        Assert.NotNull(stored);
        Assert.True(stored.IsUsed);
    }

    [Fact]
    public async Task DeleteExpiredRemovesOnlyExpiredCodes()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        await using var context = _fixture.CreateContext();
        var repo = new AuthorizationCodeRepository(context);
        var expired = new AuthorizationCode(UniqueHash("expired"), clientId, userId, RedirectUri, ["openid"], lifetimeSeconds: 0);
        var valid = new AuthorizationCode(UniqueHash("valid"), clientId, userId, RedirectUri, ["openid"]);
        await repo.AddAsync(expired);
        await repo.AddAsync(valid);

        await repo.DeleteExpiredAsync();

        Assert.Null(await repo.GetByCodeHashAsync(expired.CodeHash));
        Assert.NotNull(await repo.GetByCodeHashAsync(valid.CodeHash));
    }
}