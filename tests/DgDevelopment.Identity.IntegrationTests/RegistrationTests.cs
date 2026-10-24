namespace DgDevelopment.Identity.IntegrationTests;

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

[Collection(IntegrationCollection.Name)]
public sealed partial class RegistrationTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task RegisterCreatesNewTenantAndItsOwner()
    {
        using var client = CreateClient();
        var slug = $"register-new-{Guid.NewGuid():N}";
        var username = $"owner-{Guid.NewGuid():N}";
        var email = $"{username}@example.test";

        var getResponse = await client.GetAsync("/account/register");
        var token = ExtractAntiforgery(await getResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = username,
            ["Email"] = email,
            ["Password"] = "Correct-Horse-1",
            ["ConfirmPassword"] = "Correct-Horse-1",
            ["OrganizationName"] = "Register New Co",
            ["OrganizationSlug"] = slug,
            ["__RequestVerificationToken"] = token,
        }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Contains("/account/check-email", postResponse.Headers.Location!.ToString(), StringComparison.Ordinal);

        await using var context = fixture.CreateContext();
        var tenant = await context.Tenants.SingleAsync(t => t.Slug == slug);
        Assert.False(tenant.IsPlatformTenant);

        var user = await context.Users.Include(u => u.Groups).SingleAsync(u => u.Username == username);
        var membership = await context.TenantMemberships.SingleAsync(m => m.TenantId == tenant.Id && m.UserId == user.Id);
        Assert.True(membership.IsOwner);

        var globalAdminsGroup = await context.Groups.SingleAsync(g => g.TenantId == tenant.Id && g.Name == "GlobalAdmins");
        Assert.Contains(user.Groups, g => g.GroupId == globalAdminsGroup.Id);
    }

    [Fact]
    public async Task VerifyEmailWithAValidTokenMarksTheEmailVerified()
    {
        using var client = CreateClient();
        var slug = $"register-verify-{Guid.NewGuid():N}";
        var username = $"verify-{Guid.NewGuid():N}";
        var email = $"{username}@example.test";

        var getResponse = await client.GetAsync("/account/register");
        var token = ExtractAntiforgery(await getResponse.Content.ReadAsStringAsync());

        await client.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = username,
            ["Email"] = email,
            ["Password"] = "Correct-Horse-1",
            ["ConfirmPassword"] = "Correct-Horse-1",
            ["OrganizationName"] = "Register Verify Co",
            ["OrganizationSlug"] = slug,
            ["__RequestVerificationToken"] = token,
        }));

        var emailBody = fixture.Factory.Notifications.GetLastBodySentTo(email);
        var verificationToken = ExtractToken(emailBody!);

        var verifyResponse = await client.GetAsync($"/account/verify-email?token={Uri.EscapeDataString(verificationToken)}");
        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        Assert.Contains("has been verified", verifyBody, StringComparison.Ordinal);

        await using var context = fixture.CreateContext();
        var user = await context.Users.Include(u => u.Emails).SingleAsync(u => u.Username == username);
        Assert.True(user.Emails.Single().IsVerified);
    }

    [Fact]
    public async Task RegisterJoinsAnExistingTenantWithoutElevatedRole()
    {
        using var client = CreateClient();
        var username = $"joiner-{Guid.NewGuid():N}";
        var email = $"{username}@example.test";

        var getResponse = await client.GetAsync("/account/register?tenant=customer-tenant");
        var body = await getResponse.Content.ReadAsStringAsync();
        Assert.Contains("Customer Tenant", body, StringComparison.Ordinal);
        var token = ExtractAntiforgery(body);

        var postResponse = await client.PostAsync("/account/register?tenant=customer-tenant", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = username,
            ["Email"] = email,
            ["Password"] = "Correct-Horse-1",
            ["ConfirmPassword"] = "Correct-Horse-1",
            ["__RequestVerificationToken"] = token,
        }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        await using var context = fixture.CreateContext();
        var customerTenant = await context.Tenants.SingleAsync(t => t.Slug == "customer-tenant");
        var user = await context.Users.Include(u => u.Groups).SingleAsync(u => u.Username == username);
        var membership = await context.TenantMemberships.SingleAsync(m => m.TenantId == customerTenant.Id && m.UserId == user.Id);
        Assert.False(membership.IsOwner);
        Assert.Empty(user.Groups);
    }

    [Fact]
    public async Task ForgotPasswordAlwaysRedirectsToCheckEmailRegardlessOfWhetherTheAccountExists()
    {
        using var client = CreateClient();

        var existingResponse = await PostForgotPasswordAsync(client, IntegrationTestConstants.SuperAdminEmail);
        var nonexistentResponse = await PostForgotPasswordAsync(client, $"no-such-user-{Guid.NewGuid():N}@example.test");

        Assert.Equal(HttpStatusCode.Redirect, existingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, nonexistentResponse.StatusCode);
        Assert.Equal(existingResponse.Headers.Location!.ToString(), nonexistentResponse.Headers.Location!.ToString());

        Assert.NotNull(fixture.Factory.Notifications.GetLastBodySentTo(IntegrationTestConstants.SuperAdminEmail));
    }

    [Fact]
    public async Task ResetPasswordWithAValidTokenChangesThePassword()
    {
        using var client = CreateClient();
        const string resetEmail = "customer.admin@dgdevelopment.it";

        await PostForgotPasswordAsync(client, resetEmail);
        var emailBody = fixture.Factory.Notifications.GetLastBodySentTo(resetEmail);
        var token = ExtractToken(emailBody!);

        var getResponse = await client.GetAsync($"/account/reset-password?token={Uri.EscapeDataString(token)}");
        var pageBody = await getResponse.Content.ReadAsStringAsync();
        var antiforgeryToken = ExtractAntiforgery(pageBody);

        var postResponse = await client.PostAsync("/account/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Token"] = token,
            ["Password"] = "New-Correct-Horse-1",
            ["ConfirmPassword"] = "New-Correct-Horse-1",
            ["__RequestVerificationToken"] = antiforgeryToken,
        }));

        postResponse.EnsureSuccessStatusCode();
        var resultBody = await postResponse.Content.ReadAsStringAsync();
        Assert.Contains("has been reset", resultBody, StringComparison.Ordinal);

        await using var context = fixture.CreateContext();
        var user = await context.Users.SingleAsync(u => u.Username == "customer.admin");
        Assert.Equal(new FakePasswordHasher().HashPassword("New-Correct-Horse-1"), user.PasswordHash);
    }

    private HttpClient CreateClient() => fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri(IdentityWebApplicationFactory.IssuerBaseAddress),
    });

    private static async Task<HttpResponseMessage> PostForgotPasswordAsync(HttpClient client, string email)
    {
        var getResponse = await client.GetAsync("/account/forgot-password");
        var token = ExtractAntiforgery(await getResponse.Content.ReadAsStringAsync());

        return await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["__RequestVerificationToken"] = token,
        }));
    }

    private static string ExtractAntiforgery(string html)
    {
        var match = AntiforgeryRegex().Match(html);
        return match.Success ? match.Groups[1].Value : throw new InvalidOperationException("No __RequestVerificationToken found in the page.");
    }

    private static string ExtractToken(string emailBody)
    {
        var match = TokenRegex().Match(emailBody);
        return match.Success ? match.Groups[1].Value : throw new InvalidOperationException("No token found in the email body.");
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryRegex();

    [GeneratedRegex(@"token=([A-Za-z0-9]+)")]
    private static partial Regex TokenRegex();
}
