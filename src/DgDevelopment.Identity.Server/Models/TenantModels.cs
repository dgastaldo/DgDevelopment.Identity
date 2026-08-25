using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Server.Models;

public sealed record CreateTenantRequest(string Name, string Slug);

public sealed record TenantAdminResponse(Guid Id, string Name, string Slug, bool IsActive, DateTime CreatedAt)
{
    public static TenantAdminResponse From(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return new(tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive, tenant.CreatedAt);
    }
}
