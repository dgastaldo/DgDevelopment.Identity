using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class SigningKeyRepository : ISigningKeyRepository
{
    private readonly IdentityDbContext _context;

    public SigningKeyRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<SigningKey>> GetActiveKeysAsync(CancellationToken ct = default)
    {
        return await _context.SigningKeys
            .AsNoTracking()
            .Where(k => k.IsActive && k.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<SigningKey?> GetByKeyIdAsync(string kid, CancellationToken ct = default)
    {
        return await _context.SigningKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == kid, ct).ConfigureAwait(false);
    }

    public async Task AddAsync(SigningKey key, CancellationToken ct = default)
    {
        await _context.SigningKeys.AddAsync(key, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeactivateAsync(string id, CancellationToken ct = default)
    {
        var key = await _context.SigningKeys.FindAsync([id], ct).ConfigureAwait(false);
        if (key is not null)
        {
            key.Deactivate();
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task DeleteExpiredAsync(CancellationToken ct = default)
    {
        await _context.SigningKeys
            .Where(k => k.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}
