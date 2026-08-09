using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Application.Services;

public interface IUserAuthenticationService
{
    Task<User?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default);
    Task RecordFailedLoginAsync(User user, CancellationToken ct = default);
    Task RecordSuccessfulLoginAsync(User user, CancellationToken ct = default);
}
