namespace DgDevelopment.Identity.Server.UnitTests.OAuth;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class TokenIntrospectionServiceTests : IClassFixture<DatabaseFixture<TokenIntrospectionServiceTests>>
{
    private readonly DatabaseFixture<TokenIntrospectionServiceTests> _fixture;

    public TokenIntrospectionServiceTests(DatabaseFixture<TokenIntrospectionServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Hash(string value)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static TokenIntrospectionService CreateService(IdentityDbContext context, KeyMaterialService keyMaterial)
        => new(
            new RefreshTokenRepository(context),
            new RevokedTokenRepository(context),
            new ClientRepository(context),
            new JwtService(keyMaterial, new FakeIssuerProvider()),
            keyMaterial,
            new FakeIssuerProvider());

    private static async Task<string> CreateAccessTokenAsync(IdentityDbContext context, KeyMaterialService keyMaterial, Client client, User user, int lifetimeSeconds = 3600)
    {
        var jwt = new JwtService(keyMaterial, new FakeIssuerProvider());
        return await jwt.CreateAccessTokenAsync(new AccessTokenRequest(client.TenantId, user, client, ["openid", "profile"], null, lifetimeSeconds));
    }

    private sealed class FakeIssuerProvider : IOidcIssuerProvider
    {
        public Uri GetIssuer() => new("https://idp.test.local");
    }

    [Fact]
    public async Task IntrospectAsyncReturnsActiveForValidRefreshToken()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();

        await using var context = _fixture.CreateContext();
        var sessionRepo = new UserSessionRepository(context);
        var session = new UserSession(userId, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddHours(8), ["pwd"]);
        await sessionRepo.AddAsync(session);

        var value = $"rt-{Guid.NewGuid():N}";
        var refreshRepo = new RefreshTokenRepository(context);
        await refreshRepo.AddAsync(new RefreshToken(tenantId, Hash(value), clientId, userId, session.Id, ["openid", "profile"]));
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var response = await service.IntrospectAsync(value);

        Assert.True(response.Active);
        Assert.Equal("refresh_token", response.TokenType);
        Assert.Equal("openid profile", response.Scope);
        Assert.Equal(TestConstants.AdminClientId, response.ClientId);
        Assert.Equal(userId.ToString(), response.Sub);
        Assert.True(response.Exp > 0);
        Assert.True(response.Iat > 0);
    }

    [Fact]
    public async Task IntrospectAsyncReturnsInactiveForRevokedRefreshToken()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();

        await using var context = _fixture.CreateContext();
        var sessionRepo = new UserSessionRepository(context);
        var session = new UserSession(userId, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddHours(8), ["pwd"]);
        await sessionRepo.AddAsync(session);

        var value = $"rt-{Guid.NewGuid():N}";
        var refreshRepo = new RefreshTokenRepository(context);
        var stored = new RefreshToken(tenantId, Hash(value), clientId, userId, session.Id, ["openid"]);
        await refreshRepo.AddAsync(stored);
        await refreshRepo.RevokeAsync(stored.Id);
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var response = await service.IntrospectAsync(value);

        Assert.False(response.Active);
    }

    [Fact]
    public async Task IntrospectAsyncReturnsInactiveForUnknownToken()
    {
        await using var context = _fixture.CreateContext();
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var response = await service.IntrospectAsync(Guid.NewGuid().ToString("N"));

        Assert.False(response.Active);
    }

    [Fact]
    public async Task IntrospectAsyncReturnsActiveForValidAccessToken()
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

        var response = await service.IntrospectAsync(token);

        Assert.True(response.Active);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal("openid profile", response.Scope);
        Assert.Equal(TestConstants.AdminClientId, response.ClientId);
        Assert.Equal(userId.ToString(), response.Sub);
        Assert.NotNull(response.Jti);
    }

    [Fact]
    public async Task IntrospectAsyncReturnsInactiveForExpiredAccessToken()
    {
        await using var context = _fixture.CreateContext();
        var clientRepo = new ClientRepository(context);
        var userRepo = new UserRepository(context);
        var client = await clientRepo.GetByClientIdAsync(TestConstants.AdminClientId);
        var userId = await _fixture.GetSeededUserIdAsync();

        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);
        var credentials = await keyMaterial.GetSigningCredentialsAsync();
        var handler = new JwtSecurityTokenHandler();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString(null, CultureInfo.InvariantCulture)),
            new("client_id", TestConstants.AdminClientId),
            new("scope", "openid profile"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        var token = handler.WriteToken(new JwtSecurityToken(
            issuer: "https://idp.test.local",
            audience: TestConstants.AdminClientId,
            claims: claims,
            notBefore: null,
            expires: DateTime.UtcNow.AddSeconds(-1),
            signingCredentials: credentials));

        var response = await service.IntrospectAsync(token);

        Assert.False(response.Active);
        Assert.Equal("openid profile", response.Scope);
        Assert.NotNull(response.Jti);
    }

    [Fact]
    public async Task IntrospectAsyncReturnsInactiveForDenylistedAccessToken()
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

        var revokedRepo = new RevokedTokenRepository(context);
        await revokedRepo.AddAsync(new RevokedToken(jti!, "access_token", client!.Id, userId, DateTime.UtcNow.AddHours(1)));

        var response = await service.IntrospectAsync(token);

        Assert.False(response.Active);
    }

    [Fact]
    public async Task IntrospectAsyncThrowsForMissingToken()
    {
        await using var context = _fixture.CreateContext();
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.IntrospectAsync("  "));

        Assert.Equal("Token is missing.", exception.Message);
    }

    [Fact]
    public async Task IntrospectAsyncHonorsAccessTokenHintForRefreshToken()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();

        await using var context = _fixture.CreateContext();
        var sessionRepo = new UserSessionRepository(context);
        var session = new UserSession(userId, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddHours(8), ["pwd"]);
        await sessionRepo.AddAsync(session);

        var value = $"rt-{Guid.NewGuid():N}";
        var refreshRepo = new RefreshTokenRepository(context);
        await refreshRepo.AddAsync(new RefreshToken(tenantId, Hash(value), clientId, userId, session.Id, ["openid"]));
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var response = await service.IntrospectAsync(value, "access_token");

        Assert.False(response.Active);
    }
}