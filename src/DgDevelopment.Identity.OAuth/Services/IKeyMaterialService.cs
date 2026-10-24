namespace DgDevelopment.Identity.OAuth.Services;

using Microsoft.IdentityModel.Tokens;

public interface IKeyMaterialService
{
    Task<SigningCredentials> GetSigningCredentialsAsync(CancellationToken ct = default);
    Task<JsonWebKeySet> GetJwksDocumentAsync(CancellationToken ct = default);
    Task RotateKeysAsync(CancellationToken ct = default);
}
