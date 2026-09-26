namespace DgDevelopment.Identity.Application.Common;

public interface IPasswordHistoryService
{
    /// <summary>
    /// Throws <see cref="ArgumentException"/> if <paramref name="candidatePassword"/> matches the
    /// user's current password or any of their recent previous ones.
    /// </summary>
    Task EnsureNotReusedAsync(Guid userId, string candidatePassword, string currentPasswordHash, CancellationToken ct = default);

    /// <summary>
    /// Records <paramref name="previousPasswordHash"/> - the hash being replaced - into the
    /// user's password history. Call this once the new password has been accepted, right before
    /// (or after) persisting it.
    /// </summary>
    Task RecordChangeAsync(Guid userId, string previousPasswordHash, CancellationToken ct = default);
}
