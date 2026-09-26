namespace DgDevelopment.Identity.Server.Services;

using DgDevelopment.Identity.OAuth.Services;

public sealed class UserInteractionService : IUserInteractionService
{
    public string GetLoginUrl(string returnUrl)
    {
        ArgumentNullException.ThrowIfNull(returnUrl);
        return $"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    public string GetConsentUrl(string returnUrl)
    {
        ArgumentNullException.ThrowIfNull(returnUrl);
        return $"/account/consent?returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    public string GetErrorUrl(string errorCode, string? errorDescription)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        var url = $"/error?error={Uri.EscapeDataString(errorCode)}";
        if (errorDescription != null)
            url += $"&errorDescription={Uri.EscapeDataString(errorDescription)}";
        return url;
    }
}
