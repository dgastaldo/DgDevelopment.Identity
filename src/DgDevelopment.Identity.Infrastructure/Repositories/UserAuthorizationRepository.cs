using DgDevelopment.Identity.Domain.Authorization;
using DgDevelopment.Identity.Domain.Entities;
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
            .AsSplitQuery()
            .SingleOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false);

        if (user is null)
            return [];

        var groups = await context.Groups.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        var visitedGroups = ResolveVisitedGroups(user.Groups.Select(g => g.GroupId), groups);
        var groupTenants = groups.Where(g => visitedGroups.Contains(g.Id)).ToDictionary(g => g.Id, g => g.TenantId);

        var directPermissionIds = user.Permissions.Select(p => p.PermissionId).ToHashSet();
        var directPermissions = await context.Permissions
            .AsNoTracking()
            .Where(p => directPermissionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct)
            .ConfigureAwait(false);

        var grants = BuildDirectGrants(user.Permissions, directPermissions);

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

        grants.AddRange(BuildRoleGrants(rolePermissions, permissions, userRoleAssignments, groupRoleAssignments, groupTenants, tenantId));

        return grants
            .Where(g => g.IsGlobal || g.GrantTenantId == tenantId)
            .Select(g => new EffectivePermission(g.PermissionName, g.ScopeType, g.ScopeValue))
            .Distinct()
            .ToArray();
    }

    // Walks each group's parent chain to collect the full set of groups a user effectively
    // belongs to (their direct groups plus every ancestor) - shared with HasAnyRoleAsync below,
    // which needed the exact same walk.
    private static HashSet<Guid> ResolveVisitedGroups(IEnumerable<Guid> directGroupIds, IReadOnlyCollection<Group> groups)
    {
        var visitedGroups = new HashSet<Guid>();
        foreach (var groupId in directGroupIds)
        {
            var current = groups.FirstOrDefault(g => g.Id == groupId);
            while (current is not null && visitedGroups.Add(current.Id))
                current = current.ParentGroupId is { } parentId
                    ? groups.FirstOrDefault(g => g.Id == parentId)
                    : null;
        }
        return visitedGroups;
    }

    private static List<RawGrant> BuildDirectGrants(IEnumerable<UserPermission> userPermissions, IReadOnlyDictionary<Guid, Permission> directPermissions)
    {
        var grants = new List<RawGrant>();
        foreach (var assignment in userPermissions)
        {
            if (directPermissions.TryGetValue(assignment.PermissionId, out var permission))
                grants.Add(new(permission.Name, assignment.ScopeType, assignment.ScopeValue, assignment.TenantId, permission.IsGlobal));
        }
        return grants;
    }

    private static List<RawGrant> BuildRoleGrants(
        IEnumerable<RolePermission> rolePermissions,
        IReadOnlyDictionary<Guid, Permission> permissions,
        IReadOnlyDictionary<Guid, (Guid TenantId, string? ScopeType, string? ScopeValue)> userRoleAssignments,
        IReadOnlyCollection<GroupRole> groupRoleAssignments,
        IReadOnlyDictionary<Guid, Guid> groupTenants,
        Guid tenantId)
    {
        var grants = new List<RawGrant>();
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
                continue;
            }

            var groupScope = groupRoleAssignments.FirstOrDefault(gr => gr.RoleId == rolePermission.RoleId);
            var grantTenantId = groupScope is not null && groupTenants.TryGetValue(groupScope.GroupId, out var gt) ? gt : tenantId;
            grants.Add(new(
                permission.Name,
                rolePermission.ScopeType ?? groupScope?.ScopeType,
                rolePermission.ScopeValue ?? groupScope?.ScopeValue,
                grantTenantId,
                permission.IsGlobal));
        }
        return grants;
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
            .AsSplitQuery()
            .SingleOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false);

        if (user is null)
            return false;

        if (user.Roles.Any(r => roleIds.Contains(r.RoleId)))
            return true;

        if (user.Groups.Count == 0)
            return false;

        var groups = await context.Groups.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        var visitedGroups = ResolveVisitedGroups(user.Groups.Select(g => g.GroupId), groups);

        if (visitedGroups.Count == 0)
            return false;

        return await context.GroupRoles
            .AsNoTracking()
            .AnyAsync(gr => visitedGroups.Contains(gr.GroupId) && roleIds.Contains(gr.RoleId), ct)
            .ConfigureAwait(false);
    }
}
