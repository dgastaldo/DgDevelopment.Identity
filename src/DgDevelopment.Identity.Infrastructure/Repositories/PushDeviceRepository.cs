using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class PushDeviceRepository : IPushDeviceRepository
{
    private readonly IdentityDbContext _context;

    public PushDeviceRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<PushDevice>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.PushDevices
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<PushDevice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.PushDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct).ConfigureAwait(false);
    }

    public async Task AddAsync(PushDevice device, CancellationToken ct = default)
    {
        await _context.PushDevices.AddAsync(device, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(PushDevice device, CancellationToken ct = default)
    {
        _context.PushDevices.Update(device);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var device = await _context.PushDevices.FindAsync([id], ct).ConfigureAwait(false);
        if (device is not null)
        {
            _context.PushDevices.Remove(device);
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}