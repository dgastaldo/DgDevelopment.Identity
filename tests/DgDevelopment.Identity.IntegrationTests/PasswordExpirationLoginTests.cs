namespace DgDevelopment.Identity.IntegrationTests;

using System.Text.RegularExpressions;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers Password.cshtml's expired-password redirect and the /account/expired-password page
/// (PasswordExpirationPolicy, ExpiredPasswordModel), which had zero coverage beyond
/// PasswordExpirationPolicyTests' pure date-math unit tests - nothing previously drove the actual
/// login-time redirect or the update-password page over real HTTP. Each test provisions its own
/// isolated user, for the same reason as MandatoryMfaEnrollmentTests (IntegrationTestFixture is
/// shared across the whole "Integration" collection).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed partial class PasswordExpirationLoginTests(IntegrationTestFixture fixture)
{
    private const string OldPassword = "Test-Old-Password-1!";
    private const string NewPassword = "Brand-New-Password-1!";

    [Fact]
    public async Task UserWithARecentPasswordIsNotRedirectedToUpdateIt()
    {
        var username = $"fresh-password-{Guid.NewGuid():N}";
        await CreateUserAsync(username, backdatePasswordDays: null);

        using var client = CreateFreshClient();
        var (_, challenge) = OidcTestClient.GeneratePkce();
        var redirect = await new OidcTestClient(client).PostPasswordAndGetRedirectAsync(
            username, OldPassword, IntegrationTestConstants.AdminClientId, IntegrationTestConstants.RedirectUri, challenge);

        Assert.DoesNotContain("expired-password", redirect.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UserWithAnExpiredPasswordIsRedirectedToUpdateItInsteadOfCompletingLogin()
    {
        var username = $"expired-password-{Guid.NewGuid():N}";
        // PasswordExpirationPolicy.ExpirationPeriod is 90 days - 91 puts this just past it.
        await CreateUserAsync(username, backdatePasswordDays: 91);

        using var client = CreateFreshClient();
        var (_, challenge) = OidcTestClient.GeneratePkce();
        var redirect = await new OidcTestClient(client).PostPasswordAndGetRedirectAsync(
            username, OldPassword, IntegrationTestConstants.AdminClientId, IntegrationTestConstants.RedirectUri, challenge);

        Assert.Contains("/account/expired-password", redirect.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompletingTheExpiredPasswordUpdateAllowsLoginToProceed()
    {
        var username = $"updating-password-{Guid.NewGuid():N}";
        var userId = await CreateUserAsync(username, backdatePasswordDays: 91);

        using var client = CreateFreshClient();
        var (_, challenge) = OidcTestClient.GeneratePkce();
        var expiredRedirect = await new OidcTestClient(client).PostPasswordAndGetRedirectAsync(
            username, OldPassword, IntegrationTestConstants.AdminClientId, IntegrationTestConstants.RedirectUri, challenge);
        Assert.Contains("/account/expired-password", expiredRedirect.ToString(), StringComparison.OrdinalIgnoreCase);

        var expiredPage = await client.GetAsync(expiredRedirect);
        var expiredBody = await expiredPage.Content.ReadAsStringAsync();
        var antiforgery = ExtractAntiforgery(expiredBody);

        var updatePost = await client.PostAsync(expiredRedirect, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Password"] = NewPassword,
            ["ConfirmPassword"] = NewPassword,
            ["__RequestVerificationToken"] = antiforgery,
        }));

        var afterUpdate = updatePost.Headers.Location
            ?? throw new InvalidOperationException($"Expected a redirect after updating the password, got {(int)updatePost.StatusCode}.");
        Assert.DoesNotContain("expired-password", afterUpdate.ToString(), StringComparison.OrdinalIgnoreCase);

        // Resuming the original /connect/authorize URL (now with a full session) should not loop
        // back to login or expired-password again.
        var resumed = await client.GetAsync(afterUpdate);
        var resumedLocation = resumed.Headers.Location?.ToString() ?? string.Empty;
        Assert.DoesNotContain("/account/login", resumedLocation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expired-password", resumedLocation, StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = fixture.CreateContext();
        var user = await verifyContext.Users.SingleAsync(u => u.Id == userId);
        Assert.True(DateTime.UtcNow - user.PasswordChangedAt < TimeSpan.FromMinutes(1));
    }

    private async Task<Guid> CreateUserAsync(string username, int? backdatePasswordDays)
    {
        await using var context = fixture.CreateContext();
        var tenant = new Tenant(username, $"tenant-{Guid.NewGuid():N}");
        context.Tenants.Add(tenant);

        var hasher = new FakePasswordHasher();
        var email = EmailAddress.FromString($"{username}@example.test");
        var user = new User(username, hasher.HashPassword(OldPassword), email);
        user.VerifyEmail(email);
        context.Users.Add(user);
        context.TenantMemberships.Add(new TenantMembership(tenant.Id, user.Id, isOwner: true));
        await context.SaveChangesAsync().ConfigureAwait(false);

        if (backdatePasswordDays is { } days)
        {
            typeof(User).GetProperty(nameof(User.PasswordChangedAt))!.SetValue(user, DateTime.UtcNow.AddDays(-days));
            context.Users.Update(user);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        return user.Id;
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

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryRegex();
}
