using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class UserSessionRepository : IUserSessionRepository
{
    private readonly IdentityDbContext _context;

    public UserSessionRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<UserSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<UserSession?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default)
    {
        return await _context.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct).ConfigureAwait(false);
    }

    public async Task AddAsync(UserSession session, CancellationToken ct = default)
    {
        await _context.UserSessions.AddAsync(session, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var session = await _context.UserSessions.FindAsync([id], ct).ConfigureAwait(false);
        if (session is not null)
        {
            session.Revoke();
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var sessions = await _context.UserSessions
            .Where(s => s.UserId == userId)
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var session in sessions)
        {
            session.Revoke();
        }

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteExpiredAsync(CancellationToken ct = default)
    {
        await _context.UserSessions
            .Where(s => s.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}
