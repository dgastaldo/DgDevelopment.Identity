namespace DgDevelopment.Identity.Application.Authorization;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid UserId { get; }
    Guid? ClientId { get; }
    Task<bool> IsGlobalAdministratorAsync(CancellationToken ct = default);
}
