using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IRevokedTokenRepository
{
    Task<bool> ExistsAsync(string jtiHash, CancellationToken ct = default);
    Task AddAsync(RevokedToken token, CancellationToken ct = default);
    Task DeleteExpiredAsync(CancellationToken ct = default);
}