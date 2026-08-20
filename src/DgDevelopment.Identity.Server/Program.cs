using DgDevelopment.Identity.Application.Consent;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.OAuth.Services;
using DgDevelopment.Identity.Server.Data;
using DgDevelopment.Identity.Server.Services;
using DgDevelopment.Identity.ServiceDefaults;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHealthChecks();

var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found.");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IOidcIssuerProvider, OidcIssuerProvider>();
builder.Services.AddScoped<IUserAuthenticationService, UserAuthenticationService>();
builder.Services.AddScoped<IServerSessionService, ServerSessionService>();
builder.Services.AddScoped<ITotpService, TotpService>();
builder.Services.AddScoped<IPushMfaService, PushMfaService>();
builder.Services.AddScoped<IMfaPolicyService, MfaPolicyService>();
builder.Services.AddScoped<IMfaProvider, TotpMfaProvider>();
builder.Services.AddScoped<IMfaProvider, PushMfaProvider>();
builder.Services.AddOAuthEngine();
builder.Services.AddScoped<IUserInteractionService, UserInteractionService>();
builder.Services.AddScoped<IConsentService, ConsentService>();
builder.Services.Configure<ConsentOptions>(builder.Configuration.GetSection(ConsentOptions.SectionName));
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddSingleton<IClientIdCache, ClientIdCache>();
builder.Services.AddSingleton<ICorsOriginCache, CorsOriginCache>();

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    .AddCookie("Identity.Partial", options =>
    {
        options.LoginPath = "/account/login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
        options.SlidingExpiration = true;
        options.Cookie.Name = ".DgDevelopment.Identity.Partial";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

builder.Services.AddSignalR();
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddRazorPages();

builder.Services.AddCors();
builder.Services.AddSingleton<ICorsPolicyProvider, DynamicCorsPolicyProvider>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "DgDevelopment Identity API";
        document.Info.Version = "v1";
        document.Info.Description = "Identity Provider API for authentication, authorization, and user management.";
        return Task.CompletedTask;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync().ConfigureAwait(false);
}

var clientIdCache = app.Services.GetRequiredService<IClientIdCache>();
await clientIdCache.InitializeAsync().ConfigureAwait(false);

var corsOriginCache = app.Services.GetRequiredService<ICorsOriginCache>();
await corsOriginCache.InitializeAsync().ConfigureAwait(false);

app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(options =>
    {
        options.WithOpenApiRoutePattern("/openapi/v1.json");
    });

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "DgDevelopment Identity API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapGet("/", () => Results.Content(System.IO.File.ReadAllText(
    Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html")), "text/html"));

app.MapHealthChecks("/health");

app.UseAuthentication();
app.UseAuthorization();
app.UseCors();

app.MapControllers();
app.MapRazorPages();
app.MapHub<DgDevelopment.Identity.Server.Hubs.MfaHub>("/hubs/mfa");

await app.RunAsync();
