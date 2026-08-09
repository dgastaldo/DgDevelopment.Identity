namespace DgDevelopment.Identity.OAuth.Services;

using DgDevelopment.Identity.Domain.Entities;

public sealed record AuthorizationRequest(
    string ClientId,
    string RedirectUri,
    string ResponseType,
    string Scope,
    string? State,
    string? Nonce,
    string? CodeChallenge,
    string? CodeChallengeMethod);

public sealed record AuthorizationResult(
    bool IsValid,
    Client? Client,
    string? Error,
    string? ErrorDescription,
    string? RedirectUri);

public sealed record TokenRequest(
    string GrantType,
    string? Code,
    string? RedirectUri,
    string? ClientId,
    string? ClientSecret,
    string? CodeVerifier,
    string? RefreshToken,
    string? DeviceCode,
    string? Scope);

public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string? IdToken,
    string? RefreshToken,
    string Scope);

public sealed record ClientValidationResult(
    bool IsValid,
    Client? Client,
    string? ErrorDescription);

public sealed record IdTokenRequest(
    User User,
    Client Client,
    string[] Scopes,
    string? Nonce,
    string[] AuthMethods,
    string SessionId);

public sealed record AccessTokenRequest(
    User User,
    Client Client,
    string[] Scopes,
    string[]? Permissions,
    int LifetimeSeconds = 3600);

public sealed record DiscoveryDocument(
    string Issuer,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string UserInfoEndpoint,
    string EndSessionEndpoint,
    string JwksUri,
    string DeviceAuthorizationEndpoint,
    string IntrospectionEndpoint,
    string RevocationEndpoint,
    string[] ScopesSupported,
    string[] GrantTypesSupported,
    string[] CodeChallengeMethodsSupported);
