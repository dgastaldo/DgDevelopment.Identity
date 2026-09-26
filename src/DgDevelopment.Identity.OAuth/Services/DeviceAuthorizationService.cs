namespace DgDevelopment.Identity.OAuth.Services;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.ValueObjects;

public sealed class DeviceAuthorizationService(
    IClientValidator clientValidator,
    IDeviceCodeRepository deviceCodeRepository,
    IClientRepository clientRepository) : IDeviceAuthorizationService
{
    private const int LifetimeSeconds = 900;
    private const int PollIntervalSeconds = 5;

    private static readonly char[] _userCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public async Task<DeviceAuthorizationResponse> IssueAsync(string? clientId, string? clientSecret, IReadOnlyCollection<string> scopes, Uri verificationUri, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(verificationUri);

        var validation = await clientValidator.ValidateAsync(clientId, clientSecret, "device_code", ct).ConfigureAwait(false);
        if (!validation.IsValid || validation.Client is null)
            throw new DeviceAuthorizationException("invalid_client", validation.ErrorDescription ?? "Invalid client.");

        var client = validation.Client;
        foreach (var scope in scopes)
        {
            if (!client.Scopes.Any(s => s.Scope == scope))
                throw new DeviceAuthorizationException("invalid_scope", $"Scope '{scope}' not allowed for this client.");
        }

        var deviceCode = Secret.Generate(32);
        var userCode = GenerateUserCode();

        var deviceCodeHash = Hash(deviceCode);
        var userCodeHash = Hash(NormalizeUserCode(userCode));
        var stored = new DeviceCode(deviceCodeHash, userCodeHash, client.Id, scopes.ToArray(), LifetimeSeconds);
        await deviceCodeRepository.AddAsync(stored, ct).ConfigureAwait(false);

        var verificationUriComplete = $"{verificationUri}?user_code={Uri.EscapeDataString(userCode)}";
        return new DeviceAuthorizationResponse(deviceCode, userCode, verificationUri.ToString(), verificationUriComplete, LifetimeSeconds, PollIntervalSeconds);
    }

    public async Task<DeviceApprovalInfo?> GetApprovalAsync(string userCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userCode))
            return null;

        var stored = await deviceCodeRepository.GetByUserCodeHashAsync(Hash(NormalizeUserCode(userCode)), ct).ConfigureAwait(false);
        if (stored is null || stored.IsExpired() || stored.IsAuthorized || stored.IsUsed)
            return null;

        var client = await clientRepository.GetByIdAsync(stored.ClientId, ct).ConfigureAwait(false);
        if (client is null)
            return null;

        return new DeviceApprovalInfo(stored.Id, client.Name, stored.GetScopes());
    }

    public async Task<bool> ApproveAsync(string userCode, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userCode))
            return false;

        var stored = await deviceCodeRepository.GetByUserCodeHashAsync(Hash(NormalizeUserCode(userCode)), ct).ConfigureAwait(false);
        if (stored is null || stored.IsExpired() || stored.IsUsed)
            return false;

        await deviceCodeRepository.AuthorizeAsync(stored.Id, tenantId, userId, ct).ConfigureAwait(false);
        return true;
    }

    private static string NormalizeUserCode(string userCode)
        => userCode.Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();

    private static string GenerateUserCode()
    {
        var result = new char[9];
        for (var i = 0; i < 8; i++)
        {
            var index = i < 4 ? i : i + 1;
            result[index] = _userCodeAlphabet[RandomNumberGenerator.GetInt32(_userCodeAlphabet.Length)];
        }

        result[4] = '-';
        return new string(result);
    }

    private static string Hash(string value)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}