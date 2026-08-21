using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyCollection<TenantMembership>> GetMembershipsForUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyCollection<Tenant>> GetTenantsForUserAsync(Guid userId, CancellationToken ct = default);
    Task<TenantMembership?> GetMembershipAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<bool> IsUserMemberAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task AddMembershipAsync(TenantMembership membership, CancellationToken ct = default);
}
