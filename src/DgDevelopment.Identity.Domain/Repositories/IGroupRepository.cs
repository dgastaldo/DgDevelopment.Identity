using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyCollection<Group>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyCollection<Group>> GetChildrenAsync(Guid parentGroupId, CancellationToken ct = default);
    Task AddAsync(Group group, CancellationToken ct = default);
    Task UpdateAsync(Group group, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
