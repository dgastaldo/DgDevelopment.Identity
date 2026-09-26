using System.Diagnostics.CodeAnalysis;
using DgDevelopment.Identity.Client.Core;

namespace DgDevelopment.Identity.IdentityPlatform.Client.State;

[SuppressMessage("Design", "CA1515", Justification = "Must stay public: the host project (DgDevelopment.Identity.IdentityPlatform, a separate assembly) registers it directly via builder.Services.AddScoped<TenantState>() in its own Program.cs. The analyzer only sees this assembly and can't detect that cross-project reference.")]
public sealed class TenantState(IdentityClient api)
{
    public IReadOnlyCollection<TenantResponse> Tenants { get; private set; } = [];
    public Guid ActiveTenantId { get; private set; }
    public bool IsGlobalAdministrator { get; private set; }
    public bool AllTenants { get; set; }
    public bool Loaded { get; private set; }

    public TenantResponse? ActiveTenant => Tenants.FirstOrDefault(t => t.Id == ActiveTenantId);

    // MainLayout's own AuthorizeView content picks up this state naturally because MainLayout
    // calls StateHasChanged on itself once LoadAsync completes - but a sibling component like
    // NavMenu never gets told to re-render just because MainLayout did, so it needs to subscribe
    // to this event and re-render itself to reflect state loaded after its own first paint.
    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var response = await api.GetMyTenantsAsync(ct).ConfigureAwait(false);
        if (response is null)
            return;

        Tenants = response.Tenants;
        ActiveTenantId = response.ActiveTenantId;
        IsGlobalAdministrator = response.IsGlobalAdministrator;
        Loaded = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
