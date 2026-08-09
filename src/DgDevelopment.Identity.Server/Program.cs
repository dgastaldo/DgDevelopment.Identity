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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    // 4. Configura Scalar (punta al JSON di Microsoft)
    app.MapScalarApiReference(options =>
    {
        options.WithOpenApiRoutePattern("/openapi/v1.json");
    });

    // 5. Configura Swagger UI (puntandolo allo STESSO JSON di Microsoft)
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "DgDevelopment Identity API v1");
        options.RoutePrefix = "swagger"; // Sarà raggiungibile a /swagger
    });
}

app.MapGet("/", () => "DgDevelopment Identity API is running.");
app.MapHealthChecks("/health");

app.Run();
