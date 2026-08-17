using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Repositories;

public interface IDeviceCodeRepository
{
    Task<DeviceCode?> GetByUserCodeHashAsync(string userCodeHash, CancellationToken ct = default);
    Task<DeviceCode?> GetByDeviceCodeHashAsync(string deviceCodeHash, CancellationToken ct = default);
    Task AddAsync(DeviceCode code, CancellationToken ct = default);
    Task AuthorizeAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task RecordPollAsync(Guid id, DateTime now, CancellationToken ct = default);
    Task MarkAsUsedAsync(Guid id, CancellationToken ct = default);
    Task DeleteExpiredAsync(CancellationToken ct = default);
}
