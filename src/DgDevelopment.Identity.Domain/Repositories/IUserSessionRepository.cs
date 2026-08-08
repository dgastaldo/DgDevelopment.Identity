using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IUserSessionRepository
{
    Task<UserSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserSession?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default);
    Task AddAsync(UserSession session, CancellationToken ct = default);
    Task RevokeAsync(Guid id, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
    Task DeleteExpiredAsync(CancellationToken ct = default);
}
