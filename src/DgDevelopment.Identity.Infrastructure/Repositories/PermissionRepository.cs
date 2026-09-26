using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class PermissionRepository : IPermissionRepository
{
    private readonly IdentityDbContext _context;

    public PermissionRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<Permission?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == name, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Permission>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Permission>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task AddAsync(Permission permission, CancellationToken ct = default)
    {
        await _context.Permissions.AddAsync(permission, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Permission permission, CancellationToken ct = default)
    {
        _context.Permissions.Update(permission);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var permission = await _context.Permissions.FindAsync([id], ct).ConfigureAwait(false);
        if (permission is not null)
        {
            _context.Permissions.Remove(permission);
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
