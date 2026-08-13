using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.Services;

namespace DgDevelopment.Identity.Application.Services;

public sealed class UserAuthenticationService : IUserAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private const int MaxFailedAttempts = 5;

    public UserAuthenticationService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<User?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByUsernameAsync(username, ct).ConfigureAwait(false);
        user ??= await _userRepository.GetByEmailAsync(username, ct).ConfigureAwait(false);

        if (user == null || !user.IsActive)
            return null;

        if (user.IsLocked)
        {
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
                return null;
            user.Unlock();
        }

        if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            await RecordFailedLoginAsync(user, ct).ConfigureAwait(false);
            return null;
        }

        return user;
    }

    public Task RecordFailedLoginAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.RecordFailedLogin();
        if (user.FailedLoginAttempts >= MaxFailedAttempts)
            user.Lock();

        return _userRepository.UpdateAsync(user, ct);
    }

    public Task RecordSuccessfulLoginAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.IsLocked)
            user.Unlock();
        else
            user.FailedLoginAttempts = 0;

        return _userRepository.UpdateAsync(user, ct);
    }
}
