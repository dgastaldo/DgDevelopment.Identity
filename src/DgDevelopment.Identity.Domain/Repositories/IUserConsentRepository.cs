using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IUserConsentRepository
{
    Task<UserConsent?> GetAsync(Guid userId, Guid clientId, CancellationToken ct = default);
    Task<IReadOnlyCollection<UserConsent>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddOrUpdateAsync(UserConsent consent, CancellationToken ct = default);
    Task RevokeAsync(Guid userId, Guid clientId, CancellationToken ct = default);
    Task DeleteExpiredAsync(CancellationToken ct = default);
}
