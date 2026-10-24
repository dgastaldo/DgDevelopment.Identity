namespace DgDevelopment.Identity.Server.UnitTests.OAuth;

using System.Security.Cryptography;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

public sealed class KeyMaterialServiceTests : IClassFixture<DatabaseFixture<KeyMaterialServiceTests>>
{
    private readonly DatabaseFixture<KeyMaterialServiceTests> _fixture;

    public KeyMaterialServiceTests(DatabaseFixture<KeyMaterialServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static async Task DeactivateAllActiveKeysAsync(SigningKeyRepository repo)
    {
        foreach (var key in await repo.GetActiveKeysAsync())
            await repo.DeactivateAsync(key.Id);
    }

    private static async Task<SigningKey> AddActiveKeyAsync(SigningKeyRepository repo)
    {
        using var rsa = RSA.Create(2048);
        var key = new SigningKey($"kid-{Guid.NewGuid():N}", SecurityAlgorithms.RsaSha256, rsa.ExportRSAPrivateKeyPem(), rsa.ExportRSAPublicKeyPem(), DateTime.UtcNow.AddDays(90));
        await repo.AddAsync(key);
        return key;
    }

    [Fact]
    public async Task GetSigningCredentialsAsyncReturnsCredentialsWithActiveKey()
    {
        await using var context = _fixture.CreateContext();
        var repo = new SigningKeyRepository(context);
        await DeactivateAllActiveKeysAsync(repo);
        using var service = new KeyMaterialService(repo);

        var credentials = await service.GetSigningCredentialsAsync();

        Assert.Equal(SecurityAlgorithms.RsaSha256, credentials.Algorithm);
        Assert.False(string.IsNullOrWhiteSpace(credentials.Key.KeyId));
        Assert.NotEmpty(await repo.GetActiveKeysAsync());
    }

    [Fact]
    public async Task GetSigningCredentialsAsyncReusesExistingActiveKeyWithoutRotating()
    {
        await using var context = _fixture.CreateContext();
        var repo = new SigningKeyRepository(context);
        await DeactivateAllActiveKeysAsync(repo);
        var key = await AddActiveKeyAsync(repo);
        using var service = new KeyMaterialService(repo);

        var credentials = await service.GetSigningCredentialsAsync();

        Assert.Equal(key.Id, credentials.Key.KeyId);
        Assert.Single(await repo.GetActiveKeysAsync());
    }

    [Fact]
    public async Task RotateKeysAsyncDeactivatesPreviousActiveKeys()
    {
        await using var context = _fixture.CreateContext();
        var repo = new SigningKeyRepository(context);
        await DeactivateAllActiveKeysAsync(repo);
        var oldKey = await AddActiveKeyAsync(repo);
        using var service = new KeyMaterialService(repo);

        await service.RotateKeysAsync();

        var active = await repo.GetActiveKeysAsync();
        Assert.Single(active);
        Assert.NotEqual(oldKey.Id, Assert.Single(active).Id);
    }

    [Fact]
    public async Task GetJwksDocumentAsyncReturnsActiveKey()
    {
        await using var context = _fixture.CreateContext();
        var repo = new SigningKeyRepository(context);
        await DeactivateAllActiveKeysAsync(repo);
        using var service = new KeyMaterialService(repo);
        await service.GetSigningCredentialsAsync();

        var jwks = await service.GetJwksDocumentAsync();

        var jwk = Assert.Single(jwks.Keys);
        Assert.Equal("RSA", jwk.Kty);
        Assert.False(string.IsNullOrWhiteSpace(jwk.N));
        Assert.False(string.IsNullOrWhiteSpace(jwk.E));
        Assert.False(string.IsNullOrWhiteSpace(jwk.KeyId));
    }

    [Fact]
    public async Task GetJwksDocumentAsyncExcludesInactiveKeys()
    {
        await using var context = _fixture.CreateContext();
        var repo = new SigningKeyRepository(context);
        await DeactivateAllActiveKeysAsync(repo);
        using var service = new KeyMaterialService(repo);

        var empty = await service.GetJwksDocumentAsync();

        Assert.Empty(empty.Keys);

        await service.GetSigningCredentialsAsync();

        var populated = await service.GetJwksDocumentAsync();

        Assert.Single(populated.Keys);
    }
}