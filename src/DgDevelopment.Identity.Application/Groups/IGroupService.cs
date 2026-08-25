using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Groups;

public interface IGroupService
{
    Task<PagedResult<Group>> GetPagedAsync(string? search, int page, int pageSize, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task<Group?> GetAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task<Group> CreateAsync(string name, string description, Guid? parentGroupId, Guid tenantId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, string name, string description, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task SetParentAsync(Guid id, Guid? parentGroupId, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task AssignRoleAsync(Guid groupId, Guid roleId, string? scopeType, string? scopeValue, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task RemoveRoleAsync(Guid groupId, Guid roleId, Guid tenantId, bool allTenants, CancellationToken ct = default);
}
