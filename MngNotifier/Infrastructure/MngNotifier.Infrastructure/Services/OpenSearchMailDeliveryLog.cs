using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;
using MngNotifier.Application.DTOs;
using MngNotifier.Application.Services;

namespace MngNotifier.Infrastructure.Services;

public sealed class OpenSearchMailDeliveryLog : IMailDeliveryLog
{
    private static readonly Regex UnsafeIndexChars = new("[^a-z0-9-]+", RegexOptions.Compiled);
    private static readonly ConcurrentDictionary<string, byte> EnsuredIndexes = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> SubjectKeywordReady = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MngNotifierSettings _settings;
    private readonly ILogger<OpenSearchMailDeliveryLog> _logger;

    public OpenSearchMailDeliveryLog(
        IHttpClientFactory httpClientFactory,
        IOptions<MngNotifierSettings> settings,
        ILogger<OpenSearchMailDeliveryLog> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task RecordAsync(MailDeliveryLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(out var baseUrl))
            return;

        try
        {
            var domain = SanitizeDomain(entry.DomainName);
            var index = IndexName(domain);
            var client = _httpClientFactory.CreateClient("OpenSearch");
            await EnsureIndexAsync(client, baseUrl, index, cancellationToken);

            var document = new
            {
                sentAt = entry.SentAt.ToUniversalTime(),
                domain,
                subject = Truncate(entry.Subject, 500),
                recipients = NormalizeAddresses(entry.Recipients),
                cc = NormalizeAddresses(entry.Cc),
                success = entry.Success,
                error = string.IsNullOrWhiteSpace(entry.Error) ? null : Truncate(entry.Error, 500)
            };

            using var response = await client.PostAsJsonAsync($"{baseUrl}/{index}/_doc", document, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Mail log write failed HTTP {Status}: {Body}", (int)response.StatusCode, body);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Mail log write failed");
        }
    }

    public async Task<MailDeliveryLogPage> SearchAsync(string domainName, MailDeliveryLogQuery query, CancellationToken cancellationToken = default)
    {
        var page = new MailDeliveryLogPage();
        if (!IsEnabled(out var baseUrl))
            return page;

        var skip = Math.Max(0, query.Skip);
        var take = Math.Clamp(query.Take, 1, 100);
        var index = IndexName(SanitizeDomain(domainName));
        var client = _httpClientFactory.CreateClient("OpenSearch");
        await EnsureSubjectKeywordAsync(client, baseUrl, index, cancellationToken);

        var payload = new Dictionary<string, object>
        {
            ["from"] = skip,
            ["size"] = take,
            ["sort"] = new object[] { BuildSort(query) },
            ["query"] = BuildQuery(query)
        };

        using var response = await client.PostAsJsonAsync($"{baseUrl}/{index}/_search", payload, JsonOptions, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return page;

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Mail log search failed HTTP {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Mail log search failed ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = doc.RootElement;
        if (root.TryGetProperty("hits", out var hits))
        {
            if (hits.TryGetProperty("total", out var total))
            {
                if (total.ValueKind == JsonValueKind.Object && total.TryGetProperty("value", out var value))
                    page.Total = value.GetInt64();
                else if (total.ValueKind == JsonValueKind.Number)
                    page.Total = total.GetInt64();
            }

            if (hits.TryGetProperty("hits", out var rows) && rows.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in rows.EnumerateArray())
                {
                    if (!row.TryGetProperty("_source", out var source))
                        continue;
                    page.Items.Add(MapItem(source));
                }
            }
        }

        return page;
    }

    private async Task EnsureSubjectKeywordAsync(HttpClient client, string baseUrl, string index, CancellationToken cancellationToken)
    {
        if (SubjectKeywordReady.ContainsKey(index))
            return;

        try
        {
            using var mappingResponse = await client.GetAsync($"{baseUrl}/{index}/_mapping", cancellationToken);
            if (mappingResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                return;

            if (!mappingResponse.IsSuccessStatusCode)
                return;

            using var mappingDoc = JsonDocument.Parse(await mappingResponse.Content.ReadAsStringAsync(cancellationToken));
            if (HasSubjectKeyword(mappingDoc.RootElement))
            {
                SubjectKeywordReady.TryAdd(index, 0);
                return;
            }

            var update = new Dictionary<string, object>
            {
                ["properties"] = new Dictionary<string, object>
                {
                    ["subject"] = new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["fields"] = new Dictionary<string, object>
                        {
                            ["keyword"] = new Dictionary<string, object>
                            {
                                ["type"] = "keyword",
                                ["ignore_above"] = 512
                            }
                        }
                    }
                }
            };

            using var put = await client.PutAsJsonAsync($"{baseUrl}/{index}/_mapping", update, JsonOptions, cancellationToken);
            if (!put.IsSuccessStatusCode)
            {
                var body = await put.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Mail log subject keyword mapping failed HTTP {Status}: {Body}", (int)put.StatusCode, body);
                return;
            }

            using var refresh = await client.PostAsync(
                $"{baseUrl}/{index}/_update_by_query?conflicts=proceed&refresh=true",
                JsonContent.Create(new { query = new { match_all = new { } } }),
                cancellationToken);
            if (!refresh.IsSuccessStatusCode)
            {
                var body = await refresh.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Mail log subject keyword backfill failed HTTP {Status}: {Body}", (int)refresh.StatusCode, body);
            }

            SubjectKeywordReady.TryAdd(index, 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Mail log subject keyword setup failed for {Index}", index);
        }
    }

    private static bool HasSubjectKeyword(JsonElement mappingRoot)
    {
        foreach (var index in mappingRoot.EnumerateObject())
        {
            if (!index.Value.TryGetProperty("mappings", out var mappings))
                continue;
            if (!mappings.TryGetProperty("properties", out var properties))
                continue;
            if (!properties.TryGetProperty("subject", out var subject))
                continue;
            if (subject.TryGetProperty("fields", out var fields) && fields.TryGetProperty("keyword", out _))
                return true;
        }

        return false;
    }

    private static Dictionary<string, object> BuildSort(MailDeliveryLogQuery query)
    {
        var field = query.SortBy?.Trim().ToLowerInvariant() switch
        {
            "subject" => "subject.keyword",
            "recipients" => "recipients",
            "success" => "success",
            _ => "sentAt"
        };
        var unmapped = field switch
        {
            "sentAt" => "date",
            "success" => "boolean",
            _ => "keyword"
        };

        return new Dictionary<string, object>
        {
            [field] = new Dictionary<string, object>
            {
                ["order"] = query.SortDescending ? "desc" : "asc",
                ["missing"] = "_last",
                ["unmapped_type"] = unmapped
            }
        };
    }

    private static object BuildQuery(MailDeliveryLogQuery query)
    {
        var must = new List<object>();
        if (query.Success.HasValue)
        {
            must.Add(new Dictionary<string, object>
            {
                ["term"] = new Dictionary<string, object> { ["success"] = query.Success.Value }
            });
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            if (search.Length > 100)
                search = search[..100];
            var wildcard = "*" + EscapeWildcard(search) + "*";
            must.Add(new Dictionary<string, object>
            {
                ["bool"] = new Dictionary<string, object>
                {
                    ["should"] = new object[]
                    {
                        Wildcard("subject.keyword", wildcard),
                        Wildcard("recipients", wildcard),
                        Wildcard("cc", wildcard)
                    },
                    ["minimum_should_match"] = 1
                }
            });
        }

        if (must.Count == 0)
            return new Dictionary<string, object> { ["match_all"] = new Dictionary<string, object>() };

        return new Dictionary<string, object>
        {
            ["bool"] = new Dictionary<string, object> { ["must"] = must }
        };
    }

    private static Dictionary<string, object> Wildcard(string field, string value) =>
        new()
        {
            ["wildcard"] = new Dictionary<string, object>
            {
                [field] = new Dictionary<string, object>
                {
                    ["value"] = value,
                    ["case_insensitive"] = true
                }
            }
        };

    private static string EscapeWildcard(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch is '\\' or '*' or '?')
                builder.Append('\\');
            builder.Append(ch);
        }

        return builder.ToString();
    }

