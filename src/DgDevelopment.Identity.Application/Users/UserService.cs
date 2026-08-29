using DgDevelopment.Identity.Application.Common;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.Services;
using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Application.Users;

public sealed class UserService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IGroupRepository groupRepository,
    IClientRepository clientRepository,
    ITenantRepository tenantRepository,
    IPasswordHasher passwordHasher) : IUserService
{
    public async Task<PagedResult<User>> GetPagedAsync(string? search, int page, int pageSize, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;
        var users = await userRepository.GetPagedAsync(search, skip, pageSize, tenantId, allTenants, ct).ConfigureAwait(false);
        var total = await userRepository.CountAsync(search, tenantId, allTenants, ct).ConfigureAwait(false);
        return new(users, page, pageSize, total);
    }

    public async Task<User?> GetAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (user is null)
            return null;

        if (!allTenants && !await tenantRepository.IsUserMemberAsync(tenantId, id, ct).ConfigureAwait(false))
            return null;

        return user;
    }

    public async Task<User> CreateAsync(string username, string password, string email, bool isSystemAccount, Guid tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        PasswordPolicy.Validate(password);

        var user = new User(username, passwordHasher.HashPassword(password), EmailAddress.FromString(email), isSystemAccount);
        await userRepository.AddAsync(user, ct).ConfigureAwait(false);
        await tenantRepository.AddMembershipAsync(new TenantMembership(tenantId, user.Id), ct).ConfigureAwait(false);
        return user;
    }

    public async Task DeactivateAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);
        EnsureNotSystemAccount(user);
        user.Deactivate();
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task SetLockAsync(Guid id, bool locked, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);
        if (locked) user.Lock(); else user.Unlock();
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task<User> ResetPasswordAsync(Guid id, string password, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        PasswordPolicy.Validate(password);
        var user = await GetRequiredAsync(id, tenantId, allTenants, ct).ConfigureAwait(false);
        user.SetPassword(passwordHasher.HashPassword(password));
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        return user;
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId, string? scopeType, string? scopeValue, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(userId, tenantId, allTenants, ct).ConfigureAwait(false);
        var role = await roleRepository.GetByIdAsync(roleId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Role not found.");
        if (role.TenantId != tenantId)
            throw new InvalidOperationException("The role does not belong to the active tenant.");
        await EnsureScopeConsistentWithTenantAsync(tenantId, scopeType, scopeValue, ct).ConfigureAwait(false);

        user.AssignRole(role, scopeType, scopeValue);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task RemoveRoleAsync(Guid userId, Guid roleId, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(userId, tenantId, allTenants, ct).ConfigureAwait(false);
        user.RemoveRole(roleId);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task AssignPermissionAsync(Guid userId, Guid permissionId, string? scopeType, string? scopeValue, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(userId, tenantId, allTenants, ct).ConfigureAwait(false);
        var permission = await permissionRepository.GetByIdAsync(permissionId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Permission not found.");
        await EnsureScopeConsistentWithTenantAsync(tenantId, scopeType, scopeValue, ct).ConfigureAwait(false);

        user.GrantPermission(tenantId, permission, scopeType, scopeValue);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task RemovePermissionAsync(Guid userId, Guid permissionId, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(userId, tenantId, allTenants, ct).ConfigureAwait(false);
        user.RevokePermission(permissionId);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task AssignGroupAsync(Guid userId, Guid groupId, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(userId, tenantId, allTenants, ct).ConfigureAwait(false);
        var group = await groupRepository.GetByIdAsync(groupId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Group not found.");
        if (group.TenantId != tenantId)
            throw new InvalidOperationException("The group does not belong to the active tenant.");

        user.AddToGroup(group);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task RemoveGroupAsync(Guid userId, Guid groupId, Guid tenantId, bool allTenants, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(userId, tenantId, allTenants, ct).ConfigureAwait(false);
        user.RemoveFromGroup(groupId);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    private async Task<User> GetRequiredAsync(Guid id, Guid tenantId, bool allTenants, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(id, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("User not found.");

        if (!allTenants && !await tenantRepository.IsUserMemberAsync(tenantId, id, ct).ConfigureAwait(false))
            throw new InvalidOperationException("The user does not belong to the active tenant.");

        return user;
    }

    private async Task EnsureScopeConsistentWithTenantAsync(Guid tenantId, string? scopeType, string? scopeValue, CancellationToken ct)
    {
        if (!string.Equals(scopeType, "Client", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(scopeValue))
            return;

        var client = await clientRepository.GetByClientIdAsync(scopeValue, ct).ConfigureAwait(false);
        if (client is not null && client.TenantId != tenantId)
            throw new InvalidOperationException("The scoped client does not belong to the active tenant.");
    }

    private static void EnsureNotSystemAccount(User user)
    {
        if (user.IsSystemAccount)
            throw new InvalidOperationException("System accounts cannot be deactivated.");
    }
}
