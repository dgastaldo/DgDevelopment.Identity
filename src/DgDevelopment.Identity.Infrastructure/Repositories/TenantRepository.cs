using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class TenantRepository(IdentityDbContext context) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task<IReadOnlyCollection<Tenant>> GetAllAsync(CancellationToken ct = default)
        => await context.Tenants.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct).ConfigureAwait(false);

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default)
    {
        await context.Tenants.AddAsync(tenant, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<TenantMembership>> GetMembershipsForUserAsync(Guid userId, CancellationToken ct = default)
        => await context.TenantMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Status == TenantMembershipStatus.Active)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyCollection<Tenant>> GetTenantsForUserAsync(Guid userId, CancellationToken ct = default)
        => await (
            from membership in context.TenantMemberships.AsNoTracking()
            join tenant in context.Tenants.AsNoTracking() on membership.TenantId equals tenant.Id
            where membership.UserId == userId && membership.Status == TenantMembershipStatus.Active
            select tenant
        ).ToListAsync(ct).ConfigureAwait(false);

    public Task<TenantMembership?> GetMembershipAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => context.TenantMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, ct);

    public async Task<bool> IsUserMemberAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await context.TenantMemberships
            .AsNoTracking()
            .AnyAsync(m => m.TenantId == tenantId && m.UserId == userId && m.Status == TenantMembershipStatus.Active, ct)
            .ConfigureAwait(false);

    public async Task AddMembershipAsync(TenantMembership membership, CancellationToken ct = default)
    {
        await context.TenantMemberships.AddAsync(membership, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
