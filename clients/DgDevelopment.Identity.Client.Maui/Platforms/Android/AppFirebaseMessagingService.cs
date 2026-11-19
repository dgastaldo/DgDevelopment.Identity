namespace DgDevelopment.Identity.Client.Maui;

using Android.App;
using Firebase.Messaging;

// UNVERIFIED alongside FirebasePushTokenProvider - see its header comment. [Service]/
// [IntentFilter] generate the AndroidManifest.xml <service> entry at build time; no manual
// manifest edit needed.
[Service(Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class AppFirebaseMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);

        // Deliberately not re-registering with the server here - see CONTEXT.md's "Known,
        // explicitly-accepted gaps". FCM tokens are long-lived in practice; FirebasePushTokenProvider
        // always reads the current one on demand (via MfaPage's "Register device" button), so a
        // stale cached token is never sent - it's just not proactively re-pushed to the server
        // the moment it refreshes.
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);

        // No foreground-specific handling yet - see CONTEXT.md. A backgrounded app already shows
        // FCM's own notification-tray entry from the payload's "notification" block, which
        // AzureNotificationHubNotifier already sends server-side; this override exists so a
        // *foreground* app (which FCM otherwise delivers silently to, with no tray notification)
        // doesn't need that reimplemented here until there's a reason to.
    }
}
