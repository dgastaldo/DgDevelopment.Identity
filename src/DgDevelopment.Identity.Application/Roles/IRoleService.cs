using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Roles;

public interface IRoleService
{
    Task<PagedResult<Role>> GetPagedAsync(string? search, int page, int pageSize, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task<Role?> GetAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task<Role> CreateAsync(string name, string description, Guid platformId, Guid tenantId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, string name, string description, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task AssignPermissionAsync(Guid roleId, Guid permissionId, string? scopeType, string? scopeValue, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task RemovePermissionAsync(Guid roleId, Guid permissionId, Guid tenantId, bool allTenants, CancellationToken ct = default);
}
