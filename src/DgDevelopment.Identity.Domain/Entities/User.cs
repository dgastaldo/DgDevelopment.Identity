using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTime PasswordChangedAt { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsLocked { get; private set; }
    public bool IsSystemAccount { get; private set; }
    public bool RequireMfa { get; private set; }
    public DateTime? LockoutEnd { get; private set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? MfaGracePeriodStartedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<UserEmail> _emails = [];
    private readonly List<UserClaim> _claims = [];
    private readonly List<UserLogin> _logins = [];
    private readonly List<UserRole> _roles = [];
    private readonly List<UserPermission> _permissions = [];
    private readonly List<UserGroup> _groups = [];

    public IReadOnlyCollection<UserEmail> Emails => _emails.AsReadOnly();
    public IReadOnlyCollection<UserClaim> Claims => _claims.AsReadOnly();
    public IReadOnlyCollection<UserLogin> Logins => _logins.AsReadOnly();
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();
    public IReadOnlyCollection<UserPermission> Permissions => _permissions.AsReadOnly();
    public IReadOnlyCollection<UserGroup> Groups => _groups.AsReadOnly();

    public EmailAddress? PrimaryEmail => _emails.FirstOrDefault(e => e.IsPrimary)?.Email;

    private User() { }

    public User(string username, string passwordHash, EmailAddress primaryEmail, bool isSystemAccount = false)
    {
        Id = Guid.NewGuid();
        Username = username;
        PasswordHash = passwordHash;
        PasswordChangedAt = DateTime.UtcNow;
        IsActive = true;
        IsLocked = false;
        IsSystemAccount = isSystemAccount;
        FailedLoginAttempts = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        _emails.Add(new UserEmail(Id, primaryEmail, isPrimary: true));
    }

    public void AddEmail(EmailAddress email, bool isPrimary = false)
    {
        ArgumentNullException.ThrowIfNull(email);
        if (_emails.Any(e => e.Email.Value == email.Value))
            throw new InvalidOperationException($"Email {email.Value} is already associated with this user.");

        var userEmail = new UserEmail(Id, email, isPrimary);

        if (isPrimary)
        {
            foreach (var existing in _emails.Where(e => e.IsPrimary))
                existing.SetPrimary(false);
        }

        _emails.Add(userEmail);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPrimaryEmail(EmailAddress email)
    {
        ArgumentNullException.ThrowIfNull(email);
        var existing = _emails.FirstOrDefault(e => e.Email.Value == email.Value)
            ?? throw new InvalidOperationException($"Email {email.Value} is not associated with this user.");

        foreach (var e in _emails.Where(e => e.IsPrimary))
            e.SetPrimary(false);

        existing.SetPrimary(true);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveEmail(EmailAddress email)
    {
        ArgumentNullException.ThrowIfNull(email);
        var existing = _emails.FirstOrDefault(e => e.Email.Value == email.Value)
            ?? throw new InvalidOperationException($"Email {email.Value} is not associated with this user.");

        if (existing.IsPrimary && !_emails.Any(e => !e.IsPrimary))
            throw new InvalidOperationException("Cannot remove the primary email when no other email exists.");

        _emails.Remove(existing);
        UpdatedAt = DateTime.UtcNow;
    }

    public void VerifyEmail(EmailAddress email)
    {
        ArgumentNullException.ThrowIfNull(email);
        var existing = _emails.FirstOrDefault(e => e.Email.Value == email.Value)
            ?? throw new InvalidOperationException($"Email {email.Value} is not associated with this user.");

        existing.Verify();
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddClaim(string type, string value)
    {
        _claims.Add(new UserClaim(Id, type, value));
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveClaim(string type)
    {
        _claims.RemoveAll(c => c.Type == type);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddLogin(string provider, string providerKey, string? displayName = null)
    {
        _logins.Add(new UserLogin(Id, provider, providerKey, displayName));
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveLogin(string provider, string providerKey)
    {
        _logins.RemoveAll(l => l.Provider == provider && l.ProviderKey == providerKey);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        PasswordChangedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void StartMfaGracePeriodIfNotStarted()
    {
        if (MfaGracePeriodStartedAt is not null)
            return;

        MfaGracePeriodStartedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetMfaRequired(bool required)
    {
        if (RequireMfa == required)
            return;

        RequireMfa = required;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Lock(DateTime? lockoutEnd = null)
    {
        IsLocked = true;
        LockoutEnd = lockoutEnd ?? DateTime.UtcNow.AddMinutes(15);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unlock()
    {
        IsLocked = false;
        LockoutEnd = null;
        FailedLoginAttempts = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignRole(Role role, string? scopeType = null, string? scopeValue = null)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (_roles.Any(r => r.RoleId == role.Id))
            return;

        _roles.Add(new UserRole(role.TenantId, Id, role.Id, scopeType, scopeValue));
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveRole(Guid roleId)
    {
        _roles.RemoveAll(r => r.RoleId == roleId);
        UpdatedAt = DateTime.UtcNow;
    }

    public void GrantPermission(Guid tenantId, Permission permission, string? scopeType = null, string? scopeValue = null)
    {
        ArgumentNullException.ThrowIfNull(permission);
        if (_permissions.Any(p => p.PermissionId == permission.Id))
            return;

        _permissions.Add(new UserPermission(tenantId, Id, permission.Id, scopeType, scopeValue));
        UpdatedAt = DateTime.UtcNow;
    }

    public void RevokePermission(Guid permissionId)
    {
        _permissions.RemoveAll(p => p.PermissionId == permissionId);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddToGroup(Group group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (_groups.Any(g => g.GroupId == group.Id))
            return;

        _groups.Add(new UserGroup(Id, group.Id));
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveFromGroup(Guid groupId)
    {
        _groups.RemoveAll(g => g.GroupId == groupId);
        UpdatedAt = DateTime.UtcNow;
    }
}
