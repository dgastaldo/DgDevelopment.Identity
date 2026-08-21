using DgDevelopment.Identity.Client.Core;

namespace DgDevelopment.Identity.IdentityPlatform.Client.State;

public sealed class TenantState(IdentityClient api)
{
    public IReadOnlyCollection<TenantResponse> Tenants { get; private set; } = [];
    public Guid ActiveTenantId { get; private set; }
    public bool IsGlobalAdministrator { get; private set; }
    public bool AllTenants { get; set; }
    public bool Loaded { get; private set; }

    public TenantResponse? ActiveTenant => Tenants.FirstOrDefault(t => t.Id == ActiveTenantId);

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var response = await api.GetMyTenantsAsync(ct).ConfigureAwait(false);
        if (response is null)
            return;

        Tenants = response.Tenants;
        ActiveTenantId = response.ActiveTenantId;
        IsGlobalAdministrator = response.IsGlobalAdministrator;
        Loaded = true;
    }
}
