using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken ct = default);
    Task<IReadOnlyCollection<AuditLog>> GetAsync(string? actorType = null, Guid? actorId = null,
        string? action = null, DateTime? from = null, DateTime? endDate = null, int skip = 0, int take = 50,
        CancellationToken ct = default);
}
