using DgDevelopment.Identity.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
