using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.Services;

namespace DgDevelopment.Identity.Application.Common;

public sealed class PasswordHistoryService(IPasswordHistoryRepository repository, IPasswordHasher passwordHasher) : IPasswordHistoryService
{
    // "Cannot reuse any of your last N passwords" - N includes the current one, so N-1 history
    // rows are kept/checked alongside it.
    private const int RememberedPasswordCount = 5;

    public async Task EnsureNotReusedAsync(Guid userId, string candidatePassword, string currentPasswordHash, CancellationToken ct = default)
    {
        if (passwordHasher.VerifyPassword(candidatePassword, currentPasswordHash))
            throw new ArgumentException($"The new password cannot match any of your last {RememberedPasswordCount} passwords.", nameof(candidatePassword));

        var history = await repository.GetRecentAsync(userId, RememberedPasswordCount - 1, ct).ConfigureAwait(false);
        if (history.Any(entry => passwordHasher.VerifyPassword(candidatePassword, entry.PasswordHash)))
            throw new ArgumentException($"The new password cannot match any of your last {RememberedPasswordCount} passwords.", nameof(candidatePassword));
    }

    public Task RecordChangeAsync(Guid userId, string previousPasswordHash, CancellationToken ct = default)
        => repository.RecordAsync(userId, previousPasswordHash, RememberedPasswordCount - 1, ct);
}
