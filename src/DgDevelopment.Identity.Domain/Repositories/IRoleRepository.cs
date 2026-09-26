using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Role?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyCollection<Role>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Role role, CancellationToken ct = default);
    Task UpdateAsync(Role role, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
