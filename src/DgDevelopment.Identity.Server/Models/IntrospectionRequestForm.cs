namespace DgDevelopment.Identity.Server.Models;

using Microsoft.AspNetCore.Mvc;

public sealed record IntrospectionRequestForm
{
    [FromForm(Name = "token")] public string? Token { get; init; }
    [FromForm(Name = "token_type_hint")] public string? TokenTypeHint { get; init; }
    [FromForm(Name = "client_id")] public string? ClientId { get; init; }
    [FromForm(Name = "client_secret")] public string? ClientSecret { get; init; }
}