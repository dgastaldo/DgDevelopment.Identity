using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Clients;

public interface IClientService
{
    Task<PagedResult<Client>> GetPagedAsync(string? search, int page, int pageSize, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task<Client?> GetAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);

    Task<(Client Client, string ClientSecret)> CreateAsync(
        string name, ClientType clientType, Guid? platformId, bool requireConsent,
        IReadOnlyCollection<string> grantTypes, IReadOnlyCollection<string> scopes,
        IReadOnlyCollection<Uri> redirectUris, IReadOnlyCollection<Uri> postLogoutRedirectUris,
        IReadOnlyCollection<string> adminConsentScopes, Guid tenantId, CancellationToken ct = default);

    Task UpdateAsync(
        Guid id, string name, bool requireConsent, Guid? platformId,
        IReadOnlyCollection<string> grantTypes, IReadOnlyCollection<string> scopes,
        IReadOnlyCollection<Uri> redirectUris, IReadOnlyCollection<Uri> postLogoutRedirectUris,
        IReadOnlyCollection<string> adminConsentScopes, Guid tenantId, bool allTenants, CancellationToken ct = default);

    Task<string> RegenerateSecretAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task SetActiveAsync(Guid id, bool isActive, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);
}
