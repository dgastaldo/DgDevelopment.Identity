using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class ClientRepository : IClientRepository
{
    private readonly IdentityDbContext _context;

    public ClientRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Clients
            .AsNoTracking()
            .Include(c => c.GrantTypes)
            .Include(c => c.Scopes)
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Client?> GetByClientIdAsync(string clientId, CancellationToken ct = default)
    {
        return await _context.Clients
            .AsNoTracking()
            .Include(c => c.GrantTypes)
            .Include(c => c.Scopes)
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .FirstOrDefaultAsync(c => c.ClientId == clientId, ct);
    }

    public async Task AddAsync(Client client, CancellationToken ct = default)
    {
        await _context.Clients.AddAsync(client, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Client client, CancellationToken ct = default)
    {
        _context.Clients.Update(client);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var client = await _context.Clients.FindAsync([id], ct);
        if (client is not null)
        {
            _context.Clients.Remove(client);
            await _context.SaveChangesAsync(ct);
        }
    }
}
