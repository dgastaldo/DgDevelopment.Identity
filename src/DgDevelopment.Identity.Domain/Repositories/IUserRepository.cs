using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Authorization;
using DgDevelopment.Identity.Domain.ValueObjects;

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

    /// <summary>
    /// Loads the user WITH tracking and mutates its owned <see cref="User.Emails"/> collection
    /// in place, so EF Core's own change tracking emits the right INSERT/UPDATE/DELETE - unlike
    /// <see cref="UpdateAsync"/>, which works off a detached graph and can't express removing an
    /// owned <see cref="UserEmail"/> row (its constructor always assigns a new Id, so there's no
    /// way to build a delete-by-key stub for it).
    /// </summary>
    Task AddEmailAsync(Guid userId, EmailAddress email, bool isPrimary, CancellationToken ct = default);
    Task RemoveEmailAsync(Guid userId, EmailAddress email, CancellationToken ct = default);
    Task SetPrimaryEmailAsync(Guid userId, EmailAddress email, CancellationToken ct = default);
    Task VerifyEmailAsync(Guid userId, EmailAddress email, CancellationToken ct = default);
}
