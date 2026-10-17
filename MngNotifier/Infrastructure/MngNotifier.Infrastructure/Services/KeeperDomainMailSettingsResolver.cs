using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;
using MngNotifier.Application.Services;

namespace MngNotifier.Infrastructure.Services;

public sealed class KeeperDomainMailSettingsResolver : IDomainMailSettingsResolver
{
    public const string NotifyApiKeyHeaderName = "X-Monitra-Notify-Key";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MngNotifierSettings _settings;
    private readonly DomainMailSettingsCache _cache;
    private readonly ILogger<KeeperDomainMailSettingsResolver> _logger;

    public KeeperDomainMailSettingsResolver(
        IHttpClientFactory httpClientFactory,
        IOptions<MngNotifierSettings> settings,
        DomainMailSettingsCache cache,
        ILogger<KeeperDomainMailSettingsResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<MailSettings> ResolveAsync(string? domainName, CancellationToken cancellationToken = default)
    {
        var fallback = _settings.Mail ?? new MailSettings();
        var name = domainName?.Trim();
        if (string.IsNullOrEmpty(name))
            return fallback;

        if (_cache.TryGet(name, out var cached))
            return cached;

        var (resolved, cacheable) = await FetchAsync(name, fallback, cancellationToken);
        if (cacheable)
            _cache.Set(name, resolved, CacheTtl);
        return resolved;
    }

    private async Task<(MailSettings Settings, bool Cacheable)> FetchAsync(
        string domainName,
        MailSettings fallback,
        CancellationToken cancellationToken)
    {
        var baseUrl = (_settings.Telegram?.KeeperBaseUrl ?? "http://mngkeeper:5001").TrimEnd('/');
        var url = $"{baseUrl}/api/internal/domain-mail/{Uri.EscapeDataString(domainName)}";

        try
        {
            var client = _httpClientFactory.CreateClient("MngKeeper");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(_settings.InternalNotifyApiKey))
                request.Headers.TryAddWithoutValidation(NotifyApiKeyHeaderName, _settings.InternalNotifyApiKey);

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Domain {Domain} has no Keeper record; using fallback SMTP", domainName);
                return (fallback, true);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Domain mail lookup failed HTTP {Status} for {Domain}: {Body}",
                    (int)response.StatusCode,
                    domainName,
                    body);
                return (fallback, false);
            }

            var payload = await response.Content.ReadFromJsonAsync<DomainMailPayload>(JsonOptions, cancellationToken);
            if (payload is not { Configured: true } || string.IsNullOrWhiteSpace(payload.Host))
            {
                _logger.LogDebug("Domain {Domain} mail is not configured; using fallback SMTP", domainName);
                return (fallback, true);
            }

            _logger.LogInformation("Using domain SMTP for {Domain} host {Host}", domainName, payload.Host.Trim());
            return (Map(payload, fallback), true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Domain mail lookup failed for {Domain}; using fallback SMTP", domainName);
            return (fallback, false);
        }
    }

    private static MailSettings Map(DomainMailPayload payload, MailSettings fallback)
    {
        var fromEmail = string.IsNullOrWhiteSpace(payload.FromEmail)
            ? fallback.DefaultFrom?.Email ?? string.Empty
            : payload.FromEmail.Trim();
        var fromName = string.IsNullOrWhiteSpace(payload.FromName)
            ? fallback.DefaultFrom?.Name
            : payload.FromName.Trim();

        return new MailSettings
        {
            Provider = "SMTP",
            DefaultFrom = new DefaultFromSettings
            {
                Email = fromEmail,
                Name = fromName
            },
            Smtp = new SmtpSettings
            {
                Host = payload.Host.Trim(),
                Port = payload.Port > 0 ? payload.Port : 587,
                Username = payload.Username ?? string.Empty,
                Password = payload.Password ?? string.Empty,
                EnableSsl = payload.EnableSsl,
                SecureSocketMode = string.IsNullOrWhiteSpace(payload.SecureSocketMode)
                    ? "Auto"
                    : payload.SecureSocketMode.Trim()
            }
        };
    }

    private sealed class DomainMailPayload
    {
        public bool Configured { get; set; }
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool EnableSsl { get; set; }
        public string? SecureSocketMode { get; set; }
        public string? FromEmail { get; set; }
        public string? FromName { get; set; }
    }
}
