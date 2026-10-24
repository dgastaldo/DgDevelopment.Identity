using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface ISigningKeyRepository
{
    Task<IReadOnlyCollection<SigningKey>> GetActiveKeysAsync(CancellationToken ct = default);
    Task<SigningKey?> GetByKeyIdAsync(string kid, CancellationToken ct = default);
    Task AddAsync(SigningKey key, CancellationToken ct = default);
    Task DeactivateAsync(string id, CancellationToken ct = default);
    Task DeleteExpiredAsync(CancellationToken ct = default);
}
