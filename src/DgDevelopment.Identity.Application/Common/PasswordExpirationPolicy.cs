namespace DgDevelopment.Identity.Application.Common;

public static class PasswordExpirationPolicy
{
    public static readonly TimeSpan ExpirationPeriod = TimeSpan.FromDays(90);

    public static bool IsExpired(DateTime passwordChangedAt)
        => DateTime.UtcNow - passwordChangedAt >= ExpirationPeriod;
}
