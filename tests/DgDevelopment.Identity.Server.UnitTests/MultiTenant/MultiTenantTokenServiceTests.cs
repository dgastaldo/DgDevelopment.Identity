namespace DgDevelopment.Identity.Server.UnitTests.MultiTenant;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class MultiTenantTokenServiceTests : IClassFixture<DatabaseFixture<MultiTenantTokenServiceTests>>
{
    private readonly DatabaseFixture<MultiTenantTokenServiceTests> _fixture;

    public MultiTenantTokenServiceTests(DatabaseFixture<MultiTenantTokenServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static string Hash(string value)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class FakeIssuerProvider : IOidcIssuerProvider
    {
        public Uri GetIssuer() => new("https://idp.test.local");
    }

    private static TokenService CreateService(IdentityDbContext context, KeyMaterialService keyMaterial)
        => new(
            new AuthorizationCodeRepository(context),
            new RefreshTokenRepository(context),
            new DeviceCodeRepository(context),
            new ClientRepository(context),
            new UserRepository(context),
            new UserSessionRepository(context),
            new TenantRepository(context),
            new JwtService(keyMaterial, new FakeIssuerProvider()),
            new EffectivePermissionsService(new UserAuthorizationRepository(context)));

    [Fact]
    public async Task ProcessRefreshTokenAsyncThrowsWhenTenantMembershipWasRemoved()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();

        await using var context = _fixture.CreateContext();
        var tenant = new Tenant(Unique("Tenant"), Unique("tenant"));
        context.Tenants.Add(tenant);
        var user = new User(Unique("user"), "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@example.com"));
        context.Users.Add(user);
        var membership = new TenantMembership(tenant.Id, user.Id);
        context.TenantMemberships.Add(membership);
        var session = new UserSession(user.Id, Unique("session"), DateTime.UtcNow.AddHours(8), ["pwd"]);
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();

        var value = $"rt-{Guid.NewGuid():N}";
        var refreshRepo = new RefreshTokenRepository(context);
        await refreshRepo.AddAsync(new RefreshToken(tenant.Id, Hash(value), clientId, user.Id, session.Id, ["openid"]));

        membership.Remove();
        context.TenantMemberships.Update(membership);
        await context.SaveChangesAsync();

        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessRefreshTokenAsync(value, TestConstants.AdminClientId));

        Assert.Equal("Tenant membership revoked.", exception.Message);
    }

    [Fact]
    public async Task ProcessRefreshTokenAsyncSucceedsWhileMembershipIsActive()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();

        await using var context = _fixture.CreateContext();
        var tenant = new Tenant(Unique("Tenant"), Unique("tenant"));
        context.Tenants.Add(tenant);
        var user = new User(Unique("user"), "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@example.com"));
        context.Users.Add(user);
        context.TenantMemberships.Add(new TenantMembership(tenant.Id, user.Id));
        var session = new UserSession(user.Id, Unique("session"), DateTime.UtcNow.AddHours(8), ["pwd"]);
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();

        var value = $"rt-{Guid.NewGuid():N}";
        var refreshRepo = new RefreshTokenRepository(context);
        await refreshRepo.AddAsync(new RefreshToken(tenant.Id, Hash(value), clientId, user.Id, session.Id, ["openid"]));

        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var result = await service.ProcessRefreshTokenAsync(value, TestConstants.AdminClientId);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.NotNull(result.RefreshToken);
    }
}
