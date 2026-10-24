namespace DgDevelopment.Identity.IntegrationTests;

using System.Net.Http.Json;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

/// <summary>
/// Drives a real SignalR connection against SessionHub over the WebApplicationFactory test host -
/// the one piece of the session-revocation feature PR #57 explicitly left uncovered ("no test
/// exercises the SignalR hub connection/push itself"). Confirms the whole path end to end: a
/// client subscribes with a real Bearer token, a real password change happens through the real
/// HTTP endpoint, and the "ForceLogout" message actually arrives - not just that the DB rows get
/// revoked (already covered by MeControllerSelfServiceTests) or that the publisher gets called
/// (already covered by SessionRevocationServiceTests in the unit test project).
///
/// TestServer doesn't support real WebSockets, so the connection is forced onto long polling -
/// the standard workaround for SignalR-over-WebApplicationFactory.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class SessionEventHubTests(IntegrationTestFixture fixture)
{
    private const string Password = "Test-Session-Hub-Password-1!";

    [Fact]
    public async Task ChangingPasswordPushesAForceLogoutEventToASubscribedClient()
    {
        var username = $"hub-user-{Guid.NewGuid():N}";
        Guid userId;
        await using (var context = fixture.CreateContext())
            userId = await CreateUserAsync(context, username);

        using var loginClient = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri(IdentityWebApplicationFactory.IssuerBaseAddress),
        });
        var (_, challenge) = OidcTestClient.GeneratePkce();
        var accessToken = await new OidcTestClient(loginClient).LoginAndGetAccessTokenAsync(
            username, Password, IntegrationTestConstants.AdminClientId, IntegrationTestConstants.AdminClientSecret,
            IntegrationTestConstants.RedirectUri);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(IdentityWebApplicationFactory.IssuerBaseAddress), "/hubs/session"), options =>
            {
                options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

        var forceLogoutReceived = new TaskCompletionSource();
        connection.On("ForceLogout", () => forceLogoutReceived.TrySetResult());

        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeToUser", userId.ToString());

        using var apiClient = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(IdentityWebApplicationFactory.IssuerBaseAddress),
        });
        apiClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var changeResponse = await apiClient.PutAsJsonAsync("/api/v1/me/password", new { currentPassword = Password, newPassword = "Brand-New-Hub-Password-1!" });
        changeResponse.EnsureSuccessStatusCode();

        var completed = await Task.WhenAny(forceLogoutReceived.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.True(completed == forceLogoutReceived.Task, "Expected a ForceLogout message within 10 seconds of the password change.");
    }

    [Fact]
    public async Task SubscribingToAnotherUsersChannelIsRejected()
    {
        var username = $"hub-user-{Guid.NewGuid():N}";
        await using (var context = fixture.CreateContext())
            await CreateUserAsync(context, username);

        using var loginClient = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri(IdentityWebApplicationFactory.IssuerBaseAddress),
        });
        var accessToken = await new OidcTestClient(loginClient).LoginAndGetAccessTokenAsync(
            username, Password, IntegrationTestConstants.AdminClientId, IntegrationTestConstants.AdminClientSecret,
            IntegrationTestConstants.RedirectUri);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(IdentityWebApplicationFactory.IssuerBaseAddress), "/hubs/session"), options =>
            {
                options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

        await connection.StartAsync();

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SubscribeToUser", Guid.NewGuid().ToString()));
    }

    private static async Task<Guid> CreateUserAsync(IdentityDbContext context, string username)
    {
        var tenant = new Tenant(username, $"tenant-{Guid.NewGuid():N}");
        context.Tenants.Add(tenant);

        var hasher = new FakePasswordHasher();
        var email = EmailAddress.FromString($"{username}@example.test");
        var user = new User(username, hasher.HashPassword(Password), email);
        user.VerifyEmail(email);
        context.Users.Add(user);
        context.TenantMemberships.Add(new TenantMembership(tenant.Id, user.Id, isOwner: true));
        await context.SaveChangesAsync().ConfigureAwait(false);

        return user.Id;
    }
}
