using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Authorization;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _context;

    public UserRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Emails)
            .Include(u => u.Claims)
            .Include(u => u.Logins)
            .Include(u => u.Roles)
            .Include(u => u.Permissions)
            .Include(u => u.Groups)
            .FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Emails)
            .Include(u => u.Claims)
            .Include(u => u.Logins)
            .Include(u => u.Roles)
            .Include(u => u.Permissions)
            .Include(u => u.Groups)
            .FirstOrDefaultAsync(u => u.Username == username, ct).ConfigureAwait(false);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        var normalizedEmail = email.ToUpperInvariant();
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Emails)
            .Include(u => u.Claims)
            .Include(u => u.Logins)
            .Include(u => u.Roles)
            .Include(u => u.Permissions)
            .Include(u => u.Groups)
            .Where(u => u.Emails.Any(e => e.Email.Value == normalizedEmail))
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<User?> GetByLoginAsync(string provider, string providerKey, CancellationToken ct = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Emails)
            .Include(u => u.Claims)
            .Include(u => u.Logins)
            .Include(u => u.Roles)
            .Include(u => u.Permissions)
            .Include(u => u.Groups)
            .Where(u => u.Logins.Any(l => l.Provider == provider && l.ProviderKey == providerKey))
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<User>> GetPagedAsync(string? search, int skip, int take, CancellationToken ct = default)
    {
        var query = BuildQuery();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => EF.Functions.Like(u.Username, $"%{search}%")
                || u.Emails.Any(e => EF.Functions.Like(e.Email.Value, $"%{search}%")));

        return await query
            .OrderBy(u => u.Username)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<int> CountAsync(string? search, CancellationToken ct = default)
    {
        var query = _context.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => EF.Functions.Like(u.Username, $"%{search}%")
                || u.Emails.Any(e => EF.Functions.Like(e.Email.Value, $"%{search}%")));

        return await query.CountAsync(ct).ConfigureAwait(false);
    }

    public async Task<UserStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var total = await _context.Users.CountAsync(ct).ConfigureAwait(false);
        var active = await _context.Users.CountAsync(u => u.IsActive, ct).ConfigureAwait(false);
        var locked = await _context.Users.CountAsync(u => u.IsLocked, ct).ConfigureAwait(false);
        var createdLast30Days = await _context.Users.CountAsync(u => u.CreatedAt >= since, ct).ConfigureAwait(false);
        var mfaEnabled = await _context.TotpSecrets.CountAsync(s => s.IsEnabled, ct).ConfigureAwait(false);
        return new(total, active, locked, createdLast30Days, mfaEnabled);
    }

    private IQueryable<User> BuildQuery()
        => _context.Users
            .AsNoTracking()
            .Include(u => u.Emails)
            .Include(u => u.Claims)
            .Include(u => u.Logins)
            .Include(u => u.Roles)
            .Include(u => u.Permissions)
            .Include(u => u.Groups);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await _context.Users.AddAsync(user, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync([id], ct).ConfigureAwait(false);
        if (user is not null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
