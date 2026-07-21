using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Generation;
using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// D-N: posts <c>document.generated</c> to MngNotifier (email and/or Telegram). Failures are logged only.
/// </summary>
public sealed class DocumentNotificationOrchestrator : IDocumentNotificationOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MngDocumentSettings _settings;
    private readonly ILogger<DocumentNotificationOrchestrator> _logger;

    public DocumentNotificationOrchestrator(
        IHttpClientFactory httpClientFactory,
        IOptions<MngDocumentSettings> settings,
        ILogger<DocumentNotificationOrchestrator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task NotifyDocumentGeneratedAsync(
        GenerateDocumentResultDto result,
        IReadOnlyList<string>? extraRecipients = null,
        CancellationToken ct = default)
    {
        var cfg = _settings.Notifications ?? new DocumentNotificationsSettings();
        if (!cfg.Enabled)
        {
            _logger.LogDebug("Document notifications disabled; skip document.generated for {ResourceId}", result.ResourceId);
            return;
        }

        if (string.IsNullOrWhiteSpace(cfg.NotifierBaseUrl))
        {
            _logger.LogWarning("Notifications.NotifierBaseUrl empty; skip document.generated");
            return;
        }

        var channels = NormalizeChannels(cfg.Channels);
        if (channels.Count == 0)
        {
            _logger.LogDebug("No notification channels configured; skip document.generated");
            return;
        }

        var deepLink = BuildDeepLink(cfg, result.ResourceId);

        if (channels.Contains("email"))
            await SendMailAsync(cfg, result, extraRecipients, deepLink, ct);

        if (channels.Contains("telegram"))
            await SendTelegramAsync(cfg, result, deepLink, ct);
    }

    private async Task SendMailAsync(
        DocumentNotificationsSettings cfg,
        GenerateDocumentResultDto result,
        IReadOnlyList<string>? extraRecipients,
        string deepLink,
        CancellationToken ct)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in cfg.DefaultTo)
        {
            if (!string.IsNullOrWhiteSpace(t))
                recipients.Add(t.Trim());
        }
        if (extraRecipients != null)
        {
            foreach (var t in extraRecipients)
            {
                if (!string.IsNullOrWhiteSpace(t))
                    recipients.Add(t.Trim());
            }
        }

        if (recipients.Count == 0)
        {
            _logger.LogDebug("No mail recipients for document.generated ({ResourceId}); skip email", result.ResourceId);
            return;
        }

        try
        {
            var docLabel = string.IsNullOrWhiteSpace(result.DocNo) ? result.FileName : result.DocNo;
            var subject = $"[MonitraNG] Döküman üretildi: {docLabel}";
            var body = BuildHtmlBody(result, deepLink);

            var client = _httpClientFactory.CreateClient("MngNotifier");
            var version = string.IsNullOrWhiteSpace(cfg.NotifierApiVersion) ? "v1" : cfg.NotifierApiVersion.Trim();
            var baseUrl = cfg.NotifierBaseUrl.TrimEnd('/');
            var uri = $"{baseUrl}/api/{version}/notifications/mail";

            var payload = new
            {
                to = recipients.ToList(),
                subject,
                body,
                isHtml = true
            };

            using var response = await client.PostAsJsonAsync(uri, payload, JsonOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "document.generated mail failed HTTP {Status} for {ResourceId}: {Body}",
                    (int)response.StatusCode,
                    result.ResourceId,
                    err);
                return;
            }

            _logger.LogInformation(
                "document.generated mail sent for {ResourceId} to {Count} recipient(s)",
                result.ResourceId,
                recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "document.generated mail failed for {ResourceId}", result.ResourceId);
        }
    }

    private async Task SendTelegramAsync(
        DocumentNotificationsSettings cfg,
        GenerateDocumentResultDto result,
        string deepLink,
        CancellationToken ct)
    {
        try
        {
            var chatIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in cfg.DefaultTelegramChatIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    chatIds.Add(id.Trim());
            }

            foreach (var id in await ResolveTelegramChatIdsFromKeeperAsync(cfg, ct))
                chatIds.Add(id);

            // Empty to[] → Notifier uses Telegram:DefaultChatId when configured
            var client = _httpClientFactory.CreateClient("MngNotifier");
            var version = string.IsNullOrWhiteSpace(cfg.NotifierApiVersion) ? "v1" : cfg.NotifierApiVersion.Trim();
            var baseUrl = cfg.NotifierBaseUrl.TrimEnd('/');
            var uri = $"{baseUrl}/api/{version}/notifications/send-message";

            var text = BuildTelegramText(result, deepLink);
            var payload = new
            {
                channel = "telegram",
                to = chatIds.ToList(),
                text,
                disableWebPagePreview = true
            };

            using var response = await client.PostAsJsonAsync(uri, payload, JsonOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "document.generated telegram failed HTTP {Status} for {ResourceId}: {Body}",
                    (int)response.StatusCode,
                    result.ResourceId,
                    err);
                return;
            }

            _logger.LogInformation(
                "document.generated telegram sent for {ResourceId} (explicitChatIds={Count})",
                result.ResourceId,
                chatIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "document.generated telegram failed for {ResourceId}", result.ResourceId);
        }
    }

    private async Task<List<string>> ResolveTelegramChatIdsFromKeeperAsync(
        DocumentNotificationsSettings cfg,
        CancellationToken ct)
    {
        var userIds = (cfg.TelegramUserIds ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (userIds.Count == 0 || string.IsNullOrWhiteSpace(cfg.DomainId))
            return new List<string>();

        if (string.IsNullOrWhiteSpace(cfg.KeeperBaseUrl))
        {
            _logger.LogWarning("Notifications.KeeperBaseUrl empty; skip telegram user resolve");
            return new List<string>();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("MngKeeper");
            var uri = $"{cfg.KeeperBaseUrl.TrimEnd('/')}/api/internal/telegram-resolve-recipients";
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            if (!string.IsNullOrWhiteSpace(cfg.InternalNotifyApiKey))
                request.Headers.TryAddWithoutValidation("X-Monitra-Notify-Key", cfg.InternalNotifyApiKey);

            request.Content = JsonContent.Create(new
            {
                domainId = cfg.DomainId.Trim(),
                userIds
            }, options: JsonOptions);

            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("telegram-resolve-recipients HTTP {Status}: {Body}", (int)response.StatusCode, body);
                return new List<string>();
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var root = doc.RootElement;
            var chatProp = root.TryGetProperty("chatIds", out var c) ? c
                : root.TryGetProperty("ChatIds", out var c2) ? c2
                : default;

            var list = new List<string>();
            if (chatProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in chatProp.EnumerateArray())
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        list.Add(s.Trim());
                }
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keeper telegram recipient resolve failed");
            return new List<string>();
        }
    }

    private static HashSet<string> NormalizeChannels(IEnumerable<string>? channels)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (channels != null)
        {
            foreach (var c in channels)
            {
                if (string.IsNullOrWhiteSpace(c)) continue;
                set.Add(c.Trim().ToLowerInvariant());
            }
        }

        if (set.Count == 0)
            set.Add("email");

        return set;
    }

    private static string BuildDeepLink(DocumentNotificationsSettings cfg, string resourceId)
    {
        var ui = (cfg.UiBaseUrl ?? string.Empty).TrimEnd('/');
        var path = (cfg.DeepLinkPathTemplate ?? "/apps/document-intelligence/r/{id}")
            .Replace("{id}", resourceId, StringComparison.OrdinalIgnoreCase);
        if (!path.StartsWith('/'))
            path = "/" + path;
        return string.IsNullOrWhiteSpace(ui) ? path : ui + path;
    }

    private static string BuildTelegramText(GenerateDocumentResultDto result, string deepLink)
    {
        var docLabel = string.IsNullOrWhiteSpace(result.DocNo) ? result.FileName : result.DocNo;
        var sb = new StringBuilder();
        sb.AppendLine("MonitraNG — döküman üretildi");
        sb.AppendLine($"Belge: {docLabel}");
        if (!string.IsNullOrWhiteSpace(result.TemplateCode))
            sb.AppendLine($"Şablon: {result.TemplateCode}");
        if (!string.IsNullOrWhiteSpace(result.ProfileCode))
            sb.AppendLine($"Profil: {result.ProfileCode}");
        sb.AppendLine($"Zaman (UTC): {result.GeneratedAt:u}");
        if (!string.IsNullOrWhiteSpace(deepLink))
            sb.AppendLine(deepLink);
        return sb.ToString().Trim();
    }

    private static string BuildHtmlBody(GenerateDocumentResultDto result, string deepLink)
    {
        var sb = new StringBuilder();
        sb.Append("<p>Yeni bir döküman üretildi.</p><ul>");
        sb.Append(CultureInvariantLi("Dosya", result.FileName));
        sb.Append(CultureInvariantLi("Belge no", result.DocNo));
        sb.Append(CultureInvariantLi("Şablon", result.TemplateCode));
        sb.Append(CultureInvariantLi("Profil", result.ProfileCode));
        if (!string.IsNullOrWhiteSpace(result.ContextType) || !string.IsNullOrWhiteSpace(result.ContextId))
            sb.Append(CultureInvariantLi("Bağlam", $"{result.ContextType} / {result.ContextId}"));
        sb.Append(CultureInvariantLi("Zaman (UTC)", result.GeneratedAt.ToString("u")));
        sb.Append("</ul>");
        if (!string.IsNullOrWhiteSpace(deepLink))
            sb.Append($"<p><a href=\"{System.Net.WebUtility.HtmlEncode(deepLink)}\">Dökümanı aç</a></p>");
        return sb.ToString();
    }

    private static string CultureInvariantLi(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return $"<li><strong>{System.Net.WebUtility.HtmlEncode(label)}:</strong> {System.Net.WebUtility.HtmlEncode(value)}</li>";
    }
}
