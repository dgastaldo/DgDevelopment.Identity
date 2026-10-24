namespace DgDevelopment.Identity.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DgDevelopment.Identity.Domain.Services;

[Collection(IntegrationCollection.Name)]
public sealed class MfaControllerSelfServiceTests(IntegrationTestFixture fixture)
{
    // Regression test for the auth-scheme fix: MfaController used to be Cookie-only, which
    // would 401 every call made through the Blazor client's Bearer-token-only HttpClient.
    [Fact]
    public async Task PushDevicesCanBeListedWithABearerToken()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/account/mfa/push-devices");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TotpStatusReturnsTheExpectedShape()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/account/mfa/totp/status");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("isEnabled", out _));
        Assert.True(body.TryGetProperty("availableBackupCodes", out _));
    }

    [Fact]
    public async Task EnrollReturnsAProvisioningQrCodeDataUri()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/account/mfa/totp/enroll", new { });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("data:image/png;base64,", body.GetProperty("qrCodeDataUri").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("secretKey").GetString()));
    }

    [Fact]
    public async Task EnableWithAnInvalidCodeReturnsBadRequest()
    {
        using var client = fixture.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/account/mfa/totp/enroll", new { });

        var response = await client.PostAsJsonAsync("/api/v1/account/mfa/totp/enable", new { code = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EnableWithAValidCodeEnablesTotpAndReturnsBackupCodesOnce()
    {
        using var client = fixture.CreateAuthenticatedClient();
        var enrollResponse = await client.PostAsJsonAsync("/api/v1/account/mfa/totp/enroll", new { });
        var enrollBody = await enrollResponse.Content.ReadFromJsonAsync<JsonElement>();
        var secretKey = enrollBody.GetProperty("secretKey").GetString()!;
        var code = TotpGenerator.ComputeCode(secretKey, TotpGenerator.GetCurrentTimeStep());

        var enableResponse = await client.PostAsJsonAsync("/api/v1/account/mfa/totp/enable", new { code });
        enableResponse.EnsureSuccessStatusCode();

        var enableBody = await enableResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(10, enableBody.GetProperty("backupCodes").GetArrayLength());

        var statusResponse = await client.GetAsync("/api/v1/account/mfa/totp/status");
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(statusBody.GetProperty("isEnabled").GetBoolean());

        // Leaves TOTP disabled again for other tests sharing this fixture's account.
        await client.PostAsync("/api/v1/account/mfa/totp/disable", null);
    }

    [Fact]
    public async Task RegenerateBackupCodesBeforeEnablingReturnsBadRequest()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.PostAsync("/api/v1/account/mfa/totp/backup-codes/regenerate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
