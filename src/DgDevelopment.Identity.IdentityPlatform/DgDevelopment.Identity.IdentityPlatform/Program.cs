using DgDevelopment.Identity.IdentityPlatform;
using DgDevelopment.Identity.IdentityPlatform.Client.State;
using DgDevelopment.Identity.IdentityPlatform.Components;
using DgDevelopment.Identity.Client.Blazor;
using DgDevelopment.Identity.Client.Core;
using DgDevelopment.Identity.ServiceDefaults;
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
string adminBaseUriString = builder.Configuration.GetValue<string>("AdminBaseUrl") ?? "https://localhost:7018";
string adminClientId = builder.Configuration.GetValue<string>("Identity:AdminClientId") ?? "";
string adminClientSecret = builder.Configuration.GetValue<string>("Identity:AdminClientSecret") ?? "";

builder.Services.AddIdentityAuthentication(new OidcOptions
{
    Authority = identityBaseUriString,
    ClientId = adminClientId,
    ClientSecret = adminClientSecret,
    RedirectUri = new Uri(adminBaseUriString + "/callback"),
    PostLogoutRedirectUri = new Uri(adminBaseUriString + "/"),
    Scopes = ["openid", "profile", "email"]
});

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
    IdentityBaseUrl = identityBaseUriString,
    AdminClientId = adminClientId
}));

app.Run();
