namespace DgDevelopment.Identity.Client.Maui.Services;

using Firebase.Messaging;

// UNVERIFIED: written and reviewed against Microsoft's own dotnet-maui-samples push notification
// sample, but never built or run - no Android SDK/emulator or dotnet tooling was available in the
// environment this was written in (see CONTEXT.md). Needs a real device/emulator check once a
// Firebase project and Platforms/Android/google-services.json exist.
public sealed class FirebasePushTokenProvider : IPushTokenProvider
{
    public async Task<string?> GetTokenAsync()
    {
        try
        {
            // FirebaseMessaging.Instance throws if the Firebase Android SDK was never
            // initialized - which happens automatically at app startup, but only once
            // google-services.json exists and is wired into the project via the
            // <GoogleServicesJson> MSBuild item (see the .csproj's comment and CONTEXT.md).
            // Until then, this catch is the expected, graceful failure mode - same
            // "configured or silently returns nothing" shape as AzureNotificationHubNotifier
            // on the server side.
            var token = await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.String>().ConfigureAwait(false);
            return token?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
