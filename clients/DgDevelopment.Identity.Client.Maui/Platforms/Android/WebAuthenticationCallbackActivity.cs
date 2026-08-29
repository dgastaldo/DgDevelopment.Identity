namespace DgDevelopment.Identity.Client.Maui;

using Android.App;
using Android.Content;
using Android.Content.PM;

// Receives the OS-level redirect back into the app after the system browser completes the
// /connect/authorize flow, for the "dgidentityapp://callback" scheme registered on the server's
// Public MAUI client (see DbSeeder.SeedMauiClientAsync and AppConfig.RedirectUri).
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "dgidentityapp",
    DataHost = "callback")]
public sealed class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
