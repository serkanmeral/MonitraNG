using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MngDataGateway.Application.Services;

namespace MngDataGateway.Persistence.Services;

/// <summary>
/// MngNotifier <c>POST /api/v1/notifications/chat-mention</c> çağrısı (yapılandırma yoksa no-op).
/// </summary>
public class ChatMentionNotifier : IChatMentionNotifier
{
    /// <summary>MngNotifier.Api <c>NotificationController.NotifyApiKeyHeaderName</c> ile aynı kalmalı.</summary>
    private const string NotifyApiKeyHeaderName = "X-Monitra-Notify-Key";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatMentionNotifier> _logger;

    public ChatMentionNotifier(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ChatMentionNotifier> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task NotifyChatMentionsAsync(
        string domainName,
        Dictionary<string, object> createdMessageRow,
        string authorFromToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["MngDataGatewaySettings:Actors:MngNotifier"]
                      ?? _configuration["Actors:MngNotifier"]
                      ?? Environment.GetEnvironmentVariable("MngDataGatewaySettings__Actors__MngNotifier");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogDebug("MngNotifier URL yapılandırılmadı; chat mention bildirimi atlandı.");
            return;
        }

        var author = ChtMentionTargetExtractor.CoercePersonId(createdMessageRow.GetValueOrDefault("authorPersonId"))
                     ?? authorFromToken;
        var targets = ChtMentionTargetExtractor.Collect(createdMessageRow, author);
        if (targets.Count == 0)
            return;

        var dataId = ChtMentionTargetExtractor.CoercePersonId(createdMessageRow.GetValueOrDefault("__dataId")) ?? string.Empty;
        var bodyPreview = ChtMentionTargetExtractor.BodyPreview(createdMessageRow.GetValueOrDefault("body"));

        var url = $"{baseUrl.TrimEnd('/')}/api/v1/notifications/chat-mention";
        var payload = new
        {
            domainName,
            dataId,
            targetPersonIds = targets,
            actorPersonId = author,
            bodyPreview,
            source = "cht_messages"
        };

        var apiKey = _configuration["MngDataGatewaySettings:Actors:MngNotifierNotifyApiKey"]
                     ?? _configuration["Actors:MngNotifierNotifyApiKey"];

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };
            if (!string.IsNullOrWhiteSpace(apiKey))
                request.Headers.TryAddWithoutValidation(NotifyApiKeyHeaderName, apiKey);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "MngNotifier chat-mention HTTP {Status}: {Body}",
                    (int)response.StatusCode,
                    err.Length > 500 ? err.Substring(0, 500) : err);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MngNotifier chat-mention çağrısı başarısız: {Url}", url);
        }
    }
}

/// <summary>
/// <c>mentions</c> alanı ve gövdedeki <c>@[id]</c> token'larından hedef id listesi.
/// </summary>
internal static class ChtMentionTargetExtractor
{
    private static readonly System.Text.RegularExpressions.Regex BodyMentionRegex = new(
        @"@\[([^\]\s]+)\]",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public static IReadOnlyList<string> Collect(Dictionary<string, object> row, string authorPersonId)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFromMentionsField(row.GetValueOrDefault("mentions"), set);
        var body = CoerceString(row.GetValueOrDefault("body"));
        if (!string.IsNullOrEmpty(body))
        {
            foreach (System.Text.RegularExpressions.Match m in BodyMentionRegex.Matches(body))
            {
                if (m.Groups.Count > 1)
                {
                    var id = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(id))
                        set.Add(id);
                }
            }
        }

        set.RemoveWhere(string.IsNullOrWhiteSpace);
        if (!string.IsNullOrWhiteSpace(authorPersonId))
            set.Remove(authorPersonId.Trim());

        return set.ToList();
    }

    public static string? CoercePersonId(object? value) => CoerceString(value);

    public static string? BodyPreview(object? value)
    {
        var s = CoerceString(value);
        if (string.IsNullOrEmpty(s))
            return null;
        return s.Length > 200 ? s.Substring(0, 197) + "…" : s;
    }

    private static void AddFromMentionsField(object? mentions, HashSet<string> set)
    {
        if (mentions == null)
            return;

        if (mentions is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in je.EnumerateArray())
                AddOneMentionElement(el, set);
            return;
        }

        if (mentions is IEnumerable enumerable && mentions is not string)
        {
            foreach (var item in enumerable)
                AddOneMentionElement(item, set);
        }
    }

    private static void AddOneMentionElement(object? item, HashSet<string> set)
    {
        if (item == null)
            return;
        if (item is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                var s = je.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    set.Add(s.Trim());
                return;
            }

            if (je.ValueKind == JsonValueKind.Object &&
                je.TryGetProperty("personId", out var pid) &&
                pid.ValueKind == JsonValueKind.String)
            {
                var s = pid.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    set.Add(s.Trim());
            }

            return;
        }

        if (item is string str && !string.IsNullOrWhiteSpace(str))
        {
            set.Add(str.Trim());
            return;
        }

        if (item is Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("personId", out var pidObj))
            {
                var s = CoerceString(pidObj);
                if (!string.IsNullOrWhiteSpace(s))
                    set.Add(s);
            }
        }
    }

    private static string? CoerceString(object? v)
    {
        if (v == null)
            return null;
        if (v is string s)
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        if (v is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString()?.Trim(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => je.ToString().Trim()
            };
        }

        return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)?.Trim();
    }
}
