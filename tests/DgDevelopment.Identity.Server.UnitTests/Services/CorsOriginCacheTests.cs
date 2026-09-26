namespace DgDevelopment.Identity.Server.UnitTests.Services;

using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.Services;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

public sealed class CorsOriginCacheTests : IClassFixture<DatabaseFixture<CorsOriginCacheTests>>
{
    private readonly DatabaseFixture<CorsOriginCacheTests> _fixture;

    public CorsOriginCacheTests(DatabaseFixture<CorsOriginCacheTests> fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InitializeAsyncPopulatesOriginsAndRedirectUrisFromSeededClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IdentityDbContext>(_ => _fixture.CreateContext());
        services.AddScoped<IClientRepository, ClientRepository>();
        await using var provider = services.BuildServiceProvider();
        using var cache = new CorsOriginCache(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<CorsOriginCache>>());

        await cache.InitializeAsync();

        // Seeded by TestDbSeeder.SeedClientsAsync - see TestConstants.AdminClientId's client.
        Assert.True(cache.IsAllowedOrigin("https://localhost:7018"));
        Assert.True(cache.IsAllowedRedirectUri("https://localhost:7018/callback"));
        Assert.False(cache.IsAllowedRedirectUri("https://evil.example.com/callback"));
        Assert.False(cache.IsAllowedOrigin("https://evil.example.com"));
    }
}
