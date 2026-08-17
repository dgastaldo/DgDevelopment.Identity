namespace DgDevelopment.Identity.UnitTests.OAuth;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using DgDevelopment.Identity.UnitTests.Testing;
using Xunit;

public sealed class TokenServiceTests : IClassFixture<DatabaseFixture<TokenServiceTests>>
{
    private readonly DatabaseFixture<TokenServiceTests> _fixture;

    public TokenServiceTests(DatabaseFixture<TokenServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static readonly Uri SeededRedirect = new(TestConstants.AdminClientRedirectUri);

    private static string Hash(string value)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ComputePkceChallenge(string verifier)
        => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');

    private static Client CreateOtherClient(string clientId, string secret, params string[] scopes)
    {
        var client = new Client(clientId, Hash(secret), $"Other {clientId}", ClientType.Confidential);
        client.AddGrantType("authorization_code");
        foreach (var scope in scopes)
            client.AddScope(scope);
        return client;
    }

    private static TokenService CreateService(IdentityDbContext context, KeyMaterialService keyMaterial)
        => new(
            new AuthorizationCodeRepository(context),
            new RefreshTokenRepository(context),
            new DeviceCodeRepository(context),
            new ClientRepository(context),
            new UserRepository(context),
            new UserSessionRepository(context),
            new JwtService(keyMaterial, new FakeIssuerProvider()));

    private sealed class FakeIssuerProvider : IOidcIssuerProvider
    {
        public Uri GetIssuer() => new("https://idp.test.local");
    }

    [Fact]
    public async Task ProcessAuthorizationCodeAsyncReturnsTokensForValidCode()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var code = $"code-{Guid.NewGuid():N}";
        var verifier = $"verifier-{Guid.NewGuid():N}";

        await using var context = _fixture.CreateContext();
        var codeRepo = new AuthorizationCodeRepository(context);
        await codeRepo.AddAsync(new AuthorizationCode(Hash(code), clientId, userId, SeededRedirect, ["openid", "profile"],
            ComputePkceChallenge(verifier), "S256"));
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var result = await service.ProcessAuthorizationCodeAsync(code, verifier, TestConstants.AdminClientId, SeededRedirect);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.IdToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(3600, result.ExpiresIn);
        Assert.Equal("openid profile", result.Scope);

        var stored = await codeRepo.GetByCodeHashAsync(Hash(code));
        Assert.NotNull(stored);
        Assert.True(stored.IsUsed);
    }

    [Fact]
    public async Task ProcessAuthorizationCodeAsyncRejectsInvalidCode()
    {
        await using var context = _fixture.CreateContext();
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessAuthorizationCodeAsync("unknown-code", "verifier", TestConstants.AdminClientId, SeededRedirect));

