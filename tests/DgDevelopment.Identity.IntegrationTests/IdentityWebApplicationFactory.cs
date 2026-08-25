namespace DgDevelopment.Identity.IntegrationTests;

using DgDevelopment.Identity.Domain.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class IdentityWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true;Database=DgDevelopment.Identity.IntegrationTests";

    // OidcIssuerProvider stamps the `iss` claim from the request's own scheme+host, but JWT bearer
    // validation checks it against this fixed ValidIssuer - they only agree in real usage because
    // Kestrel's dev port happens to match the Program.cs fallback. TestServer's default base address
    // doesn't, so the login client below must be pointed at this exact address too.
    public const string IssuerBaseAddress = "https://localhost";

    public IdentityWebApplicationFactory()
    {
        // Program.cs reads ConnectionStrings:IdentityDb into a local variable right after
        // WebApplication.CreateBuilder(args), before builder.Build() runs. ConfigureAppConfiguration
        // below only takes effect at Build() time, which is too late - the value is already captured.
        // Environment variables are picked up by CreateBuilder itself, so this is the only override
        // that actually reaches that early read (without it, the app silently falls back to
        // appsettings.Development.json's real connection string instead of this test database).
        Environment.SetEnvironmentVariable("ConnectionStrings__IdentityDb", ConnectionString);
        Environment.SetEnvironmentVariable("Identity__Issuer", IssuerBaseAddress);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = ConnectionString
            });
        });

        builder.ConfigureServices(services =>
        {
            // The real Argon2id hasher is deliberately slow (64MB RAM, 4 iterations); tests seed
            // and log in through this hasher repeatedly, so swap it for a trivial one.
            services.RemoveAll<IPasswordHasher>();
            services.AddSingleton<IPasswordHasher, FakePasswordHasher>();
        });
    }
}
