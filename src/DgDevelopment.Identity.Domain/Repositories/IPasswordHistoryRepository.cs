using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IPasswordHistoryRepository
{
    Task<IReadOnlyCollection<PasswordHistoryEntry>> GetRecentAsync(Guid userId, int take, CancellationToken ct = default);

    /// <summary>
    /// Records <paramref name="passwordHash"/> as a password the user previously had, then trims
    /// the user's history down to the <paramref name="keep"/> most recent entries.
    /// </summary>
    Task RecordAsync(Guid userId, string passwordHash, int keep, CancellationToken ct = default);
}
