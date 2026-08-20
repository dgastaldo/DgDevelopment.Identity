namespace DgDevelopment.Identity.Server.UnitTests.Services;

using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class ServerSessionServiceTests : IClassFixture<DatabaseFixture<ServerSessionServiceTests>>
{
    private readonly DatabaseFixture<ServerSessionServiceTests> _fixture;

    public ServerSessionServiceTests(DatabaseFixture<ServerSessionServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static ServerSessionService CreateService(IdentityDbContext context)
        => new(new UserSessionRepository(context));

    private async Task<User> CreateUserAsync()
    {
        await using var context = _fixture.CreateContext();
        var user = new User($"session-{Guid.NewGuid():N}", "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@dgdevelopment.it"));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CreateAsyncCreatesStandardLifetimeSession()
    {
        var user = await CreateUserAsync();
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var session = await service.CreateAsync(user, rememberMe: false, ["pwd"]);

        Assert.Equal(user.Id, session.UserId);
        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
        Assert.False(session.IsRevoked);
        Assert.InRange(session.ExpiresAt, DateTime.UtcNow.AddHours(7.5), DateTime.UtcNow.AddHours(8.5));
    }

    [Fact]
    public async Task CreateAsyncRememberMeExtendsLifetimeToFourteenDays()
    {
        var user = await CreateUserAsync();
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var session = await service.CreateAsync(user, rememberMe: true, ["pwd"]);

        Assert.InRange(session.ExpiresAt, DateTime.UtcNow.AddDays(13), DateTime.UtcNow.AddDays(14.1));
    }

    [Fact]
    public async Task CreateAsyncPersistsSession()
    {
        var user = await CreateUserAsync();
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var session = await service.CreateAsync(user, rememberMe: false, ["pwd"]);

        var stored = await new UserSessionRepository(context).GetBySessionIdAsync(session.SessionId);
        Assert.NotNull(stored);
        Assert.Equal(session.Id, stored.Id);
        Assert.Equal(user.Id, stored.UserId);
    }

    [Fact]
    public async Task FindActiveAsyncReturnsActiveSession()
    {
        var user = await CreateUserAsync();
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var session = await service.CreateAsync(user, rememberMe: false, ["pwd"]);

        var found = await service.FindActiveAsync(user.Id);

        Assert.NotNull(found);
        Assert.Equal(session.Id, found.Id);
    }

    [Fact]
    public async Task FindActiveAsyncReturnsNullForUnknownUser()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var found = await service.FindActiveAsync(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public async Task FindActiveAsyncExcludesRevokedSessions()
    {
        var user = await CreateUserAsync();
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var session = await service.CreateAsync(user, rememberMe: false, ["pwd"]);
        await new UserSessionRepository(context).RevokeAsync(session.Id);

        var found = await service.FindActiveAsync(user.Id);

        Assert.Null(found);
    }

    [Fact]
    public async Task FindActiveAsyncExcludesExpiredSessions()
    {
        var user = await CreateUserAsync();
        await using var context = _fixture.CreateContext();
        var repo = new UserSessionRepository(context);
        await repo.AddAsync(new UserSession(user.Id, $"expired-{Guid.NewGuid():N}", DateTime.UtcNow.AddSeconds(-1), ["pwd"]));
        var service = CreateService(context);

        var found = await service.FindActiveAsync(user.Id);

        Assert.Null(found);
    }
}