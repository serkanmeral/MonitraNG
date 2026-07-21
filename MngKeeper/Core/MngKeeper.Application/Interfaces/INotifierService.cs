namespace MngKeeper.Application.Interfaces;

/// <summary>
/// Service for sending notifications via MngNotifier API
/// </summary>
public interface INotifierService
{
    /// <summary>
    /// Sends an email notification
    /// </summary>
    Task SendEmailAsync(
        List<string> to, 
        string subject, 
        string body, 
        bool isHtml = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Push channel message (Telegram). Optional templateKey requires bearerToken for DG render.
    /// </summary>
    Task SendMessageAsync(
        string channel,
        List<string> to,
        string? text = null,
        string? templateKey = null,
        object? context = null,
        string? bearerToken = null,
        string? parseMode = null,
        bool disableWebPagePreview = true,
        CancellationToken cancellationToken = default);
}