    private bool IsEnabled(out string baseUrl)
    {
        var os = _settings.OpenSearch ?? new OpenSearchSettings();
        baseUrl = (os.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        return os.Enabled && !string.IsNullOrWhiteSpace(baseUrl);
    }

    private async Task EnsureIndexAsync(HttpClient client, string baseUrl, string index, CancellationToken cancellationToken)
    {
        if (EnsuredIndexes.ContainsKey(index))
            return;

        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"{baseUrl}/{index}"), cancellationToken);
        if (head.IsSuccessStatusCode)
        {
            EnsuredIndexes.TryAdd(index, 0);
            return;
        }

        var mapping = new
        {
            settings = new { number_of_shards = 1, number_of_replicas = 0 },
            mappings = new
            {
                properties = new
                {
                    sentAt = new { type = "date" },
                    domain = new { type = "keyword" },
                    subject = new
                    {
                        type = "text",
                        fields = new { keyword = new { type = "keyword", ignore_above = 512 } }
                    },
                    recipients = new { type = "keyword" },
                    cc = new { type = "keyword" },
                    success = new { type = "boolean" },
                    error = new { type = "text" }
                }
            }
        };

        using var created = await client.PutAsJsonAsync($"{baseUrl}/{index}", mapping, JsonOptions, cancellationToken);
        if (created.IsSuccessStatusCode || (int)created.StatusCode == 400)
            EnsuredIndexes.TryAdd(index, 0);
        else
        {
            var body = await created.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Mail log index create failed HTTP {Status}: {Body}", (int)created.StatusCode, body);
        }
    }

    private static MailDeliveryLogItem MapItem(JsonElement source)
    {
        var sentAt = DateTime.UtcNow;
        if (source.TryGetProperty("sentAt", out var sent) && sent.ValueKind == JsonValueKind.String
            && DateTime.TryParse(sent.GetString(), out var parsed))
        {
            sentAt = parsed.ToUniversalTime();
        }

        return new MailDeliveryLogItem
        {
            SentAt = sentAt,
            Subject = source.TryGetProperty("subject", out var subject) ? subject.GetString() ?? string.Empty : string.Empty,
            Recipients = ReadStrings(source, "recipients"),
            Cc = ReadStrings(source, "cc"),
            Success = source.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True,
            Error = source.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                ? error.GetString()
                : null
        };
    }

    private static List<string> ReadStrings(JsonElement source, string name)
    {
        if (!source.TryGetProperty(name, out var value))
            return new List<string>();

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => item.Length > 0)
                .ToList();
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? new List<string>() : new List<string> { text };
        }

        return new List<string>();
    }

    private static List<string> NormalizeAddresses(IEnumerable<string>? addresses)
    {
        return (addresses ?? Array.Empty<string>())
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    internal static string SanitizeDomain(string? domain)
    {
        var safe = UnsafeIndexChars.Replace((domain ?? "unknown").Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrEmpty(safe) ? "unknown" : safe;
    }

    private static string IndexName(string domain) => $"mng-{domain}-mail-log";
}
