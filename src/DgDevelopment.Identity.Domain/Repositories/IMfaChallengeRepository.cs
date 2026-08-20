using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IMfaChallengeRepository
{
    Task<MfaChallenge?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MfaChallenge?> GetByIdAndCodeHashAsync(Guid id, string codeHash, CancellationToken ct = default);
    Task AddAsync(MfaChallenge challenge, CancellationToken ct = default);
    Task UpdateAsync(MfaChallenge challenge, CancellationToken ct = default);
    Task<MfaChallengeStatus?> GetStatusAsync(Guid id, CancellationToken ct = default);
    Task DeleteExpiredAsync(CancellationToken ct = default);
}