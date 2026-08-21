using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Application.Authorization;

public sealed record TenantSelectionResult(Guid? TenantId, IReadOnlyCollection<Tenant> Choices, bool Denied)
{
    public static TenantSelectionResult Selected(Guid tenantId) => new(tenantId, [], false);
    public static TenantSelectionResult NeedsSelection(IReadOnlyCollection<Tenant> choices) => new(null, choices, false);
    public static TenantSelectionResult DeniedResult { get; } = new(null, [], true);
}

public interface ITenantSelectionService
{
    Task<TenantSelectionResult> ResolveAsync(Guid userId, string? requestedTenant, CancellationToken ct = default);
}
