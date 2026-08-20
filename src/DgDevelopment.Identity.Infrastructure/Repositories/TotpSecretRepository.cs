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
            .Include(t => t.BackupCodes)
            .FirstOrDefaultAsync(t => t.UserId == userId, ct).ConfigureAwait(false);
    }

    public async Task AddAsync(TotpSecret secret, CancellationToken ct = default)
    {
        await _context.TotpSecrets.AddAsync(secret, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(TotpSecret secret, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(secret);

        // BackupCode.Id is assigned by the domain (client-generated Guid), so EF Core
        // cannot tell newly created codes from rows that already exist and would emit
        // UPDATE for rows that were never inserted. Force Added for children that are
        // not yet tracked; codes loaded from the database stay tracked and unchanged.
        foreach (var code in secret.BackupCodes)
        {
            if (_context.Entry(code).State == EntityState.Detached)
                _context.Entry(code).State = EntityState.Added;
        }

        if (_context.Entry(secret).State == EntityState.Detached)
            _context.TotpSecrets.Update(secret);

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        var secret = await _context.TotpSecrets
            .Include(t => t.BackupCodes)
            .FirstOrDefaultAsync(t => t.UserId == userId, ct).ConfigureAwait(false);

        if (secret is not null)
        {
            _context.TotpSecrets.Remove(secret);
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}