using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class GroupRepository : IGroupRepository
{
    private readonly IdentityDbContext _context;

    public GroupRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Group?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Groups
            .AsNoTracking()
            .Include(g => g.Roles)
            .FirstOrDefaultAsync(g => g.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Group>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Groups
            .AsNoTracking()
            .Include(g => g.Roles)
            .OrderBy(g => g.Name)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Group>> GetChildrenAsync(Guid parentGroupId, CancellationToken ct = default)
    {
        return await _context.Groups
            .AsNoTracking()
            .Include(g => g.Roles)
            .Where(g => g.ParentGroupId == parentGroupId)
            .OrderBy(g => g.Name)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task AddAsync(Group group, CancellationToken ct = default)
    {
        await _context.Groups.AddAsync(group, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Group group, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        // Group is loaded AsNoTracking; attaching the whole graph via Update() would mark
        // client-generated-key children (GroupRole) as Modified instead of Added, since EF
        // can't tell new rows from existing ones by key alone. Attach only the root and
        // reconcile the child collection explicitly against what's in the database.
        _context.Entry(group).State = EntityState.Modified;

        var existingRoleIds = await _context.GroupRoles
            .Where(gr => gr.GroupId == group.Id)
            .Select(gr => gr.RoleId)
            .ToListAsync(ct).ConfigureAwait(false);

        var currentRoleIds = group.Roles.Select(r => r.RoleId).ToHashSet();

        foreach (var removedRoleId in existingRoleIds.Where(id => !currentRoleIds.Contains(id)))
            _context.GroupRoles.Remove(new GroupRole(group.Id, removedRoleId));

        foreach (var role in group.Roles.Where(r => !existingRoleIds.Contains(r.RoleId)))
            _context.GroupRoles.Add(role);

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var group = await _context.Groups.FindAsync([id], ct).ConfigureAwait(false);
        if (group is not null)
        {
            _context.Groups.Remove(group);
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
