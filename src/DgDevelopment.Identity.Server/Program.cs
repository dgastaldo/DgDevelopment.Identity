using DgDevelopment.Identity.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHealthChecks();

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

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "DgDevelopment Identity API";
    options.Theme = ScalarTheme.Purple;
});

app.MapGet("/", () => "DgDevelopment Identity API is running.");
app.MapHealthChecks("/health");

app.Logger.LogInformation("DgDevelopment Identity Server starting on {Urls}", string.Join(", ", app.Urls));

app.Run();
