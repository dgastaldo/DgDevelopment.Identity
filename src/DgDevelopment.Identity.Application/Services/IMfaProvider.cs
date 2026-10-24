namespace DgDevelopment.Identity.Application.Services;

public interface IMfaProvider
{
    string Name { get; }
    Task<bool> IsAvailableAsync(Guid userId, CancellationToken ct = default);
}

public sealed class TotpMfaProvider : IMfaProvider
{
    private readonly ITotpService _totpService;

    public TotpMfaProvider(ITotpService totpService)
    {
        _totpService = totpService;
    }

    public string Name => "totp";

    public Task<bool> IsAvailableAsync(Guid userId, CancellationToken ct = default)
        => _totpService.IsEnabledAsync(userId, ct);
}

public sealed class PushMfaProvider : IMfaProvider
{
    private readonly IPushMfaService _pushMfaService;

    public PushMfaProvider(IPushMfaService pushMfaService)
    {
        _pushMfaService = pushMfaService;
    }

    public string Name => "push";

    public Task<bool> IsAvailableAsync(Guid userId, CancellationToken ct = default)
        => _pushMfaService.HasActiveDevicesAsync(userId, ct);
}