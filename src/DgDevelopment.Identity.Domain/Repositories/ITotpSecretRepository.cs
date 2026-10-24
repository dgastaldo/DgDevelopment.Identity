using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface ITotpSecretRepository
{
    Task<TotpSecret?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(TotpSecret secret, CancellationToken ct = default);
    Task UpdateAsync(TotpSecret secret, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, CancellationToken ct = default);
}
