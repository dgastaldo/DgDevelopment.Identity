using Microsoft.EntityFrameworkCore;
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
