namespace DgDevelopment.Identity.Application.Common;

public static class PasswordPolicy
{
    private const int MinimumLength = 10;

    public static void Validate(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (password.Length < MinimumLength)
            throw new ArgumentException($"Password must be at least {MinimumLength} characters long.", nameof(password));
        if (!password.Any(char.IsUpper))
            throw new ArgumentException("Password must contain at least one uppercase letter.", nameof(password));
        if (!password.Any(char.IsLower))
            throw new ArgumentException("Password must contain at least one lowercase letter.", nameof(password));
        if (!password.Any(char.IsDigit))
            throw new ArgumentException("Password must contain at least one digit.", nameof(password));
        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            throw new ArgumentException("Password must contain at least one special character.", nameof(password));
    }
}
