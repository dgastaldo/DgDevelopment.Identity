using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Authorization;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByLoginAsync(string provider, string providerKey, CancellationToken ct = default);
    Task<IReadOnlyCollection<User>> GetPagedAsync(string? search, int skip, int take, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task<int> CountAsync(string? search, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task<UserStatistics> GetStatisticsAsync(Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
