var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddConnectionString("IdentityDb");

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

var app = builder.Build();

await app.RunAsync().ConfigureAwait(false);
