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
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Permission?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == name, ct);
    }

    public async Task<IReadOnlyCollection<Permission>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Permission permission, CancellationToken ct = default)
    {
        await _context.Permissions.AddAsync(permission, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Permission permission, CancellationToken ct = default)
    {
        _context.Permissions.Update(permission);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var permission = await _context.Permissions.FindAsync([id], ct);
        if (permission is not null)
        {
            _context.Permissions.Remove(permission);
            await _context.SaveChangesAsync(ct);
        }
    }
}
