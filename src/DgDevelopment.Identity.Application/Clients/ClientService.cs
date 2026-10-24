using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Clients;

public sealed class ClientService(IClientRepository clientRepository, IPlatformRepository platformRepository) : IClientService
{
    public async Task<PagedResult<Client>> GetPagedAsync(string? search, int page, int pageSize, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var clients = (await clientRepository.GetAllAsync(ct).ConfigureAwait(false))
            .Where(c => allTenants || c.TenantId == tenantId)
            .Where(c => string.IsNullOrWhiteSpace(search) || c.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name)
            .ToList();

        var total = clients.Count;
        var items = clients.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new(items, page, pageSize, total);
    }

    public async Task<Client?> GetAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var client = await clientRepository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (client is null)
            return null;

        return !allTenants && client.TenantId != tenantId ? null : client;
    }

    public async Task<(Client Client, string ClientSecret)> CreateAsync(CreateClientCommand command, Guid tenantId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Name);
        ArgumentNullException.ThrowIfNull(command.GrantTypes);
        ArgumentNullException.ThrowIfNull(command.Scopes);
        ArgumentNullException.ThrowIfNull(command.RedirectUris);
        ArgumentNullException.ThrowIfNull(command.PostLogoutRedirectUris);
        ArgumentNullException.ThrowIfNull(command.AdminConsentScopes);

        if (command.PlatformId is { } requestedPlatformId)
            await EnsurePlatformInTenantAsync(requestedPlatformId, tenantId, ct).ConfigureAwait(false);

        var clientSecret = Secret.Generate(32);
        var client = new Client(tenantId, Guid.NewGuid(), Hash(clientSecret), command.Name, command.ClientType, command.PlatformId, command.RequireConsent);

        foreach (var grantType in command.GrantTypes)
            client.AddGrantType(grantType);
        foreach (var scope in command.Scopes)
            client.AddScope(scope);
        foreach (var redirectUri in command.RedirectUris)
            client.AddRedirectUri(redirectUri);
        foreach (var postLogoutRedirectUri in command.PostLogoutRedirectUris)
            client.AddPostLogoutRedirectUri(postLogoutRedirectUri);
        foreach (var adminConsentScope in command.AdminConsentScopes)
            client.AddAdminConsentScope(adminConsentScope);

        await clientRepository.AddAsync(client, ct).ConfigureAwait(false);
        return (client, clientSecret);
    }

    public async Task UpdateAsync(Guid id, UpdateClientCommand command, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Name);

        var client = await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);

        if (command.PlatformId is { } requestedPlatformId)
            await EnsurePlatformInTenantAsync(requestedPlatformId, client.TenantId, ct).ConfigureAwait(false);

        client.Rename(command.Name);
        client.SetRequireConsent(command.RequireConsent);
        client.SetPlatform(command.PlatformId);

        Reconcile(client.GrantTypes.Select(g => g.GrantType), command.GrantTypes, client.AddGrantType, client.RemoveGrantType);
        Reconcile(client.Scopes.Select(s => s.Scope), command.Scopes, client.AddScope, client.RemoveScope);
        Reconcile(client.RedirectUris.Select(r => r.RedirectUri), command.RedirectUris, client.AddRedirectUri, client.RemoveRedirectUri);
        Reconcile(client.PostLogoutRedirectUris.Select(r => r.RedirectUri), command.PostLogoutRedirectUris, client.AddPostLogoutRedirectUri, client.RemovePostLogoutRedirectUri);
        Reconcile(client.AdminConsentScopes.Select(s => s.Scope), command.AdminConsentScopes, client.AddAdminConsentScope, client.RemoveAdminConsentScope);

        await clientRepository.UpdateAsync(client, ct).ConfigureAwait(false);
    }

    public async Task<string> RegenerateSecretAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var client = await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);
        var clientSecret = Secret.Generate(32);
        client.SetSecret(Hash(clientSecret));
        await clientRepository.UpdateAsync(client, ct).ConfigureAwait(false);
        return clientSecret;
    }

    public async Task SetActiveAsync(Guid id, bool isActive, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var client = await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);
        if (isActive)
            client.Activate();
        else
            client.Deactivate();
        await clientRepository.UpdateAsync(client, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);
        await clientRepository.DeleteAsync(id, ct).ConfigureAwait(false);
    }

    private async Task<Client> GetRequiredAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct)
    {
        var client = await clientRepository.GetByIdAsync(id, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Client not found.");

        if (!allTenants && client.TenantId != tenantId)
            throw new InvalidOperationException("The client does not belong to the active tenant.");

        return client;
    }

    private async Task EnsurePlatformInTenantAsync(Guid platformId, Guid tenantId, CancellationToken ct)
    {
        var platform = await platformRepository.GetByIdAsync(platformId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Platform not found.");

        if (platform.TenantId != tenantId)
            throw new InvalidOperationException("The platform does not belong to the client's tenant.");
    }

    private static void Reconcile<T>(IEnumerable<T> current, IEnumerable<T> desired, Action<T> add, Action<T> remove)
    {
        var desiredSet = desired.ToHashSet();
        var currentSet = current.ToHashSet();

        foreach (var removed in currentSet.Where(item => !desiredSet.Contains(item)))
            remove(removed);
        foreach (var added in desiredSet.Where(item => !currentSet.Contains(item)))
            add(added);
    }

    private static string Hash(string secret)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
