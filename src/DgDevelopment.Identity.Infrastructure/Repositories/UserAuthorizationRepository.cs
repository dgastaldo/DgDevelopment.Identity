using DgDevelopment.Identity.Domain.Authorization;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class UserAuthorizationRepository(IdentityDbContext context) : IUserAuthorizationRepository
{
    private sealed record RawGrant(string PermissionName, string? ScopeType, string? ScopeValue, Guid GrantTenantId, bool IsGlobal);

    public async Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Include(u => u.Permissions)
            .Include(u => u.Groups)
            .SingleOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false);

        if (user is null)
            return [];

        var groups = await context.Groups.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        var groupIds = user.Groups.Select(g => g.GroupId).ToHashSet();
        var visitedGroups = new HashSet<Guid>();

        foreach (var groupId in groupIds.ToArray())
        {
            var current = groups.FirstOrDefault(g => g.Id == groupId);
            while (current is not null && visitedGroups.Add(current.Id))
                current = current.ParentGroupId is { } parentId
                    ? groups.FirstOrDefault(g => g.Id == parentId)
                    : null;
        }

        var groupTenants = groups.Where(g => visitedGroups.Contains(g.Id)).ToDictionary(g => g.Id, g => g.TenantId);

        var grants = new List<RawGrant>();

        var directPermissionIds = user.Permissions.Select(p => p.PermissionId).ToHashSet();
        var directPermissions = await context.Permissions
            .AsNoTracking()
            .Where(p => directPermissionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct)
            .ConfigureAwait(false);

        foreach (var assignment in user.Permissions)
        {
            if (directPermissions.TryGetValue(assignment.PermissionId, out var permission))
                grants.Add(new(permission.Name, assignment.ScopeType, assignment.ScopeValue, assignment.TenantId, permission.IsGlobal));
        }

        var userRoleAssignments = user.Roles.ToDictionary(r => r.RoleId, r => (r.TenantId, r.ScopeType, r.ScopeValue));
        var groupRoleAssignments = await context.GroupRoles
            .AsNoTracking()
            .Where(gr => visitedGroups.Contains(gr.GroupId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var roleIds = userRoleAssignments.Keys
            .Concat(groupRoleAssignments.Select(r => r.RoleId))
            .ToHashSet();

        var rolePermissions = await context.RolePermissions
            .AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var rolePermissionIds = rolePermissions.Select(rp => rp.PermissionId).ToHashSet();
        var permissions = await context.Permissions
            .AsNoTracking()
            .Where(p => rolePermissionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct)
            .ConfigureAwait(false);

        foreach (var rolePermission in rolePermissions)
        {
            if (!permissions.TryGetValue(rolePermission.PermissionId, out var permission))
                continue;

            if (userRoleAssignments.TryGetValue(rolePermission.RoleId, out var userAssignment))
            {
                grants.Add(new(
                    permission.Name,
                    rolePermission.ScopeType ?? userAssignment.ScopeType,
                    rolePermission.ScopeValue ?? userAssignment.ScopeValue,
                    userAssignment.TenantId,
                    permission.IsGlobal));
            }
            else
            {
                var groupScope = groupRoleAssignments.FirstOrDefault(gr => gr.RoleId == rolePermission.RoleId);
                var grantTenantId = groupScope is not null && groupTenants.TryGetValue(groupScope.GroupId, out var gt) ? gt : tenantId;
                grants.Add(new(
                    permission.Name,
                    rolePermission.ScopeType ?? groupScope?.ScopeType,
                    rolePermission.ScopeValue ?? groupScope?.ScopeValue,
                    grantTenantId,
                    permission.IsGlobal));
            }
        }

        return grants
            .Where(g => g.IsGlobal || g.GrantTenantId == tenantId)
            .Select(g => new EffectivePermission(g.PermissionName, g.ScopeType, g.ScopeValue))
            .Distinct()
            .ToArray();
    }

    public async Task<bool> HasAnyRoleAsync(Guid userId, Guid tenantId, IReadOnlyCollection<string> roleNames, CancellationToken ct = default)
    {
        var roleIds = await context.Roles
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && roleNames.Contains(r.Name))
            .Select(r => r.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (roleIds.Count == 0)
            return false;

        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Include(u => u.Groups)
            .SingleOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false);

        if (user is null)
            return false;

        if (user.Roles.Any(r => roleIds.Contains(r.RoleId)))
            return true;

        if (user.Groups.Count == 0)
            return false;

        var groups = await context.Groups.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        var visitedGroups = new HashSet<Guid>();
        foreach (var groupId in user.Groups.Select(g => g.GroupId))
        {
            var current = groups.FirstOrDefault(g => g.Id == groupId);
            while (current is not null && visitedGroups.Add(current.Id))
                current = current.ParentGroupId is { } parentId
                    ? groups.FirstOrDefault(g => g.Id == parentId)
                    : null;
        }

        if (visitedGroups.Count == 0)
            return false;

        return await context.GroupRoles
            .AsNoTracking()
            .AnyAsync(gr => visitedGroups.Contains(gr.GroupId) && roleIds.Contains(gr.RoleId), ct)
            .ConfigureAwait(false);
    }
}
