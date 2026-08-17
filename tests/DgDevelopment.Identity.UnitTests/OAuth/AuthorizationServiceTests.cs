namespace DgDevelopment.Identity.UnitTests.OAuth;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using DgDevelopment.Identity.UnitTests.Testing;
using Xunit;

public sealed class AuthorizationServiceTests : IClassFixture<DatabaseFixture<AuthorizationServiceTests>>
{
    private readonly DatabaseFixture<AuthorizationServiceTests> _fixture;

    public AuthorizationServiceTests(DatabaseFixture<AuthorizationServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private const string ClientRedirectUri = "https://client.example/callback";

    private static string HashSecret(string secret)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static Client CreateClient(string clientId, string secret, ClientType clientType, string redirectUri, params string[] scopes)
    {
        var client = new Client(clientId, HashSecret(secret), $"Test {clientId}", clientType);
        client.AddGrantType("authorization_code");
        client.AddRedirectUri(new Uri(redirectUri));
        foreach (var scope in scopes)
            client.AddScope(scope);
        return client;
    }

    private static AuthorizationService CreateService(IdentityDbContext context)
        => new(new ClientRepository(context), new AuthorizationCodeRepository(context));

    private static AuthorizationRequest CreateRequest(string clientId, string redirectUri, string responseType = "code", string scope = "openid", string? codeChallenge = null, string? codeChallengeMethod = null)
        => new(clientId, redirectUri, responseType, scope, "state-123", "nonce-456", codeChallenge, codeChallengeMethod);

    [Fact]
    public async Task ValidateAsyncReturnsValidForSeededClientCodeFlow()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var result = await service.ValidateAsync(
            CreateRequest(TestConstants.AdminClientId, TestConstants.AdminClientRedirectUri, scope: "openid profile email", codeChallenge: "challenge-123", codeChallengeMethod: "S256"));

        Assert.True(result.IsValid);
        Assert.NotNull(result.Client);
        Assert.Equal(TestConstants.AdminClientId, result.Client.ClientId);
        Assert.Equal(TestConstants.AdminClientRedirectUri, result.RedirectUri);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ValidateAsyncReturnsInvalidRequestForMissingClientId()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var result = await service.ValidateAsync(CreateRequest("", ClientRedirectUri));

        Assert.False(result.IsValid);
        Assert.Equal("invalid_request", result.Error);
        Assert.Equal("Missing client_id.", result.ErrorDescription);
        Assert.Null(result.Client);
    }

    [Fact]
    public async Task ValidateAsyncReturnsInvalidClientForUnknownClient()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var result = await service.ValidateAsync(CreateRequest("unknown-client", ClientRedirectUri));

        Assert.False(result.IsValid);
        Assert.Equal("invalid_client", result.Error);
        Assert.Equal("Invalid client.", result.ErrorDescription);
        Assert.Null(result.Client);
    }

    [Fact]
    public async Task ValidateAsyncReturnsInvalidClientForDeactivatedClient()
    {
        await using var context = _fixture.CreateContext();
        var repo = new ClientRepository(context);
        var client = CreateClient($"deact-{Guid.NewGuid():N}", "secret", ClientType.Confidential, ClientRedirectUri, "openid");
        await repo.AddAsync(client);
        client.Deactivate();
        await repo.UpdateAsync(client);

        var service = CreateService(context);
        var result = await service.ValidateAsync(CreateRequest(client.ClientId, ClientRedirectUri));

        Assert.False(result.IsValid);
        Assert.Equal("invalid_client", result.Error);
        Assert.Equal("Invalid client.", result.ErrorDescription);
        Assert.Null(result.Client);
    }

    [Fact]
    public async Task ValidateAsyncReturnsUnsupportedResponseTypeForTokenResponse()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var result = await service.ValidateAsync(
            CreateRequest(TestConstants.AdminClientId, TestConstants.AdminClientRedirectUri, responseType: "token"));

        Assert.False(result.IsValid);
        Assert.Equal("unsupported_response_type", result.Error);
        Assert.Equal("Only 'code' response type is supported.", result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsyncReturnsInvalidScopeForDisallowedScope()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var result = await service.ValidateAsync(
            CreateRequest(TestConstants.AdminClientId, TestConstants.AdminClientRedirectUri, scope: "openid admin"));

        Assert.False(result.IsValid);
        Assert.Equal("invalid_scope", result.Error);
        Assert.Equal("Scope 'admin' not allowed.", result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsyncReturnsInvalidRequestForUnregisteredRedirect()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var result = await service.ValidateAsync(CreateRequest(TestConstants.AdminClientId, "https://evil.example/callback"));

        Assert.False(result.IsValid);
        Assert.Equal("invalid_request", result.Error);
        Assert.Equal("Invalid redirect_uri.", result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsyncRequiresPkceChallengeForPublicClient()
    {
        await using var context = _fixture.CreateContext();
        var repo = new ClientRepository(context);
        var client = CreateClient($"public-{Guid.NewGuid():N}", "secret", ClientType.Public, ClientRedirectUri, "openid");
        await repo.AddAsync(client);

        var service = CreateService(context);
        var result = await service.ValidateAsync(CreateRequest(client.ClientId, ClientRedirectUri));

        Assert.False(result.IsValid);
        Assert.Equal("invalid_request", result.Error);
        Assert.Equal("PKCE code_challenge is required.", result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsyncRejectsNonS256ChallengeMethod()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var result = await service.ValidateAsync(
            CreateRequest(TestConstants.AdminClientId, TestConstants.AdminClientRedirectUri, codeChallenge: "challenge-123", codeChallengeMethod: "plain"));

        Assert.False(result.IsValid);
        Assert.Equal("invalid_request", result.Error);
        Assert.Equal("Only S256 code_challenge_method is supported.", result.ErrorDescription);
    }

    [Fact]
    public async Task CreateAuthorizationCodeAsyncPersistsHashedCode()
    {
        var userId = await _fixture.GetSeededUserIdAsync();
        await using var context = _fixture.CreateContext();
        var clientRepo = new ClientRepository(context);
        var client = await clientRepo.GetByClientIdAsync(TestConstants.AdminClientId);
        var user = await new UserRepository(context).GetByIdAsync(userId);
        var codeRepo = new AuthorizationCodeRepository(context);
        var service = new AuthorizationService(clientRepo, codeRepo);

        var code = await service.CreateAuthorizationCodeAsync(client!, user!, ["openid", "profile"], TestConstants.AdminClientRedirectUri, "challenge-123", "S256");

        Assert.False(string.IsNullOrWhiteSpace(code));
        var codeHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        var stored = await codeRepo.GetByCodeHashAsync(codeHash);

        Assert.NotNull(stored);
        Assert.Equal(client!.Id, stored.ClientId);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal(["openid", "profile"], stored.GetScopes());
        Assert.Equal("challenge-123", stored.CodeChallengeHash);
        Assert.Equal("S256", stored.CodeChallengeMethod);
        Assert.False(stored.IsUsed);
    }
}