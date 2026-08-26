using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Application.Tenants;

/// <param name="GlobalAdminRole">
/// The tenant's standard full-access role, created for every tenant regardless of
/// <see cref="Tenant.IsPlatformTenant"/> - holds every permission in the tenant's own catalog.
/// The name doesn't grant cross-tenant power by itself; that's still gated entirely by
/// <see cref="Tenant.IsPlatformTenant"/> (see ITenantContext.IsGlobalAdministratorAsync).
/// </param>
/// <param name="GlobalAdminsGroup">Holds <paramref name="GlobalAdminRole"/>.</param>
/// <param name="SuperAdminRole">
/// Only set for the platform tenant - the special role tied to the one seeded bootstrap system
/// account (see DbSeeder), never created for a customer tenant. Not the same thing as
/// <paramref name="GlobalAdminRole"/>: a platform tenant has both.
/// </param>
/// <param name="SuperAdminsGroup">Holds <paramref name="SuperAdminRole"/>; null iff it is.</param>
public sealed record TenantProvisioningResult(
    Tenant Tenant,
    Platform Platform,
    Role GlobalAdminRole,
    Group GlobalAdminsGroup,
    Role? SuperAdminRole,
    Group? SuperAdminsGroup);

public interface ITenantProvisioningService
{
    /// <summary>
    /// Creates a new tenant with its own "IdentityAdmin" platform, its own copy of the standard
    /// identity-platform.* permission catalog (scoped to that platform), a GlobalAdmin role holding
    /// all of them, and a GlobalAdmins group holding that role - plus, only for the platform tenant,
    /// an additional SuperAdmin role/group for the seeded bootstrap system account. Does not create
    /// a Client or a User - those are a separate decision (who logs into the new tenant) left to
    /// the caller.
    /// </summary>
    /// <param name="isPlatformTenant">
    /// True only for the one bootstrap tenant that owns the platform itself - never for a tenant
    /// created afterward for a customer through the admin API. Controls whether this tenant's own
    /// GlobalAdmin counts as a genuine global administrator (see ITenantContext.IsGlobalAdministratorAsync)
    /// and whether a SuperAdmin role/group is created at all.
    /// </param>
    Task<TenantProvisioningResult> ProvisionAsync(string name, string slug, bool isPlatformTenant = false, CancellationToken ct = default);
}
