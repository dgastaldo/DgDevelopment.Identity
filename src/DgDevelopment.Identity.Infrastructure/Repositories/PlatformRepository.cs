using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class PlatformRepository : IPlatformRepository
{
    private readonly IdentityDbContext _context;

    public PlatformRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Platform?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Platforms
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyCollection<Platform>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Platforms
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Platform platform, CancellationToken ct = default)
    {
        await _context.Platforms.AddAsync(platform, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Platform platform, CancellationToken ct = default)
    {
        _context.Platforms.Update(platform);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var platform = await _context.Platforms.FindAsync([id], ct);
        if (platform is not null)
        {
            _context.Platforms.Remove(platform);
            await _context.SaveChangesAsync(ct);
        }
    }
}
