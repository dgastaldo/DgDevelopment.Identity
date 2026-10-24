using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class RevokedTokenRepository : IRevokedTokenRepository
{
    private readonly IdentityDbContext _context;

    public RevokedTokenRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(string jtiHash, CancellationToken ct = default)
    {
        return await _context.RevokedTokens
            .AsNoTracking()
            .AnyAsync(r => r.JtiHash == jtiHash && r.ExpiresAt > DateTime.UtcNow, ct).ConfigureAwait(false);
    }

    public async Task AddAsync(RevokedToken token, CancellationToken ct = default)
    {
        await _context.RevokedTokens.AddAsync(token, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteExpiredAsync(CancellationToken ct = default)
    {
        await _context.RevokedTokens
            .Where(r => r.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}