namespace DgDevelopment.Identity.OAuth.Services;

public interface IDeviceAuthorizationService
{
    Task<DeviceAuthorizationResponse> IssueAsync(string? clientId, string? clientSecret, IReadOnlyCollection<string> scopes, Uri verificationUri, CancellationToken ct = default);
    Task<DeviceApprovalInfo?> GetApprovalAsync(string userCode, CancellationToken ct = default);
    Task<bool> ApproveAsync(string userCode, Guid userId, CancellationToken ct = default);
}