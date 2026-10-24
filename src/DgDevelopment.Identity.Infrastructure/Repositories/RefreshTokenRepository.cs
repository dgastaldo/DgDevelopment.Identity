using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _context;

    public RefreshTokenRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        return await _context.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct).ConfigureAwait(false);
    }

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        await _context.RefreshTokens.AddAsync(token, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var token = await _context.RefreshTokens.FindAsync([id], ct).ConfigureAwait(false);
        if (token is not null)
        {
            token.Revoke();
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task RevokeChainAsync(string familyId, CancellationToken ct = default)
    {
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == familyId, ct).ConfigureAwait(false);

        if (token is null) return;

        token.Revoke();
        var previousTokenId = token.PreviousTokenId;

        while (previousTokenId.HasValue)
        {
            var previous = await _context.RefreshTokens.FindAsync([previousTokenId.Value], ct).ConfigureAwait(false);
            if (previous is null) break;

            previous.Revoke();
            previousTokenId = previous.PreviousTokenId;
        }

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        await _context.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.IsRevoked, true), ct).ConfigureAwait(false);
    }

    public async Task DeleteExpiredAsync(CancellationToken ct = default)
    {
        await _context.RefreshTokens
            .Where(r => r.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}
