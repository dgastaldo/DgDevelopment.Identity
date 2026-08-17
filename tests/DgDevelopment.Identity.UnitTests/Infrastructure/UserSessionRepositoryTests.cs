namespace DgDevelopment.Identity.UnitTests.Infrastructure;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.UnitTests.Testing;
using Xunit;

public sealed class UserSessionRepositoryTests : IClassFixture<DatabaseFixture<UserSessionRepositoryTests>>
{
    private readonly DatabaseFixture<UserSessionRepositoryTests> _fixture;

    public UserSessionRepositoryTests(DatabaseFixture<UserSessionRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    private static async Task<User> CreateUserAsync(IdentityDbContext context)
    {
        var user = new User($"session-{Guid.NewGuid():N}", "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@dgdevelopment.it"));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task AddAndGetByIdRoundTrips()
    {
        await using var context = _fixture.CreateContext();
        var user = await CreateUserAsync(context);
        var repo = new UserSessionRepository(context);
        var session = new UserSession(user.Id, $"sid-{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(8), ["pwd"]);

        await repo.AddAsync(session);

        var stored = await repo.GetByIdAsync(session.Id);
        Assert.NotNull(stored);
        Assert.Equal(session.Id, stored.Id);
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(["pwd"], stored.GetAuthMethods());
    }

    [Fact]
    public async Task GetBySessionIdRoundTrips()
    {
        await using var context = _fixture.CreateContext();
        var user = await CreateUserAsync(context);
        var repo = new UserSessionRepository(context);
        var session = new UserSession(user.Id, $"sid-{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(8), ["pwd"]);
        await repo.AddAsync(session);

        var stored = await repo.GetBySessionIdAsync(session.SessionId);

        Assert.NotNull(stored);
        Assert.Equal(session.Id, stored.Id);
    }

    [Fact]
    public async Task GetActiveByUserIdReturnsOnlyActiveSession()
    {
        await using var context = _fixture.CreateContext();
        var user = await CreateUserAsync(context);
        var repo = new UserSessionRepository(context);
        var active = new UserSession(user.Id, $"sid-{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(8), ["pwd"]);
        var revoked = new UserSession(user.Id, $"sid-{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(8), ["pwd"]);
        var expired = new UserSession(user.Id, $"sid-{Guid.NewGuid():N}", DateTime.UtcNow.AddSeconds(-1), ["pwd"]);
        await repo.AddAsync(active);
        await repo.AddAsync(revoked);
        await repo.AddAsync(expired);
        await repo.RevokeAsync(revoked.Id);

        var stored = await repo.GetActiveByUserIdAsync(user.Id);

        Assert.NotNull(stored);
        Assert.Equal(active.Id, stored.Id);
    }

    [Fact]
    public async Task GetActiveByUserIdReturnsNullWhenAllRevokedOrExpired()
    {
        await using var context = _fixture.CreateContext();
        var user = await CreateUserAsync(context);
        var repo = new UserSessionRepository(context);
        var revoked = new UserSession(user.Id, $"sid-{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(8), ["pwd"]);
        await repo.AddAsync(revoked);
        await repo.RevokeAsync(revoked.Id);

        var stored = await repo.GetActiveByUserIdAsync(user.Id);

        Assert.Null(stored);
    }

    [Fact]
    public async Task RevokeAllForUserRevokesEverySession()
    {
        await using var context = _fixture.CreateContext();
        var user = await CreateUserAsync(context);
        var repo = new UserSessionRepository(context);
        var first = new UserSession(user.Id, $"sid-{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(8), ["pwd"]);
        var second = new UserSession(user.Id, $"sid-{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(8), ["pwd"]);
        await repo.AddAsync(first);
        await repo.AddAsync(second);

        await repo.RevokeAllForUserAsync(user.Id);

        Assert.Null(await repo.GetActiveByUserIdAsync(user.Id));
        Assert.True((await repo.GetByIdAsync(first.Id))!.IsRevoked);
        Assert.True((await repo.GetByIdAsync(second.Id))!.IsRevoked);
    }

    [Fact]
    public async Task DeleteExpiredRemovesOnlyExpiredSessions()
    {
        await using var context = _fixture.CreateContext();
        var user = await CreateUserAsync(context);
        var repo = new UserSessionRepository(context);
        var expired = new UserSession(user.Id, $"sid-{Guid.NewGuid():N}", DateTime.UtcNow.AddSeconds(-1), ["pwd"]);
        var valid = new UserSession(user.Id, $"sid-{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(8), ["pwd"]);
        await repo.AddAsync(expired);
        await repo.AddAsync(valid);

        await repo.DeleteExpiredAsync();

        Assert.Null(await repo.GetByIdAsync(expired.Id));
        Assert.NotNull(await repo.GetByIdAsync(valid.Id));
    }
}