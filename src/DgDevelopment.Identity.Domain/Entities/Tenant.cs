namespace DgDevelopment.Identity.Domain.Entities;

public sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public bool IsActive { get; private set; }

    // True only for the one tenant that owns the platform itself (the bootstrap "Identity
    // Tenant"), never for a tenant created afterward through TenantProvisioningService for a
    // customer. This is what IsGlobalAdministratorAsync checks IN ADDITION to holding the
    // tenant.read permission - without it, any tenant's own SuperAdmin (who holds every
    // permission in their own tenant-scoped catalog, tenant.read included) would count as a
    // global administrator too, since permission catalogs are duplicated per tenant.
    public bool IsPlatformTenant { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Tenant() { }

    public Tenant(string name, string slug, bool isPlatformTenant = false)
    {
        Id = Guid.NewGuid();
        Name = name;
        Slug = slug;
        IsActive = true;
        IsPlatformTenant = isPlatformTenant;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Rename(string name, string slug)
    {
        Name = name;
        Slug = slug;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
