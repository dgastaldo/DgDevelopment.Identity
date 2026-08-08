using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class TotpSecretRepository : ITotpSecretRepository
{
    private readonly IdentityDbContext _context;

    public TotpSecretRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<TotpSecret?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.TotpSecrets
            .AsNoTracking()
            .Include(t => t.BackupCodes)
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);
    }

    public async Task AddAsync(TotpSecret secret, CancellationToken ct = default)
    {
        await _context.TotpSecrets.AddAsync(secret, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TotpSecret secret, CancellationToken ct = default)
    {
        _context.TotpSecrets.Update(secret);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        var secret = await _context.TotpSecrets
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);

        if (secret is not null)
        {
            _context.TotpSecrets.Remove(secret);
            await _context.SaveChangesAsync(ct);
        }
    }
}
