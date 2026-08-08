using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IEventStoreRepository
{
    Task AppendAsync(Event @event, CancellationToken ct = default);
    Task<IReadOnlyCollection<Event>> GetByAggregateAsync(Guid aggregateId, CancellationToken ct = default);
    Task<int> GetVersionAsync(Guid aggregateId, CancellationToken ct = default);
}
