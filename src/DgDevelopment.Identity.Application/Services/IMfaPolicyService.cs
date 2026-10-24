namespace DgDevelopment.Identity.Application.Services;

using DgDevelopment.Identity.Domain.Entities;

public interface IMfaPolicyService
{
    Task<bool> RequiresMfaStepAsync(User user, CancellationToken ct = default);
}

public sealed class MfaPolicyService : IMfaPolicyService
{
    private readonly ITotpService _totpService;
    private readonly IPushMfaService _pushMfaService;

    public MfaPolicyService(ITotpService totpService, IPushMfaService pushMfaService)
    {
        _totpService = totpService;
        _pushMfaService = pushMfaService;
    }

    public async Task<bool> RequiresMfaStepAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.RequireMfa)
            return true;

        if (await _totpService.IsEnabledAsync(user.Id, ct).ConfigureAwait(false))
            return true;

        return await _pushMfaService.HasActiveDevicesAsync(user.Id, ct).ConfigureAwait(false);
    }
}