using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Server.Models;

public sealed record CreateClientRequest(
    string Name,
    ClientType ClientType,
    Guid? PlatformId,
    bool RequireConsent,
    IReadOnlyCollection<string>? GrantTypes,
    IReadOnlyCollection<string>? Scopes,
    IReadOnlyCollection<Uri>? RedirectUris,
    IReadOnlyCollection<Uri>? PostLogoutRedirectUris,
    IReadOnlyCollection<string>? AdminConsentScopes);

public sealed record UpdateClientRequest(
    string Name,
    bool RequireConsent,
    Guid? PlatformId,
    IReadOnlyCollection<string>? GrantTypes,
    IReadOnlyCollection<string>? Scopes,
    IReadOnlyCollection<Uri>? RedirectUris,
    IReadOnlyCollection<Uri>? PostLogoutRedirectUris,
    IReadOnlyCollection<string>? AdminConsentScopes);

public sealed record SetClientActiveRequest(bool IsActive);

public sealed record ClientSecretResponse(string ClientSecret);

public sealed record ClientResponse(
    Guid Id,
    Guid TenantId,
    Guid ClientId,
    Guid? PlatformId,
    string Name,
    ClientType ClientType,
    bool RequirePkce,
    bool RequireConsent,
    bool IsActive,
    IReadOnlyCollection<string> GrantTypes,
    IReadOnlyCollection<string> Scopes,
    IReadOnlyCollection<Uri> RedirectUris,
    IReadOnlyCollection<Uri> PostLogoutRedirectUris,
    IReadOnlyCollection<string> AdminConsentScopes)
{
    public static ClientResponse From(Client client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new(
            client.Id, client.TenantId, client.ClientId, client.PlatformId, client.Name, client.ClientType,
            client.RequirePkce, client.RequireConsent, client.IsActive,
            client.GrantTypes.Select(g => g.GrantType).ToList(),
            client.Scopes.Select(s => s.Scope).ToList(),
            client.RedirectUris.Select(r => r.RedirectUri).ToList(),
            client.PostLogoutRedirectUris.Select(r => r.RedirectUri).ToList(),
            client.AdminConsentScopes.Select(s => s.Scope).ToList());
    }
}
