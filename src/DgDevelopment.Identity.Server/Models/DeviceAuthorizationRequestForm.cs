namespace DgDevelopment.Identity.Server.Models;

using Microsoft.AspNetCore.Mvc;

public sealed record DeviceAuthorizationRequestForm
{
    [FromForm(Name = "client_id")] public string? ClientId { get; init; }
    [FromForm(Name = "client_secret")] public string? ClientSecret { get; init; }
    [FromForm(Name = "scope")] public string? Scope { get; init; }
}