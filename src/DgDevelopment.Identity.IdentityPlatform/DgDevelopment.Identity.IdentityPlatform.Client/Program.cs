using DgDevelopment.Identity.Client.Blazor;
using DgDevelopment.Identity.Client.Core;
using DgDevelopment.Identity.IdentityPlatform.Client;
using DgDevelopment.Identity.IdentityPlatform.Client.State;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

using var configClient = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
var identityConfig = await configClient.GetFromJsonAsync<JsonObject>("config/identity.json").ConfigureAwait(false)
    ?? throw new InvalidOperationException("IdentityPlatform configuration is unavailable.");

string identityBaseUriString = identityConfig["IdentityBaseUrl"]?.GetValue<string>()
    ?? throw new InvalidOperationException("IdentityPlatform authority is unavailable.");
string adminBaseUriString = builder.HostEnvironment.BaseAddress;
string adminClientId = identityConfig["AdminClientId"]?.GetValue<string>()
    ?? throw new InvalidOperationException("IdentityPlatform client ID is unavailable.");
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

builder.Services.AddHttpClient<IdentityClient>().AddHttpMessageHandler<IdentityRefreshHandler>();
builder.Services.AddScoped<TenantState>();

await builder.Build().RunAsync().ConfigureAwait(false);
