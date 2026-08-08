using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IEventStoreRepository
{
    Task AppendAsync(DomainEvent domainEvent, CancellationToken ct = default);
    Task<IReadOnlyCollection<DomainEvent>> GetByAggregateAsync(Guid aggregateId, CancellationToken ct = default);
    Task<int> GetVersionAsync(Guid aggregateId, CancellationToken ct = default);
}
