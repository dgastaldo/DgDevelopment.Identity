namespace DgDevelopment.Identity.OAuth.Services;

public interface IUserInteractionService
{
    string GetLoginUrl(string returnUrl);
    string GetConsentUrl(string returnUrl);
    string GetErrorUrl(string error, string? errorDescription);
}
