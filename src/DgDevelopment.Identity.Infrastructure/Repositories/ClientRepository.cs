using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class ClientRepository(IdentityDbContext context) : IClientRepository
{
    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Clients
            .AsNoTracking()
            .Include(c => c.GrantTypes)
            .Include(c => c.Scopes)
            .Include(c => c.AdminConsentScopes)
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<Client?> GetByClientIdAsync(string clientId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(clientId, out var parsedClientId))
            return null;

        return await context.Clients
            .AsNoTracking()
            .Include(c => c.GrantTypes)
            .Include(c => c.Scopes)
            .Include(c => c.AdminConsentScopes)
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .FirstOrDefaultAsync(c => c.ClientId == parsedClientId, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Client>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Clients
            .AsNoTracking()
            .Include(c => c.GrantTypes)
            .Include(c => c.Scopes)
            .Include(c => c.AdminConsentScopes)
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .OrderBy(c => c.Name)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<string>> GetAllActiveClientIdsAsync(CancellationToken ct = default)
    {
        var clientIds = await context.Clients
            .AsNoTracking()
            .Where(c => c.IsActive)
            .ToListAsync(ct).ConfigureAwait(false);

        return clientIds.Select(c => c.ClientId.ToString()).ToArray();
    }

    public async Task<IReadOnlyCollection<Uri>> GetAllActiveRedirectUrisAsync(CancellationToken ct = default)
    {
        var redirectUris = await context.Clients
            .AsNoTracking()
            .Where(c => c.IsActive)
            .SelectMany(c => c.RedirectUris.Select(r => r.RedirectUri))
            .Select(uri => uri.ToString())
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        var postLogoutUris = await context.Clients
            .AsNoTracking()
            .Where(c => c.IsActive)
            .SelectMany(c => c.PostLogoutRedirectUris.Select(r => r.RedirectUri))
            .Select(uri => uri.ToString())
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        return redirectUris.Concat(postLogoutUris).Distinct().Select(u => new Uri(u)).ToArray();
    }

    public async Task AddAsync(Client client, CancellationToken ct = default)
    {
        await context.Clients.AddAsync(client, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Client client, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        // Client's grant types/scopes/redirect URIs are EF owned collections (OwnsMany), which are
        // only tracked correctly through the owner's own tracked navigation - reattaching a
        // disconnected graph via Update() does not reconcile them (verified: silently drops
        // additions/removals). Load the tracked instance instead and replay the incoming diff onto
        // it through the entity's own Add/Remove methods, which EF's change tracker does pick up.
        var tracked = await context.Clients
            .Include(c => c.GrantTypes)
            .Include(c => c.Scopes)
            .Include(c => c.AdminConsentScopes)
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .FirstOrDefaultAsync(c => c.Id == client.Id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Client not found.");

        context.Entry(tracked).CurrentValues.SetValues(client);

        ReconcileGrantTypes(tracked, client);
        ReconcileScopes(tracked, client);
        ReconcileAdminConsentScopes(tracked, client);
        ReconcileRedirectUris(tracked, client);
        ReconcilePostLogoutRedirectUris(tracked, client);

        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        static void ReconcileGrantTypes(Client tracked, Client incoming)
        {
            var currentValues = incoming.GrantTypes.Select(g => g.GrantType).ToHashSet();
            foreach (var removed in tracked.GrantTypes.Select(g => g.GrantType).Where(g => !currentValues.Contains(g)).ToList())
                tracked.RemoveGrantType(removed);
            foreach (var added in currentValues.Except(tracked.GrantTypes.Select(g => g.GrantType)))
                tracked.AddGrantType(added);
        }

        static void ReconcileScopes(Client tracked, Client incoming)
        {
            var currentValues = incoming.Scopes.Select(s => s.Scope).ToHashSet();
            foreach (var removed in tracked.Scopes.Select(s => s.Scope).Where(s => !currentValues.Contains(s)).ToList())
                tracked.RemoveScope(removed);
            foreach (var added in currentValues.Except(tracked.Scopes.Select(s => s.Scope)))
                tracked.AddScope(added);
        }

        static void ReconcileAdminConsentScopes(Client tracked, Client incoming)
        {
            var currentValues = incoming.AdminConsentScopes.Select(s => s.Scope).ToHashSet();
            foreach (var removed in tracked.AdminConsentScopes.Select(s => s.Scope).Where(s => !currentValues.Contains(s)).ToList())
                tracked.RemoveAdminConsentScope(removed);
            foreach (var added in currentValues.Except(tracked.AdminConsentScopes.Select(s => s.Scope)))
                tracked.AddAdminConsentScope(added);
        }

        static void ReconcileRedirectUris(Client tracked, Client incoming)
        {
            var currentValues = incoming.RedirectUris.Select(r => r.RedirectUri).ToHashSet();
            foreach (var removed in tracked.RedirectUris.Select(r => r.RedirectUri).Where(r => !currentValues.Contains(r)).ToList())
                tracked.RemoveRedirectUri(removed);
            foreach (var added in currentValues.Except(tracked.RedirectUris.Select(r => r.RedirectUri)))
                tracked.AddRedirectUri(added);
        }

        static void ReconcilePostLogoutRedirectUris(Client tracked, Client incoming)
        {
            var currentValues = incoming.PostLogoutRedirectUris.Select(r => r.RedirectUri).ToHashSet();
            foreach (var removed in tracked.PostLogoutRedirectUris.Select(r => r.RedirectUri).Where(r => !currentValues.Contains(r)).ToList())
                tracked.RemovePostLogoutRedirectUri(removed);
            foreach (var added in currentValues.Except(tracked.PostLogoutRedirectUris.Select(r => r.RedirectUri)))
                tracked.AddPostLogoutRedirectUri(added);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var client = await context.Clients.FindAsync([id], ct).ConfigureAwait(false);
        if (client is not null)
        {
            context.Clients.Remove(client);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
