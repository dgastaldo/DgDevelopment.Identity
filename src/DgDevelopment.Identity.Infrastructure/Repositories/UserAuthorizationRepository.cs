using DgDevelopment.Identity.Domain.Authorization;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class UserAuthorizationRepository(IdentityDbContext context) : IUserAuthorizationRepository
{
    public async Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default)
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

        var grants = new List<EffectivePermission>();

        var directPermissionIds = user.Permissions.Select(p => p.PermissionId).ToHashSet();
        var directPermissions = await context.Permissions
            .AsNoTracking()
            .Where(p => directPermissionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct)
            .ConfigureAwait(false);

        foreach (var assignment in user.Permissions)
        {
            if (directPermissions.TryGetValue(assignment.PermissionId, out var permission))
                grants.Add(new(permission.Name, assignment.ScopeType, assignment.ScopeValue));
        }

        var userRoleAssignments = user.Roles.ToDictionary(r => r.RoleId, r => (r.ScopeType, r.ScopeValue));
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

            if (userRoleAssignments.TryGetValue(rolePermission.RoleId, out var userScope))
                grants.Add(new(permission.Name, rolePermission.ScopeType ?? userScope.ScopeType, rolePermission.ScopeValue ?? userScope.ScopeValue));
            else
            {
                var groupScope = groupRoleAssignments.FirstOrDefault(gr => gr.RoleId == rolePermission.RoleId);
                grants.Add(new(permission.Name, rolePermission.ScopeType ?? groupScope?.ScopeType, rolePermission.ScopeValue ?? groupScope?.ScopeValue));
            }
        }

        return grants
            .Distinct()
            .ToArray();
    }
}
