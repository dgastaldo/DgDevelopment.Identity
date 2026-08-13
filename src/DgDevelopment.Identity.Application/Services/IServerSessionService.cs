using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Application.Services;

public interface IServerSessionService
{
    Task<UserSession?> FindActiveAsync(Guid userId, CancellationToken ct = default);
    Task<UserSession> CreateAsync(User user, bool rememberMe, CancellationToken ct = default);
}