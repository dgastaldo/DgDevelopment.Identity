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
    Task<TenantProvisioningResult> ProvisionAsync(string name, string slug, CancellationToken ct = default);
}
