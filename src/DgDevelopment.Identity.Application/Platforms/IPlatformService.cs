using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Platforms;

public interface IPlatformService
{
    Task<PagedResult<Platform>> GetPagedAsync(string? search, int page, int pageSize, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task<Platform?> GetAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task<Platform> CreateAsync(string name, string description, PermissionMode permissionMode, Guid tenantId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, string name, string description, PermissionMode permissionMode, Guid tenantId, bool allTenants, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default);
}
