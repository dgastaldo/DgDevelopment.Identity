var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddConnectionString("IdentityDb");
var redis = builder.AddRedis("Redis");

var server = builder
    .AddProject<Projects.DgDevelopment_Identity_Server>("identity-server", launchProfileName: "https")
    .WithEndpoint("https", endpoint => endpoint.IsProxied = false)
    .WithEndpoint("http", endpoint => endpoint.IsProxied = false)
    .WithReference(sqlServer)
    .WithReference(redis);

var adminClientSecret = builder.Configuration["Identity:AdminClientSecret"] ?? "";

var adminUi = builder
    .AddProject<Projects.DgDevelopment_Identity_AdminUi>("admin-ui", launchProfileName: "https")
    .WithEndpoint("https", endpoint => endpoint.IsProxied = false)
    .WithEndpoint("http", endpoint => endpoint.IsProxied = false)
    .WithReference(server)
    .WithEnvironment("IdentityBaseUrl", "https://localhost:7157")
    .WithEnvironment("Identity__AdminClientSecret", adminClientSecret);

adminUi.WithEnvironment("AdminBaseUrl", adminUi.GetEndpoint("https"));

var app = builder.Build();

await app.RunAsync().ConfigureAwait(false);