using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Groups;

public sealed class GroupService(IGroupRepository groupRepository, IRoleRepository roleRepository) : IGroupService
{
    public async Task<PagedResult<Group>> GetPagedAsync(string? search, int page, int pageSize, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var groups = (await groupRepository.GetAllAsync(ct).ConfigureAwait(false))
            .Where(g => allTenants || g.TenantId == tenantId)
            .Where(g => string.IsNullOrWhiteSpace(search) || g.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(g => g.Name)
            .ToList();

        var total = groups.Count;
        var items = groups.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new(items, page, pageSize, total);
    }

    public async Task<Group?> GetAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var group = await groupRepository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (group is null)
            return null;

        return !allTenants && group.TenantId != tenantId ? null : group;
    }

    public async Task<Group> CreateAsync(string name, string description, Guid? parentGroupId, Guid tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (parentGroupId is { } parentId)
            await EnsureParentInTenantAsync(parentId, tenantId, ct).ConfigureAwait(false);

        var group = new Group(tenantId, name, description, parentGroupId);
        await groupRepository.AddAsync(group, ct).ConfigureAwait(false);
        return group;
    }

    public async Task UpdateAsync(Guid id, string name, string description, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var group = await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);
        group.Rename(name, description);
        await groupRepository.UpdateAsync(group, ct).ConfigureAwait(false);
    }

    public async Task SetParentAsync(Guid id, Guid? parentGroupId, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var group = await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);

        if (parentGroupId is { } parentId)
            await EnsureNoParentCycleAsync(id, parentId, group.TenantId, ct).ConfigureAwait(false);

        group.SetParent(parentGroupId);
        await groupRepository.UpdateAsync(group, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);
        await groupRepository.DeleteAsync(id, ct).ConfigureAwait(false);
    }

    public async Task AssignRoleAsync(Guid groupId, Guid roleId, string? scopeType, string? scopeValue, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var group = await GetRequiredAsync(groupId, tenantId, allTenants, ct).ConfigureAwait(false);
        var role = await roleRepository.GetByIdAsync(roleId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Role not found.");

        if (role.TenantId != group.TenantId)
            throw new InvalidOperationException("The role does not belong to the group's tenant.");

        group.AddRole(role, scopeType, scopeValue);
        await groupRepository.UpdateAsync(group, ct).ConfigureAwait(false);
    }

    public async Task RemoveRoleAsync(Guid groupId, Guid roleId, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var group = await GetRequiredAsync(groupId, tenantId, allTenants, ct).ConfigureAwait(false);
        group.RemoveRole(roleId);
        await groupRepository.UpdateAsync(group, ct).ConfigureAwait(false);
    }

    private async Task<Group> GetRequiredAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct)
    {
        var group = await groupRepository.GetByIdAsync(id, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Group not found.");

        if (!allTenants && group.TenantId != tenantId)
            throw new InvalidOperationException("The group does not belong to the active tenant.");

        return group;
    }

    private async Task EnsureParentInTenantAsync(Guid parentGroupId, Guid tenantId, CancellationToken ct)
    {
        var parent = await groupRepository.GetByIdAsync(parentGroupId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Parent group not found.");

        if (parent.TenantId != tenantId)
            throw new InvalidOperationException("The parent group does not belong to the active tenant.");
    }

    private async Task EnsureNoParentCycleAsync(Guid groupId, Guid candidateParentId, Guid tenantId, CancellationToken ct)
    {
        var current = (Guid?)candidateParentId;
        var visited = new HashSet<Guid> { groupId };

        while (current is { } currentId)
        {
            if (!visited.Add(currentId))
                throw new InvalidOperationException("Setting this parent would create a group hierarchy cycle.");

            var parent = await groupRepository.GetByIdAsync(currentId, ct).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Parent group not found.");

            if (parent.TenantId != tenantId)
                throw new InvalidOperationException("The parent group does not belong to the active tenant.");

            current = parent.ParentGroupId;
        }
    }
}
