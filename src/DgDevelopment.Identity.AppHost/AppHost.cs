var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddConnectionString("IdentityDb");

var server = builder
    .AddProject<Projects.DgDevelopment_Identity_Server>("identity-server")
    .WithExternalHttpEndpoints()
    .WithReference(sqlServer);

var adminClientSecret = builder.Configuration["Identity:AdminClientSecret"] ?? "";

builder
    .AddProject<Projects.DgDevelopment_Identity_AdminUi>("admin-ui")
    .WithExternalHttpEndpoints()
    .WithReference(server)
    .WithEnvironment("IdentityBaseUrl", "https://localhost:7157")
    .WithEnvironment("Identity__AdminClientSecret", adminClientSecret);

builder.Build().Run();
