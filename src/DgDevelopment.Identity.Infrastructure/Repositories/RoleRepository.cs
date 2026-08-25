using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly IdentityDbContext _context;

    public RoleRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Name == name, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Role>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .OrderBy(r => r.Name)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task AddAsync(Role role, CancellationToken ct = default)
    {
        await _context.Roles.AddAsync(role, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Role role, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        // Role is loaded AsNoTracking; attaching the whole graph via Update() would mark
        // client-generated-key children (RolePermission) as Modified instead of Added,
        // since EF can't tell new rows from existing ones by key alone. Attach only the
        // root and reconcile the child collection explicitly against what's in the database.
        _context.Entry(role).State = EntityState.Modified;

        var existingPermissionIds = await _context.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct).ConfigureAwait(false);

        var currentPermissionIds = role.Permissions.Select(p => p.PermissionId).ToHashSet();

        foreach (var removedPermissionId in existingPermissionIds.Where(id => !currentPermissionIds.Contains(id)))
            _context.RolePermissions.Remove(new RolePermission(role.Id, removedPermissionId));

        foreach (var permission in role.Permissions.Where(p => !existingPermissionIds.Contains(p.PermissionId)))
            _context.RolePermissions.Add(permission);

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _context.Roles.FindAsync([id], ct).ConfigureAwait(false);
        if (role is not null)
        {
            _context.Roles.Remove(role);
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