        Assert.Equal("Invalid authorization code.", exception.Message);
    }

    [Fact]
    public async Task ProcessAuthorizationCodeAsyncRejectsUsedCode()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var code = $"code-{Guid.NewGuid():N}";

        await using var context = _fixture.CreateContext();
        var codeRepo = new AuthorizationCodeRepository(context);
        var stored = new AuthorizationCode(Hash(code), clientId, userId, SeededRedirect, ["openid"]);
        await codeRepo.AddAsync(stored);
        await codeRepo.MarkAsUsedAsync(stored.Id);
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessAuthorizationCodeAsync(code, "verifier", TestConstants.AdminClientId, SeededRedirect));

        Assert.Equal("Authorization code has expired or was already used.", exception.Message);
    }

    [Fact]
    public async Task ProcessAuthorizationCodeAsyncRejectsExpiredCode()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var code = $"code-{Guid.NewGuid():N}";

        await using var context = _fixture.CreateContext();
        var codeRepo = new AuthorizationCodeRepository(context);
        await codeRepo.AddAsync(new AuthorizationCode(Hash(code), clientId, userId, SeededRedirect, ["openid"], lifetimeSeconds: 0));
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessAuthorizationCodeAsync(code, "verifier", TestConstants.AdminClientId, SeededRedirect));

        Assert.Equal("Authorization code has expired or was already used.", exception.Message);
    }

    [Fact]
    public async Task ProcessAuthorizationCodeAsyncRejectsClientMismatch()
    {
        var userId = await _fixture.GetSeededUserIdAsync();
        var code = $"code-{Guid.NewGuid():N}";

        await using var context = _fixture.CreateContext();
        var clientRepo = new ClientRepository(context);
        var otherClient = CreateOtherClient($"other-{Guid.NewGuid():N}", "secret", "openid");
        await clientRepo.AddAsync(otherClient);
        var codeRepo = new AuthorizationCodeRepository(context);
        await codeRepo.AddAsync(new AuthorizationCode(Hash(code), otherClient.Id, userId, SeededRedirect, ["openid"]));
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessAuthorizationCodeAsync(code, "verifier", TestConstants.AdminClientId, SeededRedirect));

        Assert.Equal("Client mismatch.", exception.Message);
    }

    [Fact]
    public async Task ProcessAuthorizationCodeAsyncRejectsRedirectMismatch()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var code = $"code-{Guid.NewGuid():N}";
        var otherRedirect = new Uri("https://other.example/callback");

        await using var context = _fixture.CreateContext();
        var codeRepo = new AuthorizationCodeRepository(context);
        await codeRepo.AddAsync(new AuthorizationCode(Hash(code), clientId, userId, otherRedirect, ["openid"]));
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessAuthorizationCodeAsync(code, "verifier", TestConstants.AdminClientId, SeededRedirect));

        Assert.Equal("Redirect URI mismatch.", exception.Message);
    }

    [Fact]
    public async Task ProcessAuthorizationCodeAsyncRejectsInvalidVerifier()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var code = $"code-{Guid.NewGuid():N}";

        await using var context = _fixture.CreateContext();
        var codeRepo = new AuthorizationCodeRepository(context);
        await codeRepo.AddAsync(new AuthorizationCode(Hash(code), clientId, userId, SeededRedirect, ["openid"],
            ComputePkceChallenge("original-verifier"), "S256"));
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessAuthorizationCodeAsync(code, "wrong-verifier", TestConstants.AdminClientId, SeededRedirect));

        Assert.Equal("Invalid code_verifier.", exception.Message);
    }

    [Fact]
    public async Task ProcessRefreshTokenAsyncRotatesTokenAndRevokesPrevious()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();

        await using var context = _fixture.CreateContext();
        var sessionRepo = new UserSessionRepository(context);
        var session = new UserSession(userId, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddHours(8), ["pwd"]);
        await sessionRepo.AddAsync(session);

        var oldValue = $"rt-{Guid.NewGuid():N}";
        var refreshRepo = new RefreshTokenRepository(context);
        var oldToken = new RefreshToken(Hash(oldValue), clientId, userId, session.Id, ["openid", "profile"]);
        await refreshRepo.AddAsync(oldToken);
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var result = await service.ProcessRefreshTokenAsync(oldValue, TestConstants.AdminClientId);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.NotNull(result.RefreshToken);
        Assert.NotEqual(oldValue, result.RefreshToken);
        Assert.Equal("openid profile", result.Scope);

        var storedOld = await refreshRepo.GetByTokenHashAsync(Hash(oldValue));
        Assert.NotNull(storedOld);
        Assert.True(storedOld.IsRevoked);

        var storedNew = await refreshRepo.GetByTokenHashAsync(Hash(result.RefreshToken));
        Assert.NotNull(storedNew);
        Assert.False(storedNew.IsRevoked);
        Assert.Equal(oldToken.Id, storedNew.PreviousTokenId);
    }

    [Fact]
    public async Task ProcessRefreshTokenAsyncRejectsUnknownToken()
    {
        await using var context = _fixture.CreateContext();
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessRefreshTokenAsync("unknown-token", TestConstants.AdminClientId));

        Assert.Equal("Invalid refresh token.", exception.Message);
    }

    [Fact]
    public async Task ProcessRefreshTokenAsyncRejectsRevokedToken()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();

        await using var context = _fixture.CreateContext();
        var sessionRepo = new UserSessionRepository(context);
        var session = new UserSession(userId, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddHours(8), ["pwd"]);
        await sessionRepo.AddAsync(session);

        var oldValue = $"rt-{Guid.NewGuid():N}";
        var refreshRepo = new RefreshTokenRepository(context);
        var oldToken = new RefreshToken(Hash(oldValue), clientId, userId, session.Id, ["openid"]);
        await refreshRepo.AddAsync(oldToken);
        await refreshRepo.RevokeAsync(oldToken.Id);
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessRefreshTokenAsync(oldValue, TestConstants.AdminClientId));

        Assert.Equal("Refresh token has expired or was revoked.", exception.Message);
    }

    [Fact]
    public async Task ProcessDeviceCodeAsyncReturnsTokensForAuthorizedDevice()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var deviceCodeValue = $"dc-{Guid.NewGuid():N}";

        await using var context = _fixture.CreateContext();
        var deviceRepo = new DeviceCodeRepository(context);
        var stored = new DeviceCode(Hash(deviceCodeValue), Hash($"uc-{Guid.NewGuid():N}"), clientId, ["openid"]);
        await deviceRepo.AddAsync(stored);
        await deviceRepo.AuthorizeAsync(stored.Id, userId);
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var result = await service.ProcessDeviceCodeAsync(deviceCodeValue, TestConstants.AdminClientId);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.NotNull(result.RefreshToken);
        Assert.Equal("openid", result.Scope);

        var storedAfter = await deviceRepo.GetByDeviceCodeHashAsync(Hash(deviceCodeValue));
        Assert.NotNull(storedAfter);
        Assert.True(storedAfter.IsUsed);
    }

    [Fact]
    public async Task ProcessDeviceCodeAsyncRejectsUnknownDeviceCode()
    {
        await using var context = _fixture.CreateContext();
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<DeviceAuthorizationException>(() =>
            service.ProcessDeviceCodeAsync("unknown-device-code", TestConstants.AdminClientId));

        Assert.Equal("invalid_grant", exception.ErrorCode);
        Assert.Equal("Invalid device code.", exception.Message);
    }

    [Fact]
    public async Task ProcessDeviceCodeAsyncReturnsAuthorizationPendingWhenNotAuthorized()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var deviceCodeValue = $"dc-{Guid.NewGuid():N}";

        await using var context = _fixture.CreateContext();
        var deviceRepo = new DeviceCodeRepository(context);
        await deviceRepo.AddAsync(new DeviceCode(Hash(deviceCodeValue), Hash($"uc-{Guid.NewGuid():N}"), clientId, ["openid"]));
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<DeviceAuthorizationException>(() =>
            service.ProcessDeviceCodeAsync(deviceCodeValue, TestConstants.AdminClientId));

        Assert.Equal("authorization_pending", exception.ErrorCode);
    }

    [Fact]
    public async Task ProcessDeviceCodeAsyncRejectsExpiredDeviceCode()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var deviceCodeValue = $"dc-{Guid.NewGuid():N}";

        await using var context = _fixture.CreateContext();
        var deviceRepo = new DeviceCodeRepository(context);
        await deviceRepo.AddAsync(new DeviceCode(Hash(deviceCodeValue), Hash($"uc-{Guid.NewGuid():N}"), clientId, ["openid"], lifetimeSeconds: 0));
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);

        var exception = await Assert.ThrowsAsync<DeviceAuthorizationException>(() =>
            service.ProcessDeviceCodeAsync(deviceCodeValue, TestConstants.AdminClientId));

        Assert.Equal("expired_token", exception.ErrorCode);
    }

    [Fact]
    public async Task ProcessClientCredentialsAsyncReturnsAccessToken()
    {
        await using var context = _fixture.CreateContext();
        var clientRepo = new ClientRepository(context);
        var client = await clientRepo.GetByClientIdAsync(TestConstants.AdminClientId);
        using var keyMaterial = new KeyMaterialService(new SigningKeyRepository(context));
        var service = CreateService(context, keyMaterial);
        var validation = new ClientValidationResult(true, client, null);

        var result = await service.ProcessClientCredentialsAsync(validation, ["openid", "profile"]);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(3600, result.ExpiresIn);
        Assert.Null(result.IdToken);
        Assert.Null(result.RefreshToken);
        Assert.Equal("openid profile", result.Scope);
    }
}