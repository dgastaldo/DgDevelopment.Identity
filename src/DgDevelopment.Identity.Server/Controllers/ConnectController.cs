namespace DgDevelopment.Identity.Server.Controllers;

using DgDevelopment.Identity.OAuth.Services;
using Microsoft.AspNetCore.Mvc;

[Route("connect")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ConnectController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITokenService _tokenService;
    private readonly IClientValidator _clientValidator;
    private readonly IKeyMaterialService _keyMaterialService;
    private readonly IUserInteractionService _userInteraction;

    public ConnectController(
        IAuthorizationService authorizationService,
        ITokenService tokenService,
        IClientValidator clientValidator,
        IKeyMaterialService keyMaterialService,
        IUserInteractionService userInteraction)
    {
        _authorizationService = authorizationService;
        _tokenService = tokenService;
        _clientValidator = clientValidator;
        _keyMaterialService = keyMaterialService;
        _userInteraction = userInteraction;
    }

    [HttpGet("authorize")]
    [HttpPost("authorize")]
    public async Task<IActionResult> Authorize(
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string response_type,
        [FromQuery] string scope,
        [FromQuery] string? state = null,
        [FromQuery] string? nonce = null,
        [FromQuery] string? code_challenge = null,
        [FromQuery] string? code_challenge_method = null)
    {
        var result = await _authorizationService.ValidateAsync(new(
            client_id, redirect_uri, response_type, scope, state, nonce,
            code_challenge, code_challenge_method));

        if (!result.IsValid)
            return Redirect(_userInteraction.GetErrorUrl(result.Error!, result.ErrorDescription));

        if (!User.Identity!.IsAuthenticated)
            return Redirect(_userInteraction.GetLoginUrl(Url.ActionLink()!));

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var scopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var code = await _authorizationService.CreateAuthorizationCodeAsync(
            result.Client!, new DgDevelopment.Identity.Domain.Entities.User("tmp", "",
            DgDevelopment.Identity.Domain.ValueObjects.EmailAddress.FromString("tmp@tmp.com"))
            , scopes, redirect_uri, code_challenge, code_challenge_method);

        var redirect = $"{redirect_uri}?code={Uri.EscapeDataString(code)}";
        if (state != null)
            redirect += $"&state={Uri.EscapeDataString(state)}";

        return Redirect(redirect);
    }

    [HttpPost("token")]
    public async Task<IActionResult> Token([FromForm] TokenRequest request)
    {
        try
        {
            TokenResponse response = request.GrantType switch
            {
                "authorization_code" => await _tokenService.ProcessAuthorizationCodeAsync(
                    request.Code!, request.CodeVerifier!, request.ClientId!, new Uri(request.RedirectUri!)),
                "client_credentials" => await ProcessClientCredentialsAsync(request),
                "refresh_token" => await _tokenService.ProcessRefreshTokenAsync(
                    request.RefreshToken!, request.ClientId!),
                "device_code" => await _tokenService.ProcessDeviceCodeAsync(
                    request.DeviceCode!, request.ClientId!),
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
        var validation = await _clientValidator.ValidateAsync(request.ClientId, request.ClientSecret, "client_credentials");
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.ErrorDescription!);

        var scopes = request.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? ["openid"];
        return await _tokenService.ProcessClientCredentialsAsync(validation, scopes);
    }

    [HttpGet("jwks")]
    public async Task<IActionResult> Jwks()
    {
        var jwks = await _keyMaterialService.GetJwksDocumentAsync();
        return Ok(jwks);
    }

    [HttpGet(".well-known/openid-configuration")]
    [Route(".well-known/openid-configuration")]
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
            scopes_supported = new[] { "openid", "profile", "email" },
            grant_types_supported = new[] { "authorization_code", "client_credentials", "refresh_token", "device_code" },
            code_challenge_methods_supported = new[] { "S256" },
            id_token_signing_alg_values_supported = new[] { "RS256" }
        });
    }
}
