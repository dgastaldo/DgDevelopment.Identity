var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddConnectionString("IdentityDb");
var redis = builder.AddRedis("Redis");

var server = builder
    .AddProject<Projects.DgDevelopment_Identity_Server>("identity-server")
    .WithExternalHttpEndpoints()
    .WithHttpsEndpoint(port: 7157)
    .WithHttpEndpoint(port: 5281)
    .WithReference(sqlServer)
    .WithReference(redis);

var adminClientSecret = builder.Configuration["Identity:AdminClientSecret"] ?? "";

builder
    .AddProject<Projects.DgDevelopment_Identity_AdminUi>("admin-ui")
    .WithExternalHttpEndpoints()
    .WithHttpsEndpoint(port: 7018)
    .WithHttpEndpoint(port: 5133)
    .WithReference(server)
    .WithEnvironment("IdentityBaseUrl", "https://localhost:7157")
    .WithEnvironment("Identity__AdminClientSecret", adminClientSecret);

builder.Build().Run();
