using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Authorization;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.ValueObjects;
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

    public async Task<IReadOnlyCollection<User>> GetPagedAsync(string? search, int skip, int take, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var query = BuildQuery();
        if (!allTenants)
            query = query.Where(u => _context.TenantMemberships.Any(m => m.UserId == u.Id && m.TenantId == tenantId));
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

    public async Task<int> CountAsync(string? search, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var query = _context.Users.AsNoTracking();
        if (!allTenants)
            query = query.Where(u => _context.TenantMemberships.Any(m => m.UserId == u.Id && m.TenantId == tenantId));
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => EF.Functions.Like(u.Username, $"%{search}%")
                || u.Emails.Any(e => EF.Functions.Like(e.Email.Value, $"%{search}%")));

        return await query.CountAsync(ct).ConfigureAwait(false);
    }

    public async Task<UserStatistics> GetStatisticsAsync(Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var users = _context.Users.AsNoTracking();
        if (!allTenants)
            users = users.Where(u => _context.TenantMemberships.Any(m => m.UserId == u.Id && m.TenantId == tenantId));

        var total = await users.CountAsync(ct).ConfigureAwait(false);
        var active = await users.CountAsync(u => u.IsActive, ct).ConfigureAwait(false);
        var locked = await users.CountAsync(u => u.IsLocked, ct).ConfigureAwait(false);
        var createdLast30Days = await users.CountAsync(u => u.CreatedAt >= since, ct).ConfigureAwait(false);
        var mfaEnabled = await _context.TotpSecrets
            .CountAsync(s => s.IsEnabled && (allTenants || _context.TenantMemberships.Any(m => m.UserId == s.UserId && m.TenantId == tenantId)), ct)
            .ConfigureAwait(false);
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
        ArgumentNullException.ThrowIfNull(user);

        // User is loaded AsNoTracking; attaching the whole graph via Update() would mark
        // client-generated-key children (UserRole/UserPermission/UserGroup) as Modified
        // instead of Added, since EF can't tell new rows from existing ones by key alone.
        // Attach only the root and reconcile each child collection explicitly against
        // what's in the database.
        _context.Entry(user).State = EntityState.Modified;

        var existingRoleIds = await _context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct).ConfigureAwait(false);
        var currentRoleIds = user.Roles.Select(r => r.RoleId).ToHashSet();
        foreach (var removedRoleId in existingRoleIds.Where(id => !currentRoleIds.Contains(id)))
            // TenantId isn't part of UserRole's primary key (UserId+RoleId), so it's irrelevant for a delete stub.
            _context.UserRoles.Remove(new UserRole(Guid.Empty, user.Id, removedRoleId));
        foreach (var role in user.Roles.Where(r => !existingRoleIds.Contains(r.RoleId)))
            _context.UserRoles.Add(role);

        var existingPermissionIds = await _context.UserPermissions
            .Where(up => up.UserId == user.Id)
            .Select(up => up.PermissionId)
            .ToListAsync(ct).ConfigureAwait(false);
        var currentPermissionIds = user.Permissions.Select(p => p.PermissionId).ToHashSet();
        foreach (var removedPermissionId in existingPermissionIds.Where(id => !currentPermissionIds.Contains(id)))
            _context.UserPermissions.Remove(new UserPermission(Guid.Empty, user.Id, removedPermissionId));
        foreach (var permission in user.Permissions.Where(p => !existingPermissionIds.Contains(p.PermissionId)))
            _context.UserPermissions.Add(permission);

        var existingGroupIds = await _context.UserGroups
            .Where(ug => ug.UserId == user.Id)
            .Select(ug => ug.GroupId)
            .ToListAsync(ct).ConfigureAwait(false);
        var currentGroupIds = user.Groups.Select(g => g.GroupId).ToHashSet();
        foreach (var removedGroupId in existingGroupIds.Where(id => !currentGroupIds.Contains(id)))
            _context.UserGroups.Remove(new UserGroup(user.Id, removedGroupId));
        foreach (var group in user.Groups.Where(g => !existingGroupIds.Contains(g.GroupId)))
            _context.UserGroups.Add(group);

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // Emails is an owned collection (OwnsMany) - EF Core rejects a DbSet<UserEmail> query
    // directly, and reconciling it off a detached graph (like UpdateAsync does for the
    // independent join tables above) can't express removal, since UserEmail's constructor
    // always assigns a new Id. Loading WITH tracking and mutating the tracked list in place
    // lets EF's own DetectChanges() emit the correct INSERT/UPDATE/DELETE with no manual
    // bookkeeping - the standard mechanism for owned collections.
    public async Task AddEmailAsync(Guid userId, EmailAddress email, bool isPrimary, CancellationToken ct = default)
    {
        var user = await LoadTrackedForEmailMutationAsync(userId, ct).ConfigureAwait(false);
        user.AddEmail(email, isPrimary);

        // DetectChanges() doesn't reliably notice a brand-new element appended to an owned
        // collection on an already-tracked (Unchanged) root - unlike modifying/removing an
        // existing element, which it does pick up - so the new row needs its state set
        // explicitly, or SaveChanges emits an UPDATE for it instead of an INSERT and throws a
        // concurrency exception (0 rows affected) since no such row exists yet. UserEmail's own
        // nested owned Email (EmailAddress, via OwnsOne) needs the same explicit treatment -
        // setting the parent's state doesn't cascade to it, so left alone it inserts NULL.
        var added = user.Emails.Single(e => e.Email.Value == email.Value);
        var addedEntry = _context.Entry(added);
        addedEntry.State = EntityState.Added;
        addedEntry.Reference(e => e.Email).TargetEntry!.State = EntityState.Added;

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoveEmailAsync(Guid userId, EmailAddress email, CancellationToken ct = default)
    {
        var user = await LoadTrackedForEmailMutationAsync(userId, ct).ConfigureAwait(false);
        user.RemoveEmail(email);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task SetPrimaryEmailAsync(Guid userId, EmailAddress email, CancellationToken ct = default)
    {
        var user = await LoadTrackedForEmailMutationAsync(userId, ct).ConfigureAwait(false);
        user.SetPrimaryEmail(email);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task VerifyEmailAsync(Guid userId, EmailAddress email, CancellationToken ct = default)
    {
        var user = await LoadTrackedForEmailMutationAsync(userId, ct).ConfigureAwait(false);
        user.VerifyEmail(email);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task<User> LoadTrackedForEmailMutationAsync(Guid userId, CancellationToken ct)
        => await _context.Users
            .Include(u => u.Emails)
            .FirstOrDefaultAsync(u => u.Id == userId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User '{userId}' does not exist.");

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
