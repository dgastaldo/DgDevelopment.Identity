using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class UserConsentRepository : IUserConsentRepository
{
    private readonly IdentityDbContext _context;

    public UserConsentRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<UserConsent?> GetAsync(Guid userId, Guid clientId, CancellationToken ct = default)
    {
        return await _context.UserConsents
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ClientId == clientId, ct);
    }

    public async Task<IReadOnlyCollection<UserConsent>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.UserConsents
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task AddOrUpdateAsync(UserConsent consent, CancellationToken ct = default)
    {
        var existing = await _context.UserConsents
            .FirstOrDefaultAsync(c => c.UserId == consent.UserId && c.ClientId == consent.ClientId, ct);

        if (existing is not null)
        {
            _context.Entry(existing).CurrentValues.SetValues(consent);
        }
        else
        {
            await _context.UserConsents.AddAsync(consent, ct);
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(Guid userId, Guid clientId, CancellationToken ct = default)
    {
        var consent = await _context.UserConsents
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ClientId == clientId, ct);

        if (consent is not null)
        {
            _context.UserConsents.Remove(consent);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteExpiredAsync(CancellationToken ct = default)
    {
        await _context.UserConsents
            .Where(c => c.ExpiresAt.HasValue && c.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);
    }
}
