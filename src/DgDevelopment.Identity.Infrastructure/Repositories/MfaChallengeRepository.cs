using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class MfaChallengeRepository : IMfaChallengeRepository
{
    private readonly IdentityDbContext _context;

    public MfaChallengeRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<MfaChallenge?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.MfaChallenges
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<MfaChallenge?> GetByIdAndCodeHashAsync(Guid id, string codeHash, CancellationToken ct = default)
    {
        return await _context.MfaChallenges
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.ChallengeCodeHash == codeHash, ct).ConfigureAwait(false);
    }

    public async Task AddAsync(MfaChallenge challenge, CancellationToken ct = default)
    {
        await _context.MfaChallenges.AddAsync(challenge, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(MfaChallenge challenge, CancellationToken ct = default)
    {
        _context.MfaChallenges.Update(challenge);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<MfaChallengeStatus?> GetStatusAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.MfaChallenges
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => (MfaChallengeStatus?)c.Status)
            .SingleOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteExpiredAsync(CancellationToken ct = default)
    {
        await _context.MfaChallenges
            .Where(c => c.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}