using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Clients;

public sealed record CreateClientCommand(
    string Name, ClientType ClientType, Guid? PlatformId, bool RequireConsent,
    IReadOnlyCollection<string> GrantTypes, IReadOnlyCollection<string> Scopes,
    IReadOnlyCollection<Uri> RedirectUris, IReadOnlyCollection<Uri> PostLogoutRedirectUris,
    IReadOnlyCollection<string> AdminConsentScopes);

public sealed record UpdateClientCommand(
    string Name, bool RequireConsent, Guid? PlatformId,
    IReadOnlyCollection<string> GrantTypes, IReadOnlyCollection<string> Scopes,
    IReadOnlyCollection<Uri> RedirectUris, IReadOnlyCollection<Uri> PostLogoutRedirectUris,
    IReadOnlyCollection<string> AdminConsentScopes);

public interface IClientService
{
    Task<PagedResult<Client>> GetPagedAsync(string? search, int page, int pageSize, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task<Client?> GetAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);

    Task<(Client Client, string ClientSecret)> CreateAsync(CreateClientCommand command, Guid tenantId, CancellationToken ct = default);

    Task UpdateAsync(Guid id, UpdateClientCommand command, Guid tenantId, bool allTenants, CancellationToken ct = default);

    Task<string> RegenerateSecretAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task SetActiveAsync(Guid id, bool isActive, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);
}
