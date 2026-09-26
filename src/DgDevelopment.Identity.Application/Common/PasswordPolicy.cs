namespace DgDevelopment.Identity.Application.Common;

public static class PasswordPolicy
{
    private const int MinimumLength = 10;

    // A trimmed, locally-shipped list of well-known common/breached passwords - not a live
    // Have I Been Pwned lookup, just a first line of defense against the most obvious choices.
    private static readonly Lazy<HashSet<string>> CommonPasswords = new(LoadCommonPasswords);

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
        if (CommonPasswords.Value.Contains(password))
            throw new ArgumentException("Password is too common. Choose a less predictable password.", nameof(password));
    }

    private static HashSet<string> LoadCommonPasswords()
    {
        var assembly = typeof(PasswordPolicy).Assembly;
        using var stream = assembly.GetManifestResourceStream("DgDevelopment.Identity.Application.Common.CommonPasswords.txt")
            ?? throw new InvalidOperationException("Embedded resource 'CommonPasswords.txt' is missing.");
        using var reader = new StreamReader(stream);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
                set.Add(trimmed);
        }

        return set;
    }
}
