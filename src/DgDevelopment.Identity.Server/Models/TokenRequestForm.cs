namespace DgDevelopment.Identity.Server.Models;

using Microsoft.AspNetCore.Mvc;

public sealed record TokenRequestForm
{
    [FromForm(Name = "grant_type")] public string? GrantType { get; init; }
    [FromForm(Name = "code")] public string? Code { get; init; }
    [FromForm(Name = "redirect_uri")] public string? RedirectUri { get; init; }
    [FromForm(Name = "client_id")] public string? ClientId { get; init; }
    [FromForm(Name = "client_secret")] public string? ClientSecret { get; init; }
    [FromForm(Name = "code_verifier")] public string? CodeVerifier { get; init; }
    [FromForm(Name = "refresh_token")] public string? RefreshToken { get; init; }
    [FromForm(Name = "device_code")] public string? DeviceCode { get; init; }
    [FromForm(Name = "scope")] public string? Scope { get; init; }
}