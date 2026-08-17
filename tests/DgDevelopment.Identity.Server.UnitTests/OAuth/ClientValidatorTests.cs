namespace DgDevelopment.Identity.Server.UnitTests.OAuth;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class ClientValidatorTests : IClassFixture<DatabaseFixture<ClientValidatorTests>>
{
    private readonly DatabaseFixture<ClientValidatorTests> _fixture;

    public ClientValidatorTests(DatabaseFixture<ClientValidatorTests> fixture)
    {
        _fixture = fixture;
    }

    private static string HashSecret(string secret)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static Client CreateClient(string clientId, string secret, ClientType clientType, params string[] grantTypes)
    {
        var client = new Client(clientId, HashSecret(secret), $"Test {clientId}", clientType);
        foreach (var grantType in grantTypes)
            client.AddGrantType(grantType);
        return client;
    }

    private static ClientValidator CreateValidator(IdentityDbContext context)
        => new(new ClientRepository(context));

    [Fact]
    public async Task ValidateAsyncReturnsValidForConfidentialClientWithCorrectSecret()
    {
        await using var context = _fixture.CreateContext();
        var validator = CreateValidator(context);

        var result = await validator.ValidateAsync(TestConstants.AdminClientId, TestConstants.AdminClientSecret, "authorization_code");

        Assert.True(result.IsValid);
        Assert.NotNull(result.Client);
        Assert.Equal(TestConstants.AdminClientId, result.Client.ClientId);
        Assert.Null(result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsyncReturnsFalseForMissingClientId()
    {
        await using var context = _fixture.CreateContext();
        var validator = CreateValidator(context);

        var result = await validator.ValidateAsync(null, null, "authorization_code");

        Assert.False(result.IsValid);
        Assert.Null(result.Client);
        Assert.Equal("Missing client_id.", result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsyncReturnsFalseForUnknownClient()
    {
        await using var context = _fixture.CreateContext();
        var validator = CreateValidator(context);

        var result = await validator.ValidateAsync("unknown-client", "secret", "authorization_code");

        Assert.False(result.IsValid);
        Assert.Null(result.Client);
        Assert.Equal("Invalid client_id.", result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsyncReturnsFalseForDeactivatedClient()
    {
        await using var context = _fixture.CreateContext();
        var repo = new ClientRepository(context);
        var client = CreateClient($"deact-{Guid.NewGuid():N}", "secret", ClientType.Confidential, "authorization_code");
        await repo.AddAsync(client);
        client.Deactivate();
        await repo.UpdateAsync(client);

        var validator = CreateValidator(context);
        var result = await validator.ValidateAsync(client.ClientId, "secret", "authorization_code");

        Assert.False(result.IsValid);
        Assert.Null(result.Client);
        Assert.Equal("Client is deactivated.", result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsyncReturnsFalseForDisallowedGrantType()
    {
        await using var context = _fixture.CreateContext();
        var validator = CreateValidator(context);

        var result = await validator.ValidateAsync(TestConstants.AdminClientId, TestConstants.AdminClientSecret, "password");

        Assert.False(result.IsValid);
        Assert.Null(result.Client);
        Assert.Equal("Grant type 'password' not allowed for this client.", result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsyncReturnsFalseForMissingSecret()
    {
        await using var context = _fixture.CreateContext();
        var validator = CreateValidator(context);

        var result = await validator.ValidateAsync(TestConstants.AdminClientId, null, "authorization_code");

        Assert.False(result.IsValid);
        Assert.Null(result.Client);
        Assert.Equal("Missing client_secret.", result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsyncReturnsFalseForInvalidSecret()
    {
        await using var context = _fixture.CreateContext();
        var validator = CreateValidator(context);

        var result = await validator.ValidateAsync(TestConstants.AdminClientId, "wrong-secret", "authorization_code");

        Assert.False(result.IsValid);
        Assert.Null(result.Client);
        Assert.Equal("Invalid client_secret.", result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsyncSkipsSecretCheckForPublicClient()
    {
        await using var context = _fixture.CreateContext();
        var repo = new ClientRepository(context);
        var client = CreateClient($"public-{Guid.NewGuid():N}", "secret", ClientType.Public, "authorization_code");
        await repo.AddAsync(client);

        var validator = CreateValidator(context);
        var result = await validator.ValidateAsync(client.ClientId, null, "authorization_code");

        Assert.True(result.IsValid);
        Assert.NotNull(result.Client);
        Assert.Equal(client.Id, result.Client.Id);
    }
}