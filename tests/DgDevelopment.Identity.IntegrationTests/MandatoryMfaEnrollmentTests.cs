namespace DgDevelopment.Identity.IntegrationTests;

using System.Text.RegularExpressions;
using DgDevelopment.Identity.Application.Tenants;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Services;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers AuthorizeModel's mandatory-MFA-for-privileged-roles check (MfaEnforcementService),
/// which had zero coverage beyond the service's own unit tests - nothing previously drove the
/// actual /connect/authorize redirect or the /account/mfa-enroll page over real HTTP. Every test
/// here provisions its own isolated tenant/user rather than touching the fixture's shared
/// superadmin/customer.admin accounts, since IntegrationTestFixture is one shared instance across
/// the whole "Integration" collection (see IntegrationCollection) - mutating a shared account's
/// MfaGracePeriodStartedAt would risk affecting unrelated tests that log in as the same account.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed partial class MandatoryMfaEnrollmentTests(IntegrationTestFixture fixture)
{
    private const string Password = "Test-GlobalAdmin-Password-1!";

    [Fact]
    public async Task FreshPrivilegedUserWithoutMfaIsNotBlockedImmediately()
    {
        var username = $"fresh-admin-{Guid.NewGuid():N}";
        Guid userId;
        await using (var context = fixture.CreateContext())
            userId = await CreatePrivilegedUserAsync(context, username);

        using var client = CreateFreshClient();
        var (_, challenge) = OidcTestClient.GeneratePkce();
        var redirect = await new OidcTestClient(client).LoginAndGetAuthorizeRedirectAsync(
            username, Password, IntegrationTestConstants.AdminClientId, IntegrationTestConstants.RedirectUri, challenge);

        Assert.DoesNotContain("mfa-enroll", redirect.ToString(), StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = fixture.CreateContext();
        var user = await verifyContext.Users.SingleAsync(u => u.Id == userId);
        Assert.NotNull(user.MfaGracePeriodStartedAt);
    }

    [Fact]
    public async Task NonPrivilegedUserIsNeverAskedToEnrollRegardlessOfGracePeriod()
    {
        var username = $"regular-member-{Guid.NewGuid():N}";
        await using (var context = fixture.CreateContext())
        {
            var tenant = new Tenant(username, $"tenant-{Guid.NewGuid():N}");
            context.Tenants.Add(tenant);
            var hasher = new FakePasswordHasher();
            var email = EmailAddress.FromString($"{username}@example.test");
            var user = new User(username, hasher.HashPassword(Password), email);
            user.VerifyEmail(email);
            context.Users.Add(user);
            context.TenantMemberships.Add(new TenantMembership(tenant.Id, user.Id, isOwner: true));
            await context.SaveChangesAsync();
        }

        using var client = CreateFreshClient();
        var (_, challenge) = OidcTestClient.GeneratePkce();
        var redirect = await new OidcTestClient(client).LoginAndGetAuthorizeRedirectAsync(
            username, Password, IntegrationTestConstants.AdminClientId, IntegrationTestConstants.RedirectUri, challenge);

        Assert.DoesNotContain("mfa-enroll", redirect.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrivilegedUserWhoseGracePeriodHasElapsedIsRedirectedToMfaEnroll()
    {
        var username = $"overdue-admin-{Guid.NewGuid():N}";
        await using (var context = fixture.CreateContext())
        {
            var userId = await CreatePrivilegedUserAsync(context, username);
            await BackdateGracePeriodAsync(context, userId);
        }

        using var client = CreateFreshClient();
        var (_, challenge) = OidcTestClient.GeneratePkce();
        var redirect = await new OidcTestClient(client).LoginAndGetAuthorizeRedirectAsync(
            username, Password, IntegrationTestConstants.AdminClientId, IntegrationTestConstants.RedirectUri, challenge);

        Assert.Contains("/account/mfa-enroll", redirect.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompletingEnrollmentMeansSubsequentLoginsAreNoLongerRedirectedToEnroll()
    {
        var username = $"enrolling-admin-{Guid.NewGuid():N}";
        await using (var context = fixture.CreateContext())
        {
            var userId = await CreatePrivilegedUserAsync(context, username);
            await BackdateGracePeriodAsync(context, userId);
        }

        using var firstClient = CreateFreshClient();
        var (_, firstChallenge) = OidcTestClient.GeneratePkce();
        var enrollRedirect = await new OidcTestClient(firstClient).LoginAndGetAuthorizeRedirectAsync(
            username, Password, IntegrationTestConstants.AdminClientId, IntegrationTestConstants.RedirectUri, firstChallenge);
        Assert.Contains("/account/mfa-enroll", enrollRedirect.ToString(), StringComparison.OrdinalIgnoreCase);

        var enrollPage = await firstClient.GetAsync(enrollRedirect);
        var enrollBody = await enrollPage.Content.ReadAsStringAsync();
        var secret = ExtractManualKey(enrollBody);
        var antiforgery = ExtractAntiforgery(enrollBody);
        var code = TotpGenerator.ComputeCode(secret, TotpGenerator.GetCurrentTimeStep());

        var enablePost = await firstClient.PostAsync(enrollRedirect, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Code"] = code,
            ["__RequestVerificationToken"] = antiforgery,
        }));
        enablePost.EnsureSuccessStatusCode();
        var enabledBody = await enablePost.Content.ReadAsStringAsync();
        Assert.Contains("now active", enabledBody, StringComparison.OrdinalIgnoreCase);

        // Re-issuing /connect/authorize on the same (already fully-authenticated) client - rather
        // than logging in again - is deliberate: Password.cshtml's LoginModel silently re-signs-in
        // a user with an already-active session instead of showing the password form, which would
        // make a second from-scratch login not actually exercise this page. Hitting /connect/authorize
        // directly is also the more faithful test of what matters here: does *this* endpoint's MFA
        // check still correctly pass now that TOTP is enrolled.
        var (_, secondChallenge) = OidcTestClient.GeneratePkce();
        var secondAuthorizeUrl = "/connect/authorize"
            + $"?client_id={Uri.EscapeDataString(IntegrationTestConstants.AdminClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(IntegrationTestConstants.RedirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString("openid profile email")}"
            + $"&code_challenge={Uri.EscapeDataString(secondChallenge)}"
            + "&code_challenge_method=S256";
        var secondResponse = await firstClient.GetAsync(secondAuthorizeUrl);
        var secondRedirect = secondResponse.Headers.Location
            ?? throw new InvalidOperationException($"Expected a redirect from {secondAuthorizeUrl}, got {(int)secondResponse.StatusCode}.");
        Assert.DoesNotContain("mfa-enroll", secondRedirect.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Guid> CreatePrivilegedUserAsync(IdentityDbContext context, string username)
    {
        var provisioning = new TenantProvisioningService(
            new TenantRepository(context), new PlatformRepository(context), new PermissionRepository(context),
            new RoleRepository(context), new GroupRepository(context));
        var provisioned = await provisioning.ProvisionAsync($"Tenant-{username}", $"tenant-{Guid.NewGuid():N}").ConfigureAwait(false);

        var hasher = new FakePasswordHasher();
        var email = EmailAddress.FromString($"{username}@example.test");
        var user = new User(username, hasher.HashPassword(Password), email);
        user.VerifyEmail(email);
        user.AddToGroup(provisioned.GlobalAdminsGroup);
        context.Users.Add(user);
        context.TenantMemberships.Add(new TenantMembership(provisioned.Tenant.Id, user.Id, isOwner: true));
        await context.SaveChangesAsync().ConfigureAwait(false);

        return user.Id;
    }

    private static async Task BackdateGracePeriodAsync(IdentityDbContext context, Guid userId)
    {
        var user = await context.Users.SingleAsync(u => u.Id == userId).ConfigureAwait(false);
        // MfaEnforcementService.GracePeriod is 14 days - 15 puts this user just past it, without
        // hardcoding the exact boundary here.
        typeof(User).GetProperty(nameof(User.MfaGracePeriodStartedAt))!.SetValue(user, DateTime.UtcNow.AddDays(-15));
        context.Users.Update(user);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private HttpClient CreateFreshClient() => fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri(IdentityWebApplicationFactory.IssuerBaseAddress),
    });

    private static string ExtractAntiforgery(string html)
    {
        var match = AntiforgeryRegex().Match(html);
        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException("No __RequestVerificationToken found in the page.");
    }

    private static string ExtractManualKey(string html)
    {
        var match = ManualKeyRegex().Match(html);
        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException("No enrollment secret <code> block found in the page.");
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryRegex();

    [GeneratedRegex("<code[^>]*>([^<]+)</code>")]
    private static partial Regex ManualKeyRegex();
}
