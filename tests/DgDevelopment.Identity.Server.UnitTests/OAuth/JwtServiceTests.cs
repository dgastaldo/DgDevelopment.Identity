namespace DgDevelopment.Identity.Server.UnitTests.OAuth;

using System.IdentityModel.Tokens.Jwt;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

public sealed class JwtServiceTests : IClassFixture<DatabaseFixture<JwtServiceTests>>
{
    private readonly DatabaseFixture<JwtServiceTests> _fixture;

    public JwtServiceTests(DatabaseFixture<JwtServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private const string Issuer = "https://idp.test.local";

    private static readonly Uri IssuerUri = new(Issuer);

    private sealed class FakeIssuerProvider : IOidcIssuerProvider
    {
        public Uri GetIssuer() => IssuerUri;
    }

    private static JwtService CreateService(IdentityDbContext context, KeyMaterialService keyMaterial)
        => new(keyMaterial, new FakeIssuerProvider());

    private static User CreateUser()
        => new($"jwt-{Guid.NewGuid():N}", "hash", EmailAddress.FromString("jwt@example.com"));

    private static async Task<Client> GetSeededClientAsync(IdentityDbContext context)
        => (await new ClientRepository(context).GetByClientIdAsync(TestConstants.AdminClientId))!;

    private static async Task<TokenValidationParameters> CreateValidationParametersAsync(KeyMaterialService keyMaterial)
    {
        var credentials = await keyMaterial.GetSigningCredentialsAsync();

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = TestConstants.AdminClientId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            IssuerSigningKey = credentials.Key,
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    }

    [Fact]
    public async Task CreateIdTokenAsyncIncludesOidcClaims()
    {
        await using var context = _fixture.CreateContext();
        var client = await GetSeededClientAsync(context);
        var user = CreateUser();
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var token = await service.CreateIdTokenAsync(new(client.TenantId, user, client, ["openid", "profile"], "nonce-123", ["pwd", "otp"], "session-1"));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(Issuer, jwt.Issuer);
        Assert.Equal(TestConstants.AdminClientId, jwt.Audiences.Single());
        Assert.Equal("jwt@example.com".ToUpperInvariant(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("true", jwt.Claims.First(c => c.Type == "email_verified").Value);
        Assert.Equal(user.Username, jwt.Claims.First(c => c.Type == "name").Value);
        Assert.Equal("nonce-123", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Nonce).Value);
        Assert.Equal("session-1", jwt.Claims.First(c => c.Type == "sid").Value);
        Assert.Equal(client.TenantId.ToString(), jwt.Claims.First(c => c.Type == "tid").Value);
        Assert.Contains("pwd", jwt.Claims.Where(c => c.Type == "amr").Select(c => c.Value));
        Assert.Contains("otp", jwt.Claims.Where(c => c.Type == "amr").Select(c => c.Value));
        Assert.NotNull(jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.AuthTime));
    }

    [Fact]
    public async Task CreateIdTokenAsyncOmitsNonceWhenNotProvided()
    {
        await using var context = _fixture.CreateContext();
        var client = await GetSeededClientAsync(context);
        var user = CreateUser();
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var token = await service.CreateIdTokenAsync(new(client.TenantId, user, client, ["openid"], null, ["pwd"], "session-1"));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Nonce);
    }

    [Fact]
    public async Task CreateAccessTokenAsyncIncludesClientScopePermissionsAndJti()
    {
        await using var context = _fixture.CreateContext();
        var client = await GetSeededClientAsync(context);
        var user = CreateUser();
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var token = await service.CreateAccessTokenAsync(new(client.TenantId, user, client, ["openid", "profile"], ["user:read", "role:read"]));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(Issuer, jwt.Issuer);
        Assert.Equal(TestConstants.AdminClientId, jwt.Audiences.Single());
        Assert.Equal(TestConstants.AdminClientId, jwt.Claims.First(c => c.Type == "client_id").Value);
        Assert.Equal("openid profile", jwt.Claims.First(c => c.Type == "scope").Value);
        Assert.Equal(["user:read", "role:read"], jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value));
        Assert.NotNull(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti));
        Assert.Equal(client.TenantId.ToString(), jwt.Claims.First(c => c.Type == "tid").Value);
    }

    [Fact]
    public async Task CreateAccessTokenAsyncOmitsPermissionsWhenNoneProvided()
    {
        await using var context = _fixture.CreateContext();
        var client = await GetSeededClientAsync(context);
        var user = CreateUser();
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var token = await service.CreateAccessTokenAsync(new(client.TenantId, user, client, ["openid"], null));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == "permission");
    }

    [Fact]
    public async Task ValidateTokenAsyncAcceptsSignedIdToken()
    {
        await using var context = _fixture.CreateContext();
        var client = await GetSeededClientAsync(context);
        var user = CreateUser();
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var token = await service.CreateIdTokenAsync(new(client.TenantId, user, client, ["openid"], null, ["pwd"], "session-1"));
        var parameters = await CreateValidationParametersAsync(keyMaterial);

        var principal = await service.ValidateTokenAsync(token, parameters);

        Assert.NotNull(principal.Identity);
        Assert.Equal(user.Username, principal.Identity.Name);
        Assert.Contains(principal.Claims, c => c.Value == user.Id.ToString());
    }
}