using DgDevelopment.Identity.AdminUi;
using DgDevelopment.Identity.AdminUi.Components;
using DgDevelopment.Identity.Client.Blazor;
using DgDevelopment.Identity.Client.Core;
using DgDevelopment.Identity.ServiceDefaults;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHealthChecks();

string identityBaseUriString = builder.Configuration.GetValue<string>("IdentityBaseUrl") ?? "https://localhost:7157";
string adminBaseUriString = builder.Configuration.GetValue<string>("AdminBaseUrl") ?? "https://localhost:7018";
string adminClientSecret = builder.Configuration.GetValue<string>("Identity:AdminClientSecret") ?? "";

builder.Services.AddSingleton(new OidcOptions
{
    Authority = identityBaseUriString,
    ClientId = "admin-ui",
    ClientSecret = adminClientSecret,
    RedirectUri = new Uri(adminBaseUriString + "/callback"),
    PostLogoutRedirectUri = new Uri(adminBaseUriString + "/"),
    Scopes = ["openid", "profile", "email"]
});

builder.Services.AddHttpClient<IdentityClient>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITokenStore, CookieTokenStore>();
builder.Services.AddScoped<IdentityAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<IdentityAuthStateProvider>());
builder.Services.AddCascadingAuthenticationState();

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
    .AddAdditionalAssemblies(typeof(DgDevelopment.Identity.AdminUi.Client._Imports).Assembly);

app.MapHealthChecks("/health");

app.Run();
