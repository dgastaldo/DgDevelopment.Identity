namespace DgDevelopment.Identity.Client.Maui.Services;

public interface IPushTokenProvider
{
    Task<string?> GetTokenAsync();
}

// Fallback for every platform without a real push token provider wired up yet (iOS, Windows -
// Android is the only one with a real implementation, FirebasePushTokenProvider under
// Platforms/Android). Registered in MauiProgram so the DI container still resolves
// IPushTokenProvider on those builds instead of throwing when MfaPage is activated - see
// CONTEXT.md's "Known, explicitly-accepted gaps" for what each platform still needs.
public sealed class UnsupportedPushTokenProvider : IPushTokenProvider
{
    public Task<string?> GetTokenAsync() => Task.FromResult<string?>(null);
}
