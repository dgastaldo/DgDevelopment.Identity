using DgDevelopment.Identity.IdentityPlatform;
using DgDevelopment.Identity.IdentityPlatform.Client.State;
using DgDevelopment.Identity.IdentityPlatform.Components;
using DgDevelopment.Identity.Client.Blazor;
using DgDevelopment.Identity.Client.Core;
using DgDevelopment.Identity.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Layered on top of appsettings/env vars so it wins when present - written by identity-server's
// DbSeeder (first boot) or its `rotate-admin-client-secret` command (later rotations) onto a
// volume shared between the two containers, see deploy/personal/docker-compose.yml. Optional: a
// no-op both locally (no AdminClientConfigFile set) and before identity-server's first seed
// completes, in which case Identity:AdminClientId/Secret below fall back to "" until the next
// restart picks up the file.
var adminClientConfigFile = builder.Configuration["AdminClientConfigFile"];
if (!string.IsNullOrWhiteSpace(adminClientConfigFile))
    builder.Configuration.AddJsonFile(adminClientConfigFile, optional: true, reloadOnChange: false);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHealthChecks();

string identityBaseUriString = builder.Configuration.GetValue<string>("IdentityBaseUrl") ?? "https://localhost:7157";
// Only needed when IdentityBaseUrl is a server-to-server address the browser can't resolve (e.g.
// http://identity-server:8080 on the Docker network in deploy/personal) - see OidcOptions.PublicAuthority.
// Falls back to IdentityBaseUrl, which is already correct wherever front/back share one address
// (local dev, Aspire).
string identityPublicBaseUriString = builder.Configuration.GetValue<string>("IdentityPublicBaseUrl") ?? identityBaseUriString;
string adminBaseUriString = builder.Configuration.GetValue<string>("AdminBaseUrl") ?? "https://localhost:7018";
// UriBuilder.Uri normalizes a path-less base to "/" on its own - avoids a hand-built trailing
// separator, which also reads as a hardcoded path to static analysis (csharpsquid:S1075).
var adminBaseUri = new UriBuilder(adminBaseUriString).Uri;
string adminClientId = builder.Configuration.GetValue<string>("Identity:AdminClientId") ?? "";
string adminClientSecret = builder.Configuration.GetValue<string>("Identity:AdminClientSecret") ?? "";

builder.Services.AddIdentityAuthentication(new OidcOptions
{
    Authority = identityBaseUriString,
    PublicAuthority = identityPublicBaseUriString,
    ClientId = adminClientId,
    ClientSecret = adminClientSecret,
    RedirectUri = new Uri(adminBaseUri, "callback"),
    PostLogoutRedirectUri = adminBaseUri,
    Scopes = ["openid", "profile", "email"]
});

// [Authorize] on a Razor component also becomes authorization metadata on its SSR endpoint, which
// ASP.NET Core's own AuthorizationMiddleware enforces independently of Blazor's AuthorizeRouteView -
// on a failure it calls ChallengeAsync, which needs a registered IAuthenticationService. This app's
// real auth state comes entirely from ServerIdentityAuthStateProvider's hand-decoded session cookie
// (no ASP.NET Core cookie ticket is ever written), so this scheme exists only to give the challenge
// somewhere to go - LoginPath mirrors RedirectToLogin.razor's client-side redirect for the same case.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => options.LoginPath = "/login");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ServerIdentityAuthStateProvider>();
builder.Services.AddScoped<IdentityAuthStateProvider>(sp => sp.GetRequiredService<ServerIdentityAuthStateProvider>());
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ServerIdentityAuthStateProvider>());

builder.Services.AddHttpClient<IdentityClient>().AddHttpMessageHandler<IdentityRefreshHandler>();
builder.Services.AddScoped<TenantState>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
     .AddAdditionalAssemblies(typeof(DgDevelopment.Identity.IdentityPlatform.Client._Imports).Assembly);

app.MapHealthChecks("/health");
app.MapGet("/config/identity.json", () => Results.Json(new
{
    // The WASM client runs entirely in the browser, so every call it makes - including
    // /connect/authorize - needs the publicly-reachable address, never the server-to-server one.
    IdentityBaseUrl = identityPublicBaseUriString,
    AdminClientId = adminClientId
}));

await app.RunAsync().ConfigureAwait(false);
