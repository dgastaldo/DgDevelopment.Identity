using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Client?> GetByClientIdAsync(string clientId, CancellationToken ct = default);
    Task<IReadOnlyCollection<Client>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyCollection<string>> GetAllActiveClientIdsAsync(CancellationToken ct = default);
    Task<IReadOnlyCollection<Uri>> GetAllActiveRedirectUrisAsync(CancellationToken ct = default);
    Task AddAsync(Client client, CancellationToken ct = default);
    Task UpdateAsync(Client client, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
