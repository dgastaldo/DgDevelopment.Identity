const string HttpsEndpoint = "https";

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
    .AddProject<Projects.DgDevelopment_Identity_Server>("identity-server", launchProfileName: HttpsEndpoint)
    .WithEndpoint(HttpsEndpoint, endpoint => endpoint.IsProxied = false)
    .WithEndpoint("http", endpoint => endpoint.IsProxied = false)
    .WithReference(sqlServer);

var adminClientId = builder.Configuration["Identity:AdminClientId"] ?? "";
var adminClientSecret = builder.Configuration["Identity:AdminClientSecret"] ?? "";

var identityPlatform = builder
    .AddProject<Projects.DgDevelopment_Identity_IdentityPlatform>("identity-platform", launchProfileName: HttpsEndpoint)
    .WithEndpoint(HttpsEndpoint, endpoint => endpoint.IsProxied = false)
    .WithEndpoint("http", endpoint => endpoint.IsProxied = false)
    .WithReference(server)
    .WithEnvironment("IdentityBaseUrl", server.GetEndpoint(HttpsEndpoint))
    .WithEnvironment("Identity__AdminClientId", adminClientId)
    .WithEnvironment("Identity__AdminClientSecret", adminClientSecret);

identityPlatform.WithEnvironment("AdminBaseUrl", identityPlatform.GetEndpoint(HttpsEndpoint));

// deploy-target selects the one compute-environment resource relevant to this run - see
// docs/deployment.md's "Aspire-native deployment model". personal/develop are the only cases this
// switch ever gets: docs/integration/main's Azure infrastructure (AKS, App Service, Azure SQL) is
// owned by Terraform (infra/terraform/), not Aspire - see infra/terraform/README.md. Those three
// branches deploy via a plain build-image/push-to-GHCR/kubectl-or-az-webapp step instead, never
// through aspire deploy.
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
