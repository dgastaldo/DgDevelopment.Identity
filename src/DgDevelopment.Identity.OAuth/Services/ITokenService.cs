namespace DgDevelopment.Identity.OAuth.Services;

public interface ITokenService
{
    Task<TokenResponse> ProcessAuthorizationCodeAsync(string code, string codeVerifier, string clientId, Uri redirectUri, CancellationToken ct = default);
    Task<TokenResponse> ProcessClientCredentialsAsync(ClientValidationResult client, string[] scopes, CancellationToken ct = default);
    Task<TokenResponse> ProcessRefreshTokenAsync(string refreshToken, string clientId, CancellationToken ct = default);
    Task<TokenResponse> ProcessDeviceCodeAsync(string deviceCode, string clientId, CancellationToken ct = default);
}
