using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IVerificationTokenRepository
{
    Task<VerificationToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddAsync(VerificationToken token, CancellationToken ct = default);
    Task MarkUsedAsync(Guid id, CancellationToken ct = default);
}
