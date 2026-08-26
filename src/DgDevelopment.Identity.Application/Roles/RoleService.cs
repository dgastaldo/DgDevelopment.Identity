using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Roles;

public sealed class RoleService(IRoleRepository roleRepository, IPermissionRepository permissionRepository, IPlatformRepository platformRepository) : IRoleService
{
    public async Task<PagedResult<Role>> GetPagedAsync(string? search, int page, int pageSize, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var roles = (await roleRepository.GetAllAsync(ct).ConfigureAwait(false))
            .Where(r => allTenants || r.TenantId == tenantId)
            .Where(r => string.IsNullOrWhiteSpace(search) || r.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Name)
            .ToList();

        var total = roles.Count;
        var items = roles.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new(items, page, pageSize, total);
    }

    public async Task<Role?> GetAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var role = await roleRepository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (role is null)
            return null;

        return !allTenants && role.TenantId != tenantId ? null : role;
    }

    public async Task<Role> CreateAsync(string name, string description, Guid platformId, Guid tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        await EnsurePlatformInTenantAsync(platformId, tenantId, ct).ConfigureAwait(false);

        var role = new Role(tenantId, platformId, name, description);
        await roleRepository.AddAsync(role, ct).ConfigureAwait(false);
        return role;
    }

    public async Task UpdateAsync(Guid id, string name, string description, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var role = await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);
        role.Rename(name, description);
        await roleRepository.UpdateAsync(role, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);
        await roleRepository.DeleteAsync(id, ct).ConfigureAwait(false);
    }

    public async Task AssignPermissionAsync(Guid roleId, Guid permissionId, string? scopeType, string? scopeValue, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var role = await GetRequiredAsync(roleId, tenantId, allTenants, ct).ConfigureAwait(false);
        var permission = await permissionRepository.GetByIdAsync(permissionId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Permission not found.");

        if (permission.PlatformId != role.PlatformId)
            throw new InvalidOperationException("The permission does not belong to the role's platform.");

        role.AddPermission(permission, scopeType, scopeValue);
        await roleRepository.UpdateAsync(role, ct).ConfigureAwait(false);
    }

    public async Task RemovePermissionAsync(Guid roleId, Guid permissionId, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var role = await GetRequiredAsync(roleId, tenantId, allTenants, ct).ConfigureAwait(false);
        role.RemovePermission(permissionId);
        await roleRepository.UpdateAsync(role, ct).ConfigureAwait(false);
    }

    private async Task<Role> GetRequiredAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct)
    {
        var role = await roleRepository.GetByIdAsync(id, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Role not found.");

        if (!allTenants && role.TenantId != tenantId)
            throw new InvalidOperationException("The role does not belong to the active tenant.");

        return role;
    }

    private async Task EnsurePlatformInTenantAsync(Guid platformId, Guid tenantId, CancellationToken ct)
    {
        var platform = await platformRepository.GetByIdAsync(platformId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Platform not found.");

        if (platform.TenantId != tenantId)
            throw new InvalidOperationException("The platform does not belong to the active tenant.");
    }
}
