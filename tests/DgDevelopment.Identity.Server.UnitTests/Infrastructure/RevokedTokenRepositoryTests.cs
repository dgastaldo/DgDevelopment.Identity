namespace DgDevelopment.Identity.Server.UnitTests.Infrastructure;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class RevokedTokenRepositoryTests : IClassFixture<DatabaseFixture<RevokedTokenRepositoryTests>>
{
    private readonly DatabaseFixture<RevokedTokenRepositoryTests> _fixture;

    public RevokedTokenRepositoryTests(DatabaseFixture<RevokedTokenRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Hash(string value)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static RevokedToken CreateToken(string jti, DateTime expiresAt)
        => new(jti, "access_token", clientId: null, userId: null, expiresAt);

    [Fact]
    public async Task AddThenExistsReturnsTrue()
    {
        await using var context = _fixture.CreateContext();
        var repo = new RevokedTokenRepository(context);
        var jti = $"jti-{Guid.NewGuid():N}";

        await repo.AddAsync(CreateToken(jti, DateTime.UtcNow.AddHours(1)));

        Assert.True(await repo.ExistsAsync(Hash(jti)));
    }

    [Fact]
    public async Task ExistsReturnsFalseForExpiredEntry()
    {
        await using var context = _fixture.CreateContext();
        var repo = new RevokedTokenRepository(context);
        var jti = $"jti-{Guid.NewGuid():N}";

        await repo.AddAsync(CreateToken(jti, DateTime.UtcNow.AddHours(-1)));

        Assert.False(await repo.ExistsAsync(Hash(jti)));
    }

    [Fact]
    public async Task ExistsReturnsFalseForUnknownJti()
    {
        await using var context = _fixture.CreateContext();
        var repo = new RevokedTokenRepository(context);

        Assert.False(await repo.ExistsAsync(Hash($"jti-{Guid.NewGuid():N}")));
    }

    [Fact]
    public async Task DeleteExpiredAsyncRemovesOnlyExpiredEntries()
    {
        await using var context = _fixture.CreateContext();
        var repo = new RevokedTokenRepository(context);
        var activeJti = $"jti-{Guid.NewGuid():N}";
        var expiredJti = $"jti-{Guid.NewGuid():N}";

        await repo.AddAsync(CreateToken(activeJti, DateTime.UtcNow.AddHours(1)));
        await repo.AddAsync(CreateToken(expiredJti, DateTime.UtcNow.AddHours(-1)));
        await repo.DeleteExpiredAsync();

        Assert.True(await repo.ExistsAsync(Hash(activeJti)));
        Assert.False(await repo.ExistsAsync(Hash(expiredJti)));
    }
}