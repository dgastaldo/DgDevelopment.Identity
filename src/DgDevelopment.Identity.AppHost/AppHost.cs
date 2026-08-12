var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddConnectionString("IdentityDb");
var redis = builder.AddRedis("Redis");

var server = builder
    .AddProject<Projects.DgDevelopment_Identity_Server>("identity-server")
    .WithEndpoint("https", endpoint =>
    {
        endpoint.Port = 7157;
        endpoint.UriScheme = "https";
    })
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5281;
        endpoint.UriScheme = "http";
    })
    .WithReference(sqlServer)
    .WithReference(redis);

var adminClientSecret = builder.Configuration["Identity:AdminClientSecret"] ?? "";

builder
    .AddProject<Projects.DgDevelopment_Identity_AdminUi>("admin-ui")
    .WithEndpoint("https", endpoint =>
    {
        endpoint.Port = 7018;
        endpoint.UriScheme = "https";
    })
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5133;
        endpoint.UriScheme = "http";
    })
    .WithReference(server)
    .WithEnvironment("IdentityBaseUrl", "https://localhost:7157")
    .WithEnvironment("Identity__AdminClientSecret", adminClientSecret);

builder.Build().Run();
