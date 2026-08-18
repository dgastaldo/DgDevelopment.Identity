namespace DgDevelopment.Identity.OAuth.Services;

public interface ITokenRevocationService
{
    Task RevokeAsync(ClientValidationResult client, string token, string? tokenTypeHint = null, CancellationToken ct = default);
}