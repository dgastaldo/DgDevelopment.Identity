using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Users;

public interface IUserService
{
    Task<PagedResult<User>> GetPagedAsync(string? search, int page, int pageSize, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task<User?> GetAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task<User> CreateAsync(string username, string password, string email, bool isSystemAccount, Guid tenantId, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task SetLockAsync(Guid id, bool locked, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid id, string password, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task AssignRoleAsync(Guid userId, Guid roleId, string? scopeType, string? scopeValue, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task RemoveRoleAsync(Guid userId, Guid roleId, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task AssignPermissionAsync(Guid userId, Guid permissionId, string? scopeType, string? scopeValue, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task RemovePermissionAsync(Guid userId, Guid permissionId, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task AssignGroupAsync(Guid userId, Guid groupId, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task RemoveGroupAsync(Guid userId, Guid groupId, Guid tenantId, bool allTenants, CancellationToken ct = default);
}
