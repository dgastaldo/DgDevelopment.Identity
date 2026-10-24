namespace DgDevelopment.Identity.Application.Authorization;

public interface ITenantAccessValidator
{
    Task<bool> CanAccessTenantAsync(Guid requestedTenantId, CancellationToken ct = default);
}
