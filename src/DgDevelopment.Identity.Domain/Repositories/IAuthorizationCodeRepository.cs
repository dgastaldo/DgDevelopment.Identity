using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IAuthorizationCodeRepository
{
    Task<AuthorizationCode?> GetByCodeHashAsync(string codeHash, CancellationToken ct = default);
    Task AddAsync(AuthorizationCode code, CancellationToken ct = default);
    Task MarkAsUsedAsync(Guid id, CancellationToken ct = default);
    Task DeleteExpiredAsync(CancellationToken ct = default);
}
