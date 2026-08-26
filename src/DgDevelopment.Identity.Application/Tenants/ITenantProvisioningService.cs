using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Application.Tenants;

public sealed record TenantProvisioningResult(Tenant Tenant, Platform Platform, Role SuperAdminRole, Group SuperAdminsGroup);

public interface ITenantProvisioningService
{
    /// <summary>
    /// Creates a new tenant with its own "IdentityAdmin" platform, its own copy of the standard
    /// identity-platform.* permission catalog (scoped to that platform), a SuperAdmin role holding
    /// all of them, and a SuperAdmins group holding that role. Does not create a Client or a User -
    /// those are a separate decision (who logs into the new tenant) left to the caller.
    /// </summary>
    /// <param name="isPlatformTenant">
    /// True only for the one bootstrap tenant that owns the platform itself - never for a tenant
    /// created afterward for a customer through the admin API. Controls whether this tenant's own
    /// SuperAdmin counts as a genuine global administrator (see ITenantContext.IsGlobalAdministratorAsync).
    /// </param>
    Task<TenantProvisioningResult> ProvisionAsync(string name, string slug, bool isPlatformTenant = false, CancellationToken ct = default);
}
