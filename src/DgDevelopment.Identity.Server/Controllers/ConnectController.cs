namespace DgDevelopment.Identity.Server.Controllers;

using System.Globalization;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

[Route("connect")]
public sealed class ConnectController : Controller
{
    private readonly ITokenService _tokenService;
    private readonly IClientValidator _clientValidator;
    private readonly IKeyMaterialService _keyMaterialService;
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IClientIdCache _clientIdCache;

    private static readonly string[] _supportedScopes = ["openid", "profile", "email"];
    private static readonly string[] _supportedGrantTypes = ["authorization_code", "client_credentials", "refresh_token", "device_code"];
    private static readonly string[] _supportedCodeChallengeMethods = ["S256"];
    private static readonly string[] _supportedSigningAlgs = ["RS256"];

    public ConnectController(
        ITokenService tokenService,
        IClientValidator clientValidator,
        IKeyMaterialService keyMaterialService,
        IUserRepository userRepository,
        IJwtService jwtService,
        IClientIdCache clientIdCache)
    {
        _tokenService = tokenService;
        _clientValidator = clientValidator;
        _keyMaterialService = keyMaterialService;
        _userRepository = userRepository;
        _jwtService = jwtService;
        _clientIdCache = clientIdCache;
    }

    [HttpPost("token")]
    public async Task<IActionResult> Token([FromForm] TokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            TokenResponse response = request.GrantType switch
            {
                "authorization_code" => await _tokenService.ProcessAuthorizationCodeAsync(
                    request.Code!, request.CodeVerifier!, request.ClientId!, new Uri(request.RedirectUri!)).ConfigureAwait(false),
                "client_credentials" => await ProcessClientCredentialsAsync(request).ConfigureAwait(false),
                "refresh_token" => await _tokenService.ProcessRefreshTokenAsync(
                    request.RefreshToken!, request.ClientId!).ConfigureAwait(false),
                "device_code" => await _tokenService.ProcessDeviceCodeAsync(
                    request.DeviceCode!, request.ClientId!).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unsupported grant_type: {request.GrantType}")
            };

            return Ok(new
            {
                access_token = response.AccessToken,
                token_type = response.TokenType,
                expires_in = response.ExpiresIn,
                id_token = response.IdToken,
                refresh_token = response.RefreshToken,
                scope = response.Scope
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "invalid_grant", error_description = ex.Message });
        }
    }

    private async Task<TokenResponse> ProcessClientCredentialsAsync(TokenRequest request)
    {
        var validation = await _clientValidator.ValidateAsync(request.ClientId, request.ClientSecret, "client_credentials").ConfigureAwait(false);
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.ErrorDescription!);

        var scopes = request.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? ["openid"];
        return await _tokenService.ProcessClientCredentialsAsync(validation, scopes).ConfigureAwait(false);
    }

    [HttpGet("jwks")]
    public async Task<IActionResult> Jwks()
    {
        var jwks = await _keyMaterialService.GetJwksDocumentAsync().ConfigureAwait(false);
        return Ok(jwks);
    }

    [HttpGet("/.well-known/openid-configuration")]
    public IActionResult Discovery()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(new
        {
            issuer = baseUrl,
            authorization_endpoint = $"{baseUrl}/connect/authorize",
            token_endpoint = $"{baseUrl}/connect/token",
            userinfo_endpoint = $"{baseUrl}/connect/userinfo",
            end_session_endpoint = $"{baseUrl}/connect/endsession",
            jwks_uri = $"{baseUrl}/connect/jwks",
            device_authorization_endpoint = $"{baseUrl}/connect/deviceauthorization",
            introspection_endpoint = $"{baseUrl}/connect/introspect",
            revocation_endpoint = $"{baseUrl}/connect/revoke",
            scopes_supported = _supportedScopes,
            grant_types_supported = _supportedGrantTypes,
            code_challenge_methods_supported = _supportedCodeChallengeMethods,
            id_token_signing_alg_values_supported = _supportedSigningAlgs
        });
    }

    [HttpGet("userinfo")]
    [HttpPost("userinfo")]
    public async Task<IActionResult> UserInfo()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();

        var token = authHeader["Bearer ".Length..];
        var keys = await _keyMaterialService.GetJwksDocumentAsync().ConfigureAwait(false);
        var issuer = $"{Request.Scheme}://{Request.Host}";
        var parameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidIssuer = issuer,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            IssuerSigningKeys = keys.Keys,
            AudienceValidator = (audiences, _, _) =>
            {
                foreach (var aud in audiences)
                {
                    if (_clientIdCache.IsValidClientId(aud))
                        return true;
                }
                return false;
            }
        };

        var principal = await _jwtService.ValidateTokenAsync(token, parameters).ConfigureAwait(false);
        var sub = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (sub == null || !Guid.TryParse(sub, out var userId))
            return Unauthorized();

        var user = await _userRepository.GetByIdAsync(userId).ConfigureAwait(false);
        if (user == null) return Unauthorized();

        var claims = new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString(null, CultureInfo.InvariantCulture),
            ["name"] = user.Username
        };

        if (user.PrimaryEmail != null)
        {
            claims["email"] = user.PrimaryEmail.Value;
            claims["email_verified"] = true;
        }

        return Ok(claims);
    }

    [HttpGet("endsession")]
    public async Task<IActionResult> EndSession([FromQuery] string? post_logout_redirect_uri = null)
    {
        await HttpContext.SignOutAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(post_logout_redirect_uri))
            return Redirect(post_logout_redirect_uri);
        return Ok();
    }
}
