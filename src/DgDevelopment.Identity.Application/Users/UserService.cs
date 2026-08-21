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
    IPasswordHasher passwordHasher) : IUserService
{
    public async Task<PagedResult<User>> GetPagedAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;
        var users = await userRepository.GetPagedAsync(search, skip, pageSize, ct).ConfigureAwait(false);
        var total = await userRepository.CountAsync(search, ct).ConfigureAwait(false);
        return new(users, page, pageSize, total);
    }

    public Task<User?> GetAsync(Guid id, CancellationToken ct = default)
        => userRepository.GetByIdAsync(id, ct);

    public async Task<User> CreateAsync(string username, string password, string email, bool isSystemAccount, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var user = new User(username, passwordHasher.HashPassword(password), EmailAddress.FromString(email), isSystemAccount);
        await userRepository.AddAsync(user, ct).ConfigureAwait(false);
        return user;
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(id, ct).ConfigureAwait(false);
        EnsureNotSystemAccount(user);
        user.Deactivate();
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task SetLockAsync(Guid id, bool locked, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(id, ct).ConfigureAwait(false);
        if (locked) user.Lock(); else user.Unlock();
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task ResetPasswordAsync(Guid id, string password, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var user = await GetRequiredAsync(id, ct).ConfigureAwait(false);
        user.SetPassword(passwordHasher.HashPassword(password));
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId, string? scopeType, string? scopeValue, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(userId, ct).ConfigureAwait(false);
        var role = await roleRepository.GetByIdAsync(roleId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Role not found.");
        user.AssignRole(role, scopeType, scopeValue);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(userId, ct).ConfigureAwait(false);
        user.RemoveRole(roleId);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task AssignPermissionAsync(Guid userId, Guid permissionId, string? scopeType, string? scopeValue, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(userId, ct).ConfigureAwait(false);
        var permission = await permissionRepository.GetByIdAsync(permissionId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Permission not found.");
        user.GrantPermission(permission, scopeType, scopeValue);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task RemovePermissionAsync(Guid userId, Guid permissionId, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(userId, ct).ConfigureAwait(false);
        user.RevokePermission(permissionId);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task AssignGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(userId, ct).ConfigureAwait(false);
        var group = await groupRepository.GetByIdAsync(groupId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Group not found.");
        user.AddToGroup(group);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task RemoveGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(userId, ct).ConfigureAwait(false);
        user.RemoveFromGroup(groupId);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    private async Task<User> GetRequiredAsync(Guid id, CancellationToken ct)
        => await GetAsync(id, ct).ConfigureAwait(false) ?? throw new KeyNotFoundException("User not found.");

    private static void EnsureNotSystemAccount(User user)
    {
        if (user.IsSystemAccount)
            throw new InvalidOperationException("System accounts cannot be deactivated.");
    }
}
