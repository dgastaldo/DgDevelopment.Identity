var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddConnectionString("IdentityDb");

var server = builder
    .AddProject<Projects.DgDevelopment_Identity_Server>("identity-server")
    .WithReference(sqlServer);

builder.Build().Run();
