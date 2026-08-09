using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.OAuth.Services;
using DgDevelopment.Identity.Server.Data;
using DgDevelopment.Identity.Server.Services;
using DgDevelopment.Identity.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHealthChecks();

var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found.");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddScoped<IUserAuthenticationService, UserAuthenticationService>();
builder.Services.AddOAuthEngine();
builder.Services.AddScoped<IUserInteractionService, UserInteractionService>();
builder.Services.AddScoped<DbSeeder>();

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddRazorPages();

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

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync().ConfigureAwait(false);
}

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

app.MapControllers();
app.MapRazorPages();

app.Run();
