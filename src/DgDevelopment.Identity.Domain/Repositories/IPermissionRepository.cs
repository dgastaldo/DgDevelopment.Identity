using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IPermissionRepository
{
    Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Permission?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyCollection<Permission>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Permission permission, CancellationToken ct = default);
    Task UpdateAsync(Permission permission, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
