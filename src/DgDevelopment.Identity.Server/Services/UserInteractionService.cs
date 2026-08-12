namespace DgDevelopment.Identity.Server.Services;

using DgDevelopment.Identity.OAuth.Services;

internal sealed class UserInteractionService : IUserInteractionService
{
    public string GetLoginUrl(string returnUrl)
        => $"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}";

    public string GetConsentUrl(string returnUrl)
        => $"/account/consent?returnUrl={Uri.EscapeDataString(returnUrl)}";

    public string GetErrorUrl(string error, string? errorDescription)
    {
        var url = $"/error?error={Uri.EscapeDataString(error)}";
        if (errorDescription != null)
            url += $"&errorDescription={Uri.EscapeDataString(errorDescription)}";
        return url;
    }
}
