namespace DgDevelopment.Identity.IntegrationTests;

using DgDevelopment.Identity.Domain.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;

public sealed class IdentityWebApplicationFactory : WebApplicationFactory<Program>
{
    // LocalDB (the previous approach) only exists on Windows, so this never ran on the Linux
    // self-hosted CI runner. Shared across every IdentityWebApplicationFactory instance in the
    // process, same reasoning as DatabaseFixture<T> in Server.UnitTests.
    private static readonly Lazy<Task<MsSqlContainer>> SharedContainer = new(StartContainerAsync);

    private static async Task<MsSqlContainer> StartContainerAsync()
    {
        // Without MSSQL_MEMORY_LIMIT_MB, SQL Server on Linux claims up to 80% of host physical RAM
        // by default - on the self-hosted runner (7 GB total, shared with SonarQube/Postgres) that
        // was starving everything else and triggering the kernel OOM killer. Note this is only an
        // internal soft target SQL Server imposes on itself, not a Docker/cgroup hard limit - a 2
        // GB cap still crashed sqlservr outright (SQLPAL fatal error, errno 11) even with test
        // collections serialized, so this needs real headroom above what the rest of the box uses,
        // not just "enough for this dataset's size." Kept in sync with Server.UnitTests'
        // DatabaseFixture<T>, which hits the same crash for the same reason - see its comment for
        // why this settled on 3 GB (not higher) on GitHub-hosted ubuntu-latest.
        var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
            .WithEnvironment("MSSQL_MEMORY_LIMIT_MB", "3072")
            .Build();
        await container.StartAsync();
        return container;
    }

    public string ConnectionString { get; private set; } = string.Empty;

    // OidcIssuerProvider stamps the `iss` claim from the request's own scheme+host, but JWT bearer
    // validation checks it against this fixed ValidIssuer - they only agree in real usage because
    // Kestrel's dev port happens to match the Program.cs fallback. TestServer's default base address
    // doesn't, so the login client below must be pointed at this exact address too.
    public const string IssuerBaseAddress = "https://localhost";

    public CapturingNotificationService Notifications { get; } = new();

    // Must be awaited (by IntegrationTestFixture.InitializeAsync) before anything else touches
    // ConnectionString or triggers host startup via Factory.CreateClient()/.Server.
    public async Task InitializeConnectionAsync()
    {
        var container = await SharedContainer.Value;
        ConnectionString = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = $"DgDevelopment.Identity.IntegrationTests.{Guid.NewGuid():N}",
        }.ConnectionString;

        // Program.cs reads ConnectionStrings:IdentityDb into a local variable right after
        // WebApplication.CreateBuilder(args), before builder.Build() runs. ConfigureAppConfiguration
        // below only takes effect at Build() time, which is too late - the value is already captured.
        // Environment variables are picked up by CreateBuilder itself, so this is the only override
        // that actually reaches that early read (without it, the app silently falls back to
        // appsettings.Development.json's real connection string instead of this test database).
        Environment.SetEnvironmentVariable("ConnectionStrings__IdentityDb", ConnectionString);
        Environment.SetEnvironmentVariable("Identity__Issuer", IssuerBaseAddress);

        // Same early-read timing concern as the two variables above (Program.cs reads the
        // "RateLimiting" section into a local before Build() runs) - production defaults (20
        // permits/minute on the "auth" policy) would trip almost immediately once dozens of
        // integration tests share this one host instance. Rate-limiting behavior itself is
        // covered deterministically by RateLimiterFactoryTests (unit test, fake HttpContext, no
        // shared server) instead of relying on a real 429 here.
        Environment.SetEnvironmentVariable("RateLimiting__Global__PermitLimit", "1000000");
        Environment.SetEnvironmentVariable("RateLimiting__Global__WindowSeconds", "60");
        Environment.SetEnvironmentVariable("RateLimiting__Auth__PermitLimit", "1000000");
        Environment.SetEnvironmentVariable("RateLimiting__Auth__WindowSeconds", "60");
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

            // Captures registration/password-recovery emails instead of sending them, so tests
            // can extract the verification/reset link exactly as a real recipient would.
            services.RemoveAll<INotificationService>();
            services.AddSingleton<INotificationService>(Notifications);
        });
    }
}
