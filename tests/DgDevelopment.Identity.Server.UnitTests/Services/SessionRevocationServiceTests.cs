namespace DgDevelopment.Identity.Server.UnitTests.Services;

using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class SessionRevocationServiceTests : IClassFixture<DatabaseFixture<SessionRevocationServiceTests>>
{
    private readonly DatabaseFixture<SessionRevocationServiceTests> _fixture;

    public SessionRevocationServiceTests(DatabaseFixture<SessionRevocationServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<(User User, Guid TenantId, Guid ClientId)> SeedUserWithSessionAndRefreshTokenAsync(IdentityDbContext context)
    {
        var user = new User(Unique("user"), "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@example.com"));
        context.Users.Add(user);

        var tenant = new Tenant(Unique("tenant"), Unique("tenant"));
        context.Tenants.Add(tenant);

        var platform = new Platform(tenant.Id, Unique("Platform"), "desc", PermissionMode.AuthOnly);
        context.Platforms.Add(platform);

        var client = new Client(tenant.Id, Guid.NewGuid(), "secret-hash", "Test Client", ClientType.Confidential, platform.Id);
        context.Clients.Add(client);

        await context.SaveChangesAsync();

        var session = new UserSession(user.Id, Unique("session"), DateTime.UtcNow.AddHours(8), ["pwd"]);
        context.UserSessions.Add(session);

        var refreshToken = new RefreshToken(tenant.Id, Unique("token-hash"), client.Id, user.Id, session.Id, ["openid"]);
        context.RefreshTokens.Add(refreshToken);

        await context.SaveChangesAsync();

        return (user, tenant.Id, client.Id);
    }

    [Fact]
    public async Task RevokeAllAsyncRevokesEverySessionAndRefreshTokenForTheUser()
    {
        await using var context = _fixture.CreateContext();
        var (user, _, _) = await SeedUserWithSessionAndRefreshTokenAsync(context);

        var publisher = new FakeSessionEventPublisher();
        var service = new SessionRevocationService(
            new UserSessionRepository(context), new RefreshTokenRepository(context), publisher);

        await service.RevokeAllAsync(user.Id);

        await using var verifyContext = _fixture.CreateContext();
        var session = await verifyContext.UserSessions.SingleAsync(s => s.UserId == user.Id);
        var refreshToken = await verifyContext.RefreshTokens.SingleAsync(r => r.UserId == user.Id);

        Assert.True(session.IsRevoked);
        Assert.True(refreshToken.IsRevoked);
    }

    [Fact]
    public async Task RevokeAllAsyncPublishesAForceLogoutEventForTheUser()
    {
        await using var context = _fixture.CreateContext();
        var (user, _, _) = await SeedUserWithSessionAndRefreshTokenAsync(context);

        var publisher = new FakeSessionEventPublisher();
        var service = new SessionRevocationService(
            new UserSessionRepository(context), new RefreshTokenRepository(context), publisher);

        await service.RevokeAllAsync(user.Id);

        Assert.Contains(user.Id, publisher.PublishedForUserIds);
    }

    [Fact]
    public async Task RevokeAllAsyncDoesNotAffectAnotherUsersSessionsOrTokens()
    {
        await using var context = _fixture.CreateContext();
        var (targetUser, _, _) = await SeedUserWithSessionAndRefreshTokenAsync(context);
        var (otherUser, _, _) = await SeedUserWithSessionAndRefreshTokenAsync(context);

        var service = new SessionRevocationService(
            new UserSessionRepository(context), new RefreshTokenRepository(context), new FakeSessionEventPublisher());

        await service.RevokeAllAsync(targetUser.Id);

        await using var verifyContext = _fixture.CreateContext();
        var otherSession = await verifyContext.UserSessions.SingleAsync(s => s.UserId == otherUser.Id);
        var otherToken = await verifyContext.RefreshTokens.SingleAsync(r => r.UserId == otherUser.Id);

        Assert.False(otherSession.IsRevoked);
        Assert.False(otherToken.IsRevoked);
    }
}
