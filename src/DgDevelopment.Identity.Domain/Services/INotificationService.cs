namespace DgDevelopment.Identity.Domain.Services;

public interface INotificationService
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default);
}
