namespace DgDevelopment.Identity.Domain.Services;

public interface INotificationService
{
    Task SendEmailAsync(string recipient, string subject, string body, CancellationToken ct = default);
}
