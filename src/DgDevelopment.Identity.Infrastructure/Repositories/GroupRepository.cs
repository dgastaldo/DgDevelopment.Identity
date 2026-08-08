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
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task<IReadOnlyCollection<Group>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Groups
            .AsNoTracking()
            .Include(g => g.Roles)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<Group>> GetChildrenAsync(Guid parentGroupId, CancellationToken ct = default)
    {
        return await _context.Groups
            .AsNoTracking()
            .Include(g => g.Roles)
            .Where(g => g.ParentGroupId == parentGroupId)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Group group, CancellationToken ct = default)
    {
        await _context.Groups.AddAsync(group, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Group group, CancellationToken ct = default)
    {
        _context.Groups.Update(group);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var group = await _context.Groups.FindAsync([id], ct);
        if (group is not null)
        {
            _context.Groups.Remove(group);
            await _context.SaveChangesAsync(ct);
        }
    }
}
