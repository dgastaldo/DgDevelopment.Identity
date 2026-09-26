namespace DgDevelopment.Identity.OAuth.Services;

public interface IOidcIssuerProvider
{
    Uri GetIssuer();
}