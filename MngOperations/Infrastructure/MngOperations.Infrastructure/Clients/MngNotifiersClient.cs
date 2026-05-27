using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Contracts.Notifications;
using MngOperations.Application.Interfaces;

namespace MngOperations.Infrastructure.Clients;

public class MngNotifiersClient : IMngNotifiersClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly MngNotifiersSettings _settings;
    private readonly ILogger<MngNotifiersClient> _logger;

    public MngNotifiersClient(
        IHttpClientFactory httpClientFactory,
        ILogger<MngNotifiersClient> logger,
        IOptions<MngOperationsSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value.MngNotifiers;
        _httpClient = httpClientFactory.CreateClient("MngNotifiers");

        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            var baseUrl = _settings.BaseUrl.TrimEnd('/');
            var version = string.IsNullOrWhiteSpace(_settings.ApiVersion) ? "v1" : _settings.ApiVersion.Trim();
            _httpClient.BaseAddress = new Uri($"{baseUrl}/api/{version}/");
        }
    }

    public async Task<SendMailResult> SendMailAsync(SendMailRequest request, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("MngNotifiers disabled; skipping mail to {Recipients}", string.Join(", ", request.To));
            return new SendMailResult { Success = false, ErrorMessage = "MngNotifiers disabled" };
        }

        if (_httpClient.BaseAddress == null)
        {
            _logger.LogWarning("MngNotifiers BaseUrl not configured");
            return new SendMailResult { Success = false, ErrorMessage = "MngNotifiers not configured" };
        }

        if (request.To.Count == 0)
            return new SendMailResult { Success = false, ErrorMessage = "No recipients" };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("notifications/mail", request, JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "MngNotifiers mail failed HTTP {Status}: {Body}",
                    (int)response.StatusCode,
                    body);

                return new SendMailResult
                {
                    Success = false,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}"
                };
            }

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
            string? notificationId = null;
            if (payload.ValueKind == JsonValueKind.Object
                && payload.TryGetProperty("notificationId", out var idProp)
                && idProp.ValueKind == JsonValueKind.String)
            {
                notificationId = idProp.GetString();
            }

            _logger.LogInformation(
                "MngNotifiers mail sent to {Recipients} subject={Subject}",
                string.Join(", ", request.To),
                request.Subject);

            return new SendMailResult { Success = true, NotificationId = notificationId };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MngNotifiers mail request failed");
            return new SendMailResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
