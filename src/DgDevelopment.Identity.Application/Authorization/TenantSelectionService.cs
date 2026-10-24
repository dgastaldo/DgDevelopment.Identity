using DgDevelopment.Identity.Domain.Repositories;

namespace DgDevelopment.Identity.Application.Authorization;

public sealed class TenantSelectionService(ITenantRepository tenantRepository) : ITenantSelectionService
{
    public async Task<TenantSelectionResult> ResolveAsync(Guid userId, string? requestedTenant, CancellationToken ct = default)
    {
        var tenants = await tenantRepository.GetTenantsForUserAsync(userId, ct).ConfigureAwait(false);
        if (tenants.Count == 0)
            return TenantSelectionResult.DeniedResult;

        if (!string.IsNullOrWhiteSpace(requestedTenant))
        {
            var match = tenants.FirstOrDefault(t =>
                string.Equals(t.Slug, requestedTenant, StringComparison.OrdinalIgnoreCase)
                || (Guid.TryParse(requestedTenant, out var id) && t.Id == id));

            return match is null ? TenantSelectionResult.DeniedResult : TenantSelectionResult.Selected(match.Id);
        }

        return tenants.Count == 1
            ? TenantSelectionResult.Selected(tenants.Single().Id)
            : TenantSelectionResult.NeedsSelection(tenants);
    }
}
