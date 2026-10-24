namespace DgDevelopment.Identity.OAuth.Services;

using System.Security.Cryptography;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using Microsoft.IdentityModel.Tokens;

public sealed class KeyMaterialService(ISigningKeyRepository repository) : IKeyMaterialService, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public void Dispose() => _lock.Dispose();

    public async Task<SigningCredentials> GetSigningCredentialsAsync(CancellationToken ct = default)
    {
        var keys = await repository.GetActiveKeysAsync(ct).ConfigureAwait(false);
        var activeKey = keys.FirstOrDefault(k => k.IsActive && !k.IsExpired());

        if (activeKey == null)
        {
            await RotateKeysAsync(ct).ConfigureAwait(false);
            keys = await repository.GetActiveKeysAsync(ct).ConfigureAwait(false);
            activeKey = keys.First(k => k.IsActive);
        }

        return CreateCredentials(activeKey);
    }

    public async Task<JsonWebKeySet> GetJwksDocumentAsync(CancellationToken ct = default)
    {
        var keys = await repository.GetActiveKeysAsync(ct).ConfigureAwait(false);
        var jwks = new JsonWebKeySet();

        foreach (var key in keys.Where(k => k.IsActive && !k.IsExpired()))
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(key.PublicKeyData);
            var parameters = rsa.ExportParameters(false);
            var jwk = new JsonWebKey
            {
                Kty = JsonWebAlgorithmsKeyTypes.RSA,
                N = Base64UrlEncoder.Encode(parameters.Modulus),
                E = Base64UrlEncoder.Encode(parameters.Exponent),
                KeyId = key.Id,
                Alg = key.Algorithm
            };
            jwks.Keys.Add(jwk);
        }

        return jwks;
    }

    public async Task RotateKeysAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var rsa = RSA.Create(2048);
            var privateKey = rsa.ExportRSAPrivateKeyPem();
            var publicKey = rsa.ExportRSAPublicKeyPem();
            var kid = Guid.NewGuid().ToString("N")[..8];
            var expiresAt = DateTime.UtcNow.AddDays(90);

            var signingKey = new SigningKey(kid, SecurityAlgorithms.RsaSha256, privateKey, publicKey, expiresAt);
            await repository.AddAsync(signingKey, ct).ConfigureAwait(false);

            var oldKeys = await repository.GetActiveKeysAsync(ct).ConfigureAwait(false);
            foreach (var old in oldKeys.Where(k => k.Id != kid && k.IsActive))
                await repository.DeactivateAsync(old.Id, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static SigningCredentials CreateCredentials(SigningKey key)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(key.KeyData);
        var securityKey = new RsaSecurityKey(rsa) { KeyId = key.Id };
        return new SigningCredentials(securityKey, key.Algorithm);
    }
}
