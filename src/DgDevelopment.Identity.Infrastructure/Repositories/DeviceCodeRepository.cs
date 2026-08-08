using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class DeviceCodeRepository : IDeviceCodeRepository
{
    private readonly IdentityDbContext _context;

    public DeviceCodeRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<DeviceCode?> GetByUserCodeHashAsync(string userCodeHash, CancellationToken ct = default)
    {
        return await _context.DeviceCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserCodeHash == userCodeHash, ct);
    }

    public async Task<DeviceCode?> GetByDeviceCodeHashAsync(string deviceCodeHash, CancellationToken ct = default)
    {
        return await _context.DeviceCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DeviceCodeHash == deviceCodeHash, ct);
    }

    public async Task AddAsync(DeviceCode code, CancellationToken ct = default)
    {
        await _context.DeviceCodes.AddAsync(code, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task MarkAsUsedAsync(Guid id, CancellationToken ct = default)
    {
        var code = await _context.DeviceCodes.FindAsync([id], ct);
        if (code is not null)
        {
            code.MarkUsed();
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteExpiredAsync(CancellationToken ct = default)
    {
        await _context.DeviceCodes
            .Where(d => d.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);
    }
}
