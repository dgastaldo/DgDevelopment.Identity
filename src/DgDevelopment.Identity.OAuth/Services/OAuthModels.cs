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

public sealed record DeviceAuthorizationResponse(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    string VerificationUriComplete,
    int ExpiresIn,
    int Interval);

public sealed record DeviceApprovalInfo(
    Guid DeviceCodeId,
    string ClientName,
    IReadOnlyCollection<string> Scopes);

public sealed record ClientValidationResult(
    bool IsValid,
    Client? Client,
    string? ErrorDescription);

public sealed record IdTokenRequest(
    Guid TenantId,
    User User,
    Client Client,
    IReadOnlyCollection<string> Scopes,
    string? Nonce,
    IReadOnlyCollection<string> AuthMethods,
    string SessionId);

public sealed record AccessTokenRequest(
    Guid TenantId,
    User User,
    Client Client,
    IReadOnlyCollection<string> Scopes,
    IReadOnlyCollection<string>? Permissions,
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
    IReadOnlyCollection<string> ScopesSupported,
    IReadOnlyCollection<string> GrantTypesSupported,
    IReadOnlyCollection<string> CodeChallengeMethodsSupported);

public sealed record IntrospectionResponse(
    bool Active,
    string? Scope = null,
    string? ClientId = null,
    string? Sub = null,
    string? Username = null,
    string? TokenType = null,
    string? Aud = null,
    string? Iss = null,
    long? Exp = null,
    long? Iat = null,
    string? Jti = null,
    IReadOnlyCollection<string>? Permissions = null,
    string? Tid = null);
