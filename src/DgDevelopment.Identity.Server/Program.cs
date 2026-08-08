using DgDevelopment.Identity.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "DgDevelopment Identity API",
        Version = "v1",
        Description = "Identity Provider API for authentication, authorization, and user management."
    });
});

var app = builder.Build();

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DgDevelopment Identity API v1");
        options.RoutePrefix = "docs";
    });
}

app.Run();
