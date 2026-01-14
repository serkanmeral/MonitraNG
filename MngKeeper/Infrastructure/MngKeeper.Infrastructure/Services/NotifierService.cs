using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngKeeper.Application.Configuration;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Infrastructure.Services;

/// <summary>
/// Service for sending notifications via MngNotifier API
/// </summary>
public class NotifierService : INotifierService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotifierService> _logger;
    private readonly NotifierSettings _settings;

    public NotifierService(
        HttpClient httpClient,
        ILogger<NotifierService> logger,
        IOptions<MngKeeperSettings> keeperSettings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = keeperSettings?.Value?.Notifier 
            ?? throw new ArgumentNullException(nameof(keeperSettings), "Notifier settings not configured");
        
        // Configure HttpClient base address
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task SendEmailAsync(
        List<string> to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        if (to == null || to.Count == 0)
        {
            throw new ArgumentException("At least one recipient is required", nameof(to));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Subject cannot be null or empty", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Body cannot be null or empty", nameof(body));
        }

        try
        {
            var request = new
            {
                to = to,
                subject = subject,
                body = body,
                isHtml = isHtml
            };

            var endpoint = $"/api/{_settings.ApiVersion}/notifications/mail";
            
            _logger.LogDebug("Sending email notification to MngNotifier. Endpoint: {Endpoint}, To: {To}, Subject: {Subject}",
                endpoint, string.Join(", ", to), subject);

            var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send email notification. StatusCode: {StatusCode}, Error: {Error}",
                    response.StatusCode, errorContent);
                
                throw new HttpRequestException(
                    $"Failed to send email notification. StatusCode: {response.StatusCode}, Error: {errorContent}");
            }

            _logger.LogInformation("Email notification sent successfully. To: {To}, Subject: {Subject}",
                string.Join(", ", to), subject);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email notification. To: {To}, Subject: {Subject}",
                string.Join(", ", to), subject);
            throw;
        }
    }
}
