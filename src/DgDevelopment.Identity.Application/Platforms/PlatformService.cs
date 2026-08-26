using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Platforms;

public sealed class PlatformService(IPlatformRepository platformRepository) : IPlatformService
{
    public async Task<PagedResult<Platform>> GetPagedAsync(string? search, int page, int pageSize, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var platforms = (await platformRepository.GetAllAsync(ct).ConfigureAwait(false))
            .Where(p => allTenants || p.TenantId == tenantId)
            .Where(p => string.IsNullOrWhiteSpace(search) || p.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name)
            .ToList();

        var total = platforms.Count;
        var items = platforms.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new(items, page, pageSize, total);
    }

    public async Task<Platform?> GetAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var platform = await platformRepository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (platform is null)
            return null;

        return !allTenants && platform.TenantId != tenantId ? null : platform;
    }

    public async Task<Platform> CreateAsync(string name, string description, PermissionMode permissionMode, Guid tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var platform = new Platform(tenantId, name, description, permissionMode);
        await platformRepository.AddAsync(platform, ct).ConfigureAwait(false);
        return platform;
    }

    public async Task UpdateAsync(Guid id, string name, string description, PermissionMode permissionMode, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var platform = await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);
        platform.Update(name, description, permissionMode);
        await platformRepository.UpdateAsync(platform, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);
        await platformRepository.DeleteAsync(id, ct).ConfigureAwait(false);
    }

    private async Task<Platform> GetRequiredAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct)
    {
        var platform = await platformRepository.GetByIdAsync(id, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Platform not found.");

        if (!allTenants && platform.TenantId != tenantId)
            throw new InvalidOperationException("The platform does not belong to the active tenant.");

        return platform;
    }
}
