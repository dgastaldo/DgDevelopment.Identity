namespace DgDevelopment.Identity.Server.UnitTests.Infrastructure;

using System.Runtime.CompilerServices;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class UserConsentRepositoryTests : IClassFixture<DatabaseFixture<UserConsentRepositoryTests>>
{
    private readonly DatabaseFixture<UserConsentRepositoryTests> _fixture;

    public UserConsentRepositoryTests(DatabaseFixture<UserConsentRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    private async Task<Guid> CreateUserAsync([CallerMemberName] string method = "")
    {
        await using var context = _fixture.CreateContext();
        var user = new User($"{method}-{Guid.NewGuid():N}", "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@dgdevelopment.it"));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task AddOrUpdateAddsNewConsent()
    {
        var userId = await CreateUserAsync();
        var clientId = Guid.NewGuid();
        var consent = new UserConsent(userId, clientId, ["openid", "profile"], DateTime.UtcNow.AddDays(30));
        await using var context = _fixture.CreateContext();
        var repo = new UserConsentRepository(context);
        await repo.AddOrUpdateAsync(consent);

        var stored = await repo.GetAsync(userId, clientId);

        Assert.NotNull(stored);
        Assert.Equal(["openid", "profile"], stored.GetScopes());
        Assert.NotNull(stored.ExpiresAt);
    }

    [Fact]
    public async Task AddOrUpdateUpdatesExistingConsentInPlace()
    {
        var userId = await CreateUserAsync();
        var clientId = Guid.NewGuid();
        await using var context = _fixture.CreateContext();
        var repo = new UserConsentRepository(context);

        await repo.AddOrUpdateAsync(new UserConsent(userId, clientId, ["openid"], DateTime.UtcNow.AddDays(10)));
        await repo.AddOrUpdateAsync(new UserConsent(userId, clientId, ["openid", "profile"], DateTime.UtcNow.AddDays(180)));

        var consents = await repo.GetByUserAsync(userId);
        var stored = Assert.Single(consents);
        Assert.Equal(["openid", "profile"], stored.GetScopes());
        Assert.True(stored.ExpiresAt > DateTime.UtcNow.AddDays(150));
    }

    [Fact]
    public async Task GetByUserReturnsOnlyThatUsersConsents()
    {
        var aliId = await CreateUserAsync();
        var bobId = await CreateUserAsync();
        await using var context = _fixture.CreateContext();
        var repo = new UserConsentRepository(context);
        await repo.AddOrUpdateAsync(new UserConsent(aliId, Guid.NewGuid(), ["openid"], null));
        await repo.AddOrUpdateAsync(new UserConsent(aliId, Guid.NewGuid(), ["profile"], null));
        await repo.AddOrUpdateAsync(new UserConsent(bobId, Guid.NewGuid(), ["openid"], null));

        var aliConsents = await repo.GetByUserAsync(aliId);

        Assert.Equal(2, aliConsents.Count);
        Assert.True(aliConsents.All(c => c.UserId == aliId));
    }

    [Fact]
    public async Task RevokeRemovesTheConsent()
    {
        var userId = await CreateUserAsync();
        var clientId = Guid.NewGuid();
        await using var context = _fixture.CreateContext();
        var repo = new UserConsentRepository(context);
        await repo.AddOrUpdateAsync(new UserConsent(userId, clientId, ["openid"], null));

        await repo.RevokeAsync(userId, clientId);

        Assert.Null(await repo.GetAsync(userId, clientId));
    }

    [Fact]
    public async Task DeleteExpiredRemovesOnlyExpiredConsents()
    {
        var userId = await CreateUserAsync();
        await using var context = _fixture.CreateContext();
        var repo = new UserConsentRepository(context);
        var expiredClientId = Guid.NewGuid();
        var validClientId = Guid.NewGuid();
        await repo.AddOrUpdateAsync(new UserConsent(userId, expiredClientId, ["openid"], DateTime.UtcNow.AddDays(-1)));
        await repo.AddOrUpdateAsync(new UserConsent(userId, validClientId, ["openid"], DateTime.UtcNow.AddDays(30)));

        await repo.DeleteExpiredAsync();

        Assert.Null(await repo.GetAsync(userId, expiredClientId));
        Assert.NotNull(await repo.GetAsync(userId, validClientId));
    }
}