var builder = DistributedApplication.CreateBuilder(args);

var deployTarget = builder.Configuration["Parameters:deploy-target"];

// IdentityDb: containerized for personal/develop (the Ubuntu host has no LocalDB, and the whole
// point is no manual DB admin there) - Aspire provisions and auto-migrates/seeds it as part of the
// same Compose stack. Bring-your-own everywhere else (local dev today; Azure SQL for
// docs/integration/main once those environments are provisioned).
IResourceBuilder<IResourceWithConnectionString> sqlServer = deployTarget is "personal" or "develop"
    ? builder.AddSqlServer("sql").AddDatabase("IdentityDb")
    : builder.AddConnectionString("IdentityDb");

var server = builder
    .AddProject<Projects.DgDevelopment_Identity_Server>("identity-server", launchProfileName: "https")
    .WithEndpoint("https", endpoint => endpoint.IsProxied = false)
    .WithEndpoint("http", endpoint => endpoint.IsProxied = false)
    .WithReference(sqlServer);

var adminClientId = builder.Configuration["Identity:AdminClientId"] ?? "";
var adminClientSecret = builder.Configuration["Identity:AdminClientSecret"] ?? "";

var identityPlatform = builder
    .AddProject<Projects.DgDevelopment_Identity_IdentityPlatform>("identity-platform", launchProfileName: "https")
    .WithEndpoint("https", endpoint => endpoint.IsProxied = false)
    .WithEndpoint("http", endpoint => endpoint.IsProxied = false)
    .WithReference(server)
    .WithEnvironment("IdentityBaseUrl", server.GetEndpoint("https"))
    .WithEnvironment("Identity__AdminClientId", adminClientId)
    .WithEnvironment("Identity__AdminClientSecret", adminClientSecret);

identityPlatform.WithEnvironment("AdminBaseUrl", identityPlatform.GetEndpoint("https"));

// deploy-target selects the one compute-environment resource relevant to this run - see
// docs/deployment.md's "Aspire-native deployment model". Only personal/develop are wired up so far
// (Docker Compose, self-hosted runner on the home PC); docs/integration/main get their own case
// (and NuGet packages: Aspire.Hosting.Azure.AppService / Aspire.Hosting.Azure.Kubernetes) once
// those environments are actually provisioned - not yet, per explicit scope.
switch (deployTarget)
{
    case "personal":
    case "develop":
        builder.AddDockerComposeEnvironment("docker-compose");
        server.WithExternalHttpEndpoints();
        identityPlatform.WithExternalHttpEndpoints();
        break;
    // null/other: no environment resource added - today's local dev-orchestration behavior, untouched
}

var app = builder.Build();

await app.RunAsync().ConfigureAwait(false);
