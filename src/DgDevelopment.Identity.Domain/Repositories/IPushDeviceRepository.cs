using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IPushDeviceRepository
{
    Task<IReadOnlyCollection<PushDevice>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<PushDevice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(PushDevice device, CancellationToken ct = default);
    Task UpdateAsync(PushDevice device, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}