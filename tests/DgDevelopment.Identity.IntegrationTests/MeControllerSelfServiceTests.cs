namespace DgDevelopment.Identity.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

[Collection(IntegrationCollection.Name)]
public sealed partial class MeControllerSelfServiceTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task GetMeReturnsTheCallersOwnProfile()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/me");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(IntegrationTestConstants.SuperAdminUserName, body.GetProperty("username").GetString());
        Assert.True(body.GetProperty("emails").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task ChangePasswordWithWrongCurrentPasswordReturnsBadRequest()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.PutAsJsonAsync("/api/v1/me/password", new { currentPassword = "definitely-wrong", newPassword = "New-Correct-Horse-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePasswordWithAWeakNewPasswordReturnsBadRequest()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.PutAsJsonAsync("/api/v1/me/password", new { currentPassword = IntegrationTestConstants.SuperAdminPassword, newPassword = "weak" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePasswordWithCorrectCurrentPasswordSucceedsAndPersists()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.PutAsJsonAsync("/api/v1/me/password", new { currentPassword = IntegrationTestConstants.SuperAdminPassword, newPassword = "New-Correct-Horse-1" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var context = fixture.CreateContext();
        var user = await context.Users.SingleAsync(u => u.Id == fixture.SuperAdminUserId);
        Assert.Equal(new FakePasswordHasher().HashPassword("New-Correct-Horse-1"), user.PasswordHash);
    }

    [Fact]
    public async Task AddEmailIssuesAVerificationLinkThatVerifiesOnlyTheNewEmail()
    {
        using var client = fixture.CreateAuthenticatedClient();
        var newEmail = $"added-{Guid.NewGuid():N}@example.test";

        var addResponse = await client.PostAsJsonAsync("/api/v1/me/emails", new { email = newEmail });
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        var emailBody = fixture.Factory.Notifications.GetLastBodySentTo(newEmail);
        Assert.NotNull(emailBody);
        var token = ExtractToken(emailBody!);

        var verifyResponse = await client.GetAsync($"/account/verify-email?token={Uri.EscapeDataString(token)}");
        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        Assert.Contains("has been verified", verifyBody, StringComparison.Ordinal);

        await using var context = fixture.CreateContext();
        var user = await context.Users.Include(u => u.Emails).SingleAsync(u => u.Id == fixture.SuperAdminUserId);
        var addedEmail = Assert.Single(user.Emails, e => e.Email.Value == newEmail.ToUpperInvariant());
        Assert.True(addedEmail.IsVerified);
        var primaryEmail = user.Emails.Single(e => e.IsPrimary);
        Assert.NotEqual(addedEmail.Id, primaryEmail.Id);
        Assert.False(primaryEmail.Email.Value == addedEmail.Email.Value);
    }

    [Fact]
    public async Task AddingAnEmailAlreadyInUseReturnsBadRequest()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/me/emails", new { email = IntegrationTestConstants.SuperAdminEmail });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemovingThePrimaryEmailWhileAnotherExistsReturnsBadRequest()
    {
        using var client = fixture.CreateAuthenticatedClient();
        var secondEmail = $"guard-{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/v1/me/emails", new { email = secondEmail });

        var response = await client.DeleteAsync($"/api/v1/me/emails/{Uri.EscapeDataString(IntegrationTestConstants.SuperAdminEmail)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemovingANonPrimaryEmailSucceeds()
    {
        using var client = fixture.CreateAuthenticatedClient();
        var secondEmail = $"removable-{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/v1/me/emails", new { email = secondEmail });

        var response = await client.DeleteAsync($"/api/v1/me/emails/{Uri.EscapeDataString(secondEmail)}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var context = fixture.CreateContext();
        var user = await context.Users.Include(u => u.Emails).SingleAsync(u => u.Id == fixture.SuperAdminUserId);
        Assert.DoesNotContain(user.Emails, e => e.Email.Value == secondEmail.ToUpperInvariant());
    }

    [Fact]
    public async Task SetPrimaryEmailRoundTrips()
    {
        using var client = fixture.CreateAuthenticatedClient();
        var secondEmail = $"newprimary-{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/v1/me/emails", new { email = secondEmail });

        var response = await client.PutAsync($"/api/v1/me/emails/{Uri.EscapeDataString(secondEmail)}/primary", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using (var context = fixture.CreateContext())
        {
            var user = await context.Users.Include(u => u.Emails).SingleAsync(u => u.Id == fixture.SuperAdminUserId);
            Assert.Equal(secondEmail.ToUpperInvariant(), user.PrimaryEmail!.Value);
        }

        // Restores the original primary so later tests relying on the seeded superadmin email aren't affected.
        await client.PutAsync($"/api/v1/me/emails/{Uri.EscapeDataString(IntegrationTestConstants.SuperAdminEmail)}/primary", null);
    }

    private static string ExtractToken(string emailBody)
    {
        var match = TokenRegex().Match(emailBody);
        return match.Success ? match.Groups[1].Value : throw new InvalidOperationException("No token found in the email body.");
    }

    [GeneratedRegex(@"token=([A-Za-z0-9]+)")]
    private static partial Regex TokenRegex();
}
