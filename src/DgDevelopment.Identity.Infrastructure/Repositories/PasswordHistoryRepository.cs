using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class PasswordHistoryRepository(IdentityDbContext context) : IPasswordHistoryRepository
{
    public async Task<IReadOnlyCollection<PasswordHistoryEntry>> GetRecentAsync(Guid userId, int take, CancellationToken ct = default)
        => await context.PasswordHistoryEntries.AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task RecordAsync(Guid userId, string passwordHash, int keep, CancellationToken ct = default)
    {
        await context.PasswordHistoryEntries.AddAsync(new PasswordHistoryEntry(userId, passwordHash), ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        var stale = await context.PasswordHistoryEntries
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Skip(keep)
            .ToListAsync(ct).ConfigureAwait(false);

        if (stale.Count == 0)
            return;

        context.PasswordHistoryEntries.RemoveRange(stale);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
