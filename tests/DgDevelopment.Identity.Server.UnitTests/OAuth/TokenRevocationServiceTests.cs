namespace DgDevelopment.Identity.Server.UnitTests.OAuth;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class TokenRevocationServiceTests : IClassFixture<DatabaseFixture<TokenRevocationServiceTests>>
{
    private readonly DatabaseFixture<TokenRevocationServiceTests> _fixture;

    public TokenRevocationServiceTests(DatabaseFixture<TokenRevocationServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Hash(string value)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static TokenRevocationService CreateService(IdentityDbContext context, KeyMaterialService keyMaterial)
        => new(
            new RefreshTokenRepository(context),
            new RevokedTokenRepository(context),
            new JwtService(keyMaterial, new FakeIssuerProvider()),
            keyMaterial,
            new FakeIssuerProvider());

    private static TokenIntrospectionService CreateIntrospectionService(IdentityDbContext context, KeyMaterialService keyMaterial)
        => new(
            new RefreshTokenRepository(context),
            new RevokedTokenRepository(context),
            new ClientRepository(context),
            new JwtService(keyMaterial, new FakeIssuerProvider()),
            keyMaterial,
            new FakeIssuerProvider());

    private static async Task<string> CreateAccessTokenAsync(IdentityDbContext context, KeyMaterialService keyMaterial, Client client, User user)
    {
        var jwt = new JwtService(keyMaterial, new FakeIssuerProvider());
        return await jwt.CreateAccessTokenAsync(new AccessTokenRequest(user, client, ["openid", "profile"], null));
    }

    private static Client CreateOtherClient(string clientId, string secret)
    {
        var client = new Client(clientId, Hash(secret), $"Other {clientId}", ClientType.Confidential);
        client.AddGrantType("client_credentials");
        client.AddScope("openid");
        return client;
    }

    private sealed class FakeIssuerProvider : IOidcIssuerProvider
    {
        public Uri GetIssuer() => new("https://idp.test.local");
    }

    [Fact]
    public async Task RevokeAsyncRevokesRefreshToken()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();

        await using var context = _fixture.CreateContext();
        var sessionRepo = new UserSessionRepository(context);
        var session = new UserSession(userId, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddHours(8), ["pwd"]);
        await sessionRepo.AddAsync(session);

        var value = $"rt-{Guid.NewGuid():N}";
        var refreshRepo = new RefreshTokenRepository(context);
        var stored = new RefreshToken(Hash(value), clientId, userId, session.Id, ["openid"]);
        await refreshRepo.AddAsync(stored);

        var clientRepo = new ClientRepository(context);
        var client = await clientRepo.GetByClientIdAsync(TestConstants.AdminClientId);
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        await service.RevokeAsync(new ClientValidationResult(true, client, null), value);

        var after = await refreshRepo.GetByTokenHashAsync(Hash(value));
        Assert.NotNull(after);
        Assert.True(after.IsRevoked);
    }

    [Fact]
    public async Task RevokeAsyncRevokesRefreshTokenFamily()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();

        await using var context = _fixture.CreateContext();
        var sessionRepo = new UserSessionRepository(context);
        var session = new UserSession(userId, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddHours(8), ["pwd"]);
        await sessionRepo.AddAsync(session);

        var firstValue = $"rt-{Guid.NewGuid():N}";
        var secondValue = $"rt-{Guid.NewGuid():N}";
        var refreshRepo = new RefreshTokenRepository(context);
        var first = new RefreshToken(Hash(firstValue), clientId, userId, session.Id, ["openid"]);
        await refreshRepo.AddAsync(first);
        var second = new RefreshToken(Hash(secondValue), clientId, userId, session.Id, ["openid"], first.Id);
        await refreshRepo.AddAsync(second);

        var clientRepo = new ClientRepository(context);
        var client = await clientRepo.GetByClientIdAsync(TestConstants.AdminClientId);
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        await service.RevokeAsync(new ClientValidationResult(true, client, null), secondValue);

        var firstAfter = await refreshRepo.GetByTokenHashAsync(Hash(firstValue));
        var secondAfter = await refreshRepo.GetByTokenHashAsync(Hash(secondValue));
        Assert.NotNull(firstAfter);
        Assert.True(firstAfter.IsRevoked);
        Assert.NotNull(secondAfter);
        Assert.True(secondAfter.IsRevoked);
    }

    [Fact]
    public async Task RevokeAsyncDoesNotRevokeForeignRefreshToken()
    {
        var userId = await _fixture.GetSeededUserIdAsync();

        await using var context = _fixture.CreateContext();
        var sessionRepo = new UserSessionRepository(context);
        var session = new UserSession(userId, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddHours(8), ["pwd"]);
        await sessionRepo.AddAsync(session);

        var clientRepo = new ClientRepository(context);
        var otherClient = CreateOtherClient($"other-{Guid.NewGuid():N}", "secret");
        await clientRepo.AddAsync(otherClient);

        var value = $"rt-{Guid.NewGuid():N}";
        var refreshRepo = new RefreshTokenRepository(context);
        var stored = new RefreshToken(Hash(value), otherClient.Id, userId, session.Id, ["openid"]);
        await refreshRepo.AddAsync(stored);

        var adminClient = await clientRepo.GetByClientIdAsync(TestConstants.AdminClientId);
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        await service.RevokeAsync(new ClientValidationResult(true, adminClient, null), value);

        var after = await refreshRepo.GetByTokenHashAsync(Hash(value));
        Assert.NotNull(after);
        Assert.False(after.IsRevoked);
    }

    [Fact]
    public async Task RevokeAsyncDenylistsAccessToken()
    {
        await using var context = _fixture.CreateContext();
        var clientRepo = new ClientRepository(context);
        var userRepo = new UserRepository(context);
        var client = await clientRepo.GetByClientIdAsync(TestConstants.AdminClientId);
        var userId = await _fixture.GetSeededUserIdAsync();
        var user = await userRepo.GetByIdAsync(userId);

        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);
        var token = await CreateAccessTokenAsync(context, keyMaterial, client!, user!);
        var jti = new JwtSecurityTokenHandler().ReadJwtToken(token).Id;

        await service.RevokeAsync(new ClientValidationResult(true, client, null), token, "access_token");

        var revokedRepo = new RevokedTokenRepository(context);
        var exists = await revokedRepo.ExistsAsync(Hash(jti!));
        Assert.True(exists);

        var introspection = CreateIntrospectionService(context, keyMaterial);
        var response = await introspection.IntrospectAsync(token);
        Assert.False(response.Active);
    }

    [Fact]
    public async Task RevokeAsyncDoesNotDenylistForeignAccessToken()
    {
        await using var context = _fixture.CreateContext();
        var clientRepo = new ClientRepository(context);
        var userRepo = new UserRepository(context);
        var otherClient = CreateOtherClient($"other-{Guid.NewGuid():N}", "secret");
        await clientRepo.AddAsync(otherClient);
        var userId = await _fixture.GetSeededUserIdAsync();
        var user = await userRepo.GetByIdAsync(userId);

        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);
        var token = await CreateAccessTokenAsync(context, keyMaterial, otherClient, user!);
        var jti = new JwtSecurityTokenHandler().ReadJwtToken(token).Id;

        var adminClient = await clientRepo.GetByClientIdAsync(TestConstants.AdminClientId);
        await service.RevokeAsync(new ClientValidationResult(true, adminClient, null), token, "access_token");

        var revokedRepo = new RevokedTokenRepository(context);
        var exists = await revokedRepo.ExistsAsync(Hash(jti!));
        Assert.False(exists);
    }

    [Fact]
    public async Task RevokeAsyncIgnoresUnknownToken()
    {
        await using var context = _fixture.CreateContext();
        var clientRepo = new ClientRepository(context);
        var adminClient = await clientRepo.GetByClientIdAsync(TestConstants.AdminClientId);
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        await service.RevokeAsync(new ClientValidationResult(true, adminClient, null), "unknown-token");

        var revokedRepo = new RevokedTokenRepository(context);
        var count = context.RevokedTokens.Count();
        Assert.Equal(0, count);
        Assert.False(await revokedRepo.ExistsAsync(Hash("unknown-token")));
    }

    [Fact]
    public async Task RevokeAsyncThrowsForMissingToken()
    {
        await using var context = _fixture.CreateContext();
        var clientRepo = new ClientRepository(context);
        var adminClient = await clientRepo.GetByClientIdAsync(TestConstants.AdminClientId);
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RevokeAsync(new ClientValidationResult(true, adminClient, null), "  "));

        Assert.Equal("Token is missing.", exception.Message);
    }
}