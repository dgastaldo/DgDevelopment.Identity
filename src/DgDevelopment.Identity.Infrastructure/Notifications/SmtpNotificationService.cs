using System.Net;
using System.Net.Mail;
using DgDevelopment.Identity.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DgDevelopment.Identity.Infrastructure.Notifications;

public sealed partial class SmtpNotificationService : INotificationService
{
    private readonly string? _host;
    private readonly int _port;
    private readonly string? _from;
    private readonly string? _username;
    private readonly string? _password;
    private readonly bool _enableSsl;
    private readonly ILogger<SmtpNotificationService> _logger;

    public SmtpNotificationService(IConfiguration configuration, ILogger<SmtpNotificationService> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger;

        _host = configuration["Email:Host"];
        _port = int.TryParse(configuration["Email:Port"], out var port) ? port : 587;
        _from = configuration["Email:From"];
        _username = configuration["Email:Username"];
        _password = configuration["Email:Password"];
        _enableSsl = !bool.TryParse(configuration["Email:EnableSsl"], out var enableSslConfigured) || enableSslConfigured;
    }

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);

        // No SMTP host configured (the local dev/test default) - log instead of sending, so
        // registration/password-recovery flows work out of the box without a real mail server.
        if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(_from))
        {
            LogEmail(to, subject, body);
            return;
        }

        using var client = new SmtpClient(_host, _port) { EnableSsl = _enableSsl };
        if (!string.IsNullOrWhiteSpace(_username))
            client.Credentials = new NetworkCredential(_username, _password);

        using var message = new MailMessage(_from, to, subject, body);
        await client.SendMailAsync(message, ct).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email (no SMTP host configured) to {To} - {Subject}: {Body}")]
    private partial void LogEmail(string to, string subject, string body);
}
