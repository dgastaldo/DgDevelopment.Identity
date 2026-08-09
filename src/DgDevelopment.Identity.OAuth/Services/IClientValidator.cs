using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.OAuth.Services;

public interface IClientValidator
{
    Task<ClientValidationResult> ValidateAsync(string? clientId, string? clientSecret, string grantType, CancellationToken ct = default);
}
