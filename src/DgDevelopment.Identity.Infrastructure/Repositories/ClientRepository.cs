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
        context.Clients.Update(client);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
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
