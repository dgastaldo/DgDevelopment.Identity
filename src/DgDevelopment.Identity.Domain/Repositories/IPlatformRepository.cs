using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IPlatformRepository
{
    Task<Platform?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyCollection<Platform>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Platform platform, CancellationToken ct = default);
    Task UpdateAsync(Platform platform, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
