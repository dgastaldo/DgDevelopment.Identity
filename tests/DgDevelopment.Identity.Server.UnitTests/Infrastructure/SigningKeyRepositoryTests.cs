namespace DgDevelopment.Identity.Server.UnitTests.Infrastructure;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class SigningKeyRepositoryTests : IClassFixture<DatabaseFixture<SigningKeyRepositoryTests>>
{
    private readonly DatabaseFixture<SigningKeyRepositoryTests> _fixture;

    public SigningKeyRepositoryTests(DatabaseFixture<SigningKeyRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddAndGetByKeyIdRoundTrips()
    {
        var key = new SigningKey($"kid-1-{Guid.NewGuid():N}", "RS256", "private-data", "public-data", DateTime.UtcNow.AddDays(30));
        await using var context = _fixture.CreateContext();
        var repo = new SigningKeyRepository(context);
        await repo.AddAsync(key);

        var stored = await repo.GetByKeyIdAsync(key.Id);

        Assert.NotNull(stored);
        Assert.Equal("RS256", stored.Algorithm);
        Assert.Equal("private-data", stored.KeyData);
        Assert.Equal("public-data", stored.PublicKeyData);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task GetByKeyIdReturnsNullForUnknownKey()
    {
        await using var context = _fixture.CreateContext();
        var repo = new SigningKeyRepository(context);

        var stored = await repo.GetByKeyIdAsync("unknown-kid");

        Assert.Null(stored);
    }

    [Fact]
    public async Task GetActiveKeysReturnsOnlyActiveAndNotExpired()
    {
        await using var context = _fixture.CreateContext();
        var repo = new SigningKeyRepository(context);
        var active = new SigningKey($"kid-active-{Guid.NewGuid():N}", "RS256", "p", "pub", DateTime.UtcNow.AddDays(30));
        var deactivated = new SigningKey($"kid-deactivated-{Guid.NewGuid():N}", "RS256", "p", "pub", DateTime.UtcNow.AddDays(30));
        var expired = new SigningKey($"kid-expired-{Guid.NewGuid():N}", "RS256", "p", "pub", DateTime.UtcNow.AddDays(-1));
        await repo.AddAsync(active);
        await repo.AddAsync(deactivated);
        await repo.AddAsync(expired);
        await repo.DeactivateAsync(deactivated.Id);

        var keys = await repo.GetActiveKeysAsync();

        Assert.Contains(active.Id, keys.Select(k => k.Id));
        Assert.DoesNotContain(deactivated.Id, keys.Select(k => k.Id));
        Assert.DoesNotContain(expired.Id, keys.Select(k => k.Id));
    }

    [Fact]
    public async Task DeactivatePersistsDeactivation()
    {
        var key = new SigningKey($"kid-1-{Guid.NewGuid():N}", "RS256", "p", "pub", DateTime.UtcNow.AddDays(30));
        await using var context = _fixture.CreateContext();
        var repo = new SigningKeyRepository(context);
        await repo.AddAsync(key);

        await repo.DeactivateAsync(key.Id);

        var stored = await repo.GetByKeyIdAsync(key.Id);
        Assert.NotNull(stored);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task DeleteExpiredRemovesOnlyExpiredKeys()
    {
        await using var context = _fixture.CreateContext();
        var repo = new SigningKeyRepository(context);
        var expired = new SigningKey($"kid-expired-{Guid.NewGuid():N}", "RS256", "p", "pub", DateTime.UtcNow.AddDays(-1));
        var valid = new SigningKey($"kid-valid-{Guid.NewGuid():N}", "RS256", "p", "pub", DateTime.UtcNow.AddDays(30));
        await repo.AddAsync(expired);
        await repo.AddAsync(valid);

        await repo.DeleteExpiredAsync();

        Assert.Null(await repo.GetByKeyIdAsync(expired.Id));
        Assert.NotNull(await repo.GetByKeyIdAsync(valid.Id));
    }
}