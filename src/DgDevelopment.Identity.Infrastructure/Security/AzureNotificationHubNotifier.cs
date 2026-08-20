using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DgDevelopment.Identity.Infrastructure.Security;

public sealed partial class AzureNotificationHubNotifier : IPushNotifier
{
    private const string FormatHeader = "ServiceBusNotification-Format";
    private const string DeviceHandleHeader = "ServiceBusNotification-DeviceHandle";

    private static readonly HttpClient HttpClient = new();

    private readonly Uri? _messagesUri;
    private readonly string? _sasKeyName;
    private readonly string? _sasKey;
    private readonly ILogger<AzureNotificationHubNotifier> _logger;

    public AzureNotificationHubNotifier(IConfiguration configuration, ILogger<AzureNotificationHubNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger;

        var connectionString = configuration["Azure:NotificationHub:ConnectionString"];
        var hubName = configuration["Azure:NotificationHub:Name"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(hubName))
            return;

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1], StringComparer.OrdinalIgnoreCase);

        if (!parts.TryGetValue("Endpoint", out var endpoint) || !parts.TryGetValue("SharedAccessKeyName", out var keyName) || !parts.TryGetValue("SharedAccessKey", out var key))
            return;

        _messagesUri = BuildMessagesUri(endpoint, hubName);
        _sasKeyName = keyName;
        _sasKey = key;
    }

    public async Task SendAsync(PushNotification notification, IReadOnlyCollection<PushDevice> devices, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(devices);
        if (_messagesUri is null || _sasKeyName is null || _sasKey is null)
            return;

        foreach (var device in devices)
        {
            if (!device.IsActive || !TryBuildPayload(device.Platform, notification, out var format, out var contentType, out var body))
                continue;

            ct.ThrowIfCancellationRequested();
            try
            {
                await SendAsync(device, format, contentType, body, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                LogSendFailed(device.Id, ex.Message);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
        }
    }

    private async Task SendAsync(PushDevice device, string format, string contentType, string body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _messagesUri);
        request.Headers.TryAddWithoutValidation("Authorization", BuildSasToken());
        request.Headers.TryAddWithoutValidation(FormatHeader, format);
        request.Headers.TryAddWithoutValidation(DeviceHandleHeader, device.PushToken);
        request.Content = new StringContent(body, Encoding.UTF8, contentType);

        var response = await HttpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            LogSendStatus(device.Id, (int)response.StatusCode);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "ANH send to device {DeviceId} failed: {Message}")]
    private partial void LogSendFailed(Guid deviceId, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ANH send to device {DeviceId} returned {StatusCode}.")]
    private partial void LogSendStatus(Guid deviceId, int statusCode);

    private string BuildSasToken(int expiresInSeconds = 3600)
    {
        var resourceUri = _messagesUri?.GetLeftPart(System.UriPartial.Path).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(resourceUri))
            throw new InvalidOperationException("ANH message endpoint is not configured.");

        var expiry = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds).ToUnixTimeSeconds();
        var stringToSign = $"{WebUtility.UrlEncode(resourceUri)}\n{expiry}";
        var signature = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_sasKey!), Encoding.UTF8.GetBytes(stringToSign)));

        return $"SharedAccessSignature sr={WebUtility.UrlEncode(resourceUri)}&sig={WebUtility.UrlEncode(signature)}&se={expiry}&skn={_sasKeyName}";
    }

    private static Uri BuildMessagesUri(string endpoint, string hubName)
    {
        var host = new Uri(endpoint.TrimEnd('/') + "/").Host;
        return new Uri($"https://{host}/{Uri.EscapeDataString(hubName)}/messages");
    }

    private static bool TryBuildPayload(PushPlatform platform, PushNotification notification, out string format, out string contentType, out string body)
    {
        var data = new
        {
            kind = notification.Kind == PushNotificationKind.ChallengeNew ? "challenge" : "resolved",
            challengeId = notification.ChallengeId,
            action = notification.ActionToken,
        };

        switch (platform)
        {
            case PushPlatform.Android:
                format = "fcm";
                contentType = "application/json;charset=utf-8";
                body = JsonSerializer.Serialize(new
                {
                    message = new
                    {
                        notification = new { title = notification.Title, body = notification.Body },
                        data,
                    },
                });
                return true;
            case PushPlatform.Apple:
                format = "apple";
                contentType = "application/json;charset=utf-8";
                body = JsonSerializer.Serialize(new
                {
                    aps = new { alert = new { title = notification.Title, body = notification.Body } },
                    data,
                });
                return true;
            case PushPlatform.Windows:
                format = "windows";
                contentType = "application/xml";
                body = $"<toast><visual><binding template=\"ToastGeneric\"><text>{System.Net.WebUtility.HtmlEncode(notification.Title)}</text><text>{System.Net.WebUtility.HtmlEncode(notification.Body)}</text></binding></visual></toast>";
                return true;
            default:
                format = string.Empty;
                contentType = string.Empty;
                body = string.Empty;
                return false;
        }
    }
}