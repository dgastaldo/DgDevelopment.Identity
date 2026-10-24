namespace DgDevelopment.Identity.Domain.Services;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}