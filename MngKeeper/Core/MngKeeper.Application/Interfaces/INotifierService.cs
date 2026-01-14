namespace MngKeeper.Application.Interfaces;

/// <summary>
/// Service for sending notifications via MngNotifier API
/// </summary>
public interface INotifierService
{
    /// <summary>
    /// Sends an email notification
    /// </summary>
    /// <param name="to">Recipient email addresses</param>
    /// <param name="subject">Email subject</param>
    /// <param name="body">Email body (HTML supported)</param>
    /// <param name="isHtml">Whether the body is HTML (default: true)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendEmailAsync(
        List<string> to, 
        string subject, 
        string body, 
        bool isHtml = true,
        CancellationToken cancellationToken = default);
}
