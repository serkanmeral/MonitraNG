using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Configuration;
using MngReactor.Application.Contracts.SecEvents;
using MngReactor.Application.Services.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

public sealed class SecEventWindowsParseSampleService : ISecEventWindowsParseSampleService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex Ipv4Regex = new(
        @"^(?:\d{1,3}\.){3}\d{1,3}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SecEventsSettings _settings;
    private readonly ILogger<SecEventWindowsParseSampleService> _logger;

    public SecEventWindowsParseSampleService(
        IHttpClientFactory httpClientFactory,
        IOptions<MngReactorSettings> options,
        ILogger<SecEventWindowsParseSampleService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value.SecEvents ?? new SecEventsSettings();
        _logger = logger;
    }

    public async Task<SecEventWindowsParseSampleResponse> GetSamplesAsync(
        string domain,
        SecEventWindowsParseSampleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = new SecEventWindowsParseSampleResponse();
        if (string.IsNullOrWhiteSpace(domain))
            return response;

        if (!_settings.OpenSearchReadEnabled)
        {
            _logger.LogWarning("Windows parse samples: OpenSearchReadEnabled=false");
            response.Notes.Add("OpenSearch read is disabled on Reactor.");
            return response;
        }

        var hours = Math.Clamp(request.Hours <= 0 ? 168 : request.Hours, 1, 720);
        var limit = Math.Clamp(request.Limit <= 0 ? 1 : request.Limit, 1, 20);
        var from = DateTime.UtcNow.AddHours(-hours);
        response.Hours = hours;

        var hostInput = request.Host?.Trim();
        string? resolvedHost = null;
        if (!string.IsNullOrWhiteSpace(hostInput) && LooksLikeIpv4(hostInput))
        {
            resolvedHost = await ResolveHostnameByIpAsync(domain, hostInput, hours, cancellationToken);
            if (!string.IsNullOrWhiteSpace(resolvedHost))
            {
                response.EffectiveHost = resolvedHost;
                response.Notes.Add($"Host IP '{hostInput}' resolved to hostname '{resolvedHost}'.");
            }
            else
            {
                response.Notes.Add(
                    $"Host IP '{hostInput}' could not be resolved to a hostname. Event docs usually store host.name (e.g. TERMINAL), not the IP.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(hostInput))
        {
            response.EffectiveHost = hostInput;
        }

        var filters = new List<object>
        {
            new Dictionary<string, object>
            {
                ["term"] = new Dictionary<string, object> { ["source.type"] = "windows-eventlog" }
            },
            new Dictionary<string, object>
            {
                ["range"] = new Dictionary<string, object>
                {
                    ["@timestamp"] = new Dictionary<string, object>
                    {
                        ["gte"] = from.ToString("o", CultureInfo.InvariantCulture)
                    }
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            filters.Add(new Dictionary<string, object>
            {
                ["bool"] = new Dictionary<string, object>
                {
                    ["should"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["term"] = new Dictionary<string, object>
                            {
                                ["fields.channel.keyword"] = request.Channel.Trim()
                            }
                        },
                        new Dictionary<string, object>
                        {
                            ["term"] = new Dictionary<string, object>
                            {
                                ["fields.channel"] = request.Channel.Trim()
                            }
                        }
                    },
                    ["minimum_should_match"] = 1
                }
            });
        }

        if (request.EventId is > 0)
        {
            filters.Add(new Dictionary<string, object>
            {
                ["bool"] = new Dictionary<string, object>
                {
                    ["should"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["term"] = new Dictionary<string, object> { ["fields.eventId"] = request.EventId.Value }
                        },
                        new Dictionary<string, object>
                        {
                            ["term"] = new Dictionary<string, object>
                            {
                                ["event.code"] = request.EventId.Value.ToString(CultureInfo.InvariantCulture)
                            }
                        }
                    },
                    ["minimum_should_match"] = 1
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(hostInput))
        {
            filters.Add(BuildHostFilter(hostInput, resolvedHost));
        }

        var body = new Dictionary<string, object?>
        {
            ["size"] = limit,
            ["track_total_hits"] = true,
            ["sort"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["@timestamp"] = new Dictionary<string, string> { ["order"] = "desc" }
                }
            },
            ["query"] = new Dictionary<string, object>
            {
                ["bool"] = new Dictionary<string, object> { ["filter"] = filters }
            },
            ["aggs"] = new Dictionary<string, object>
            {
                ["event_ids"] = new Dictionary<string, object>
                {
                    ["terms"] = new Dictionary<string, object>
                    {
                        ["field"] = "fields.eventId",
                        ["size"] = 50
                    }
                }
            }
        };

        using var doc = await PostSearchAsync(domain, body, cancellationToken);
        if (doc is null)
        {
            response.Notes.Add("OpenSearch sample query failed or index was not found.");
            return response;
        }

        if (doc.RootElement.TryGetProperty("aggregations", out var aggs)
            && aggs.TryGetProperty("event_ids", out var eidAgg)
            && eidAgg.TryGetProperty("buckets", out var buckets)
            && buckets.ValueKind == JsonValueKind.Array)
        {
            foreach (var b in buckets.EnumerateArray())
            {
                if (b.TryGetProperty("key", out var key))
                {
                    if (key.ValueKind == JsonValueKind.Number && key.TryGetInt32(out var n))
                        response.RecentEventIds.Add(n);
                    else if (int.TryParse(key.GetString(), out var ns))
                        response.RecentEventIds.Add(ns);
                }
            }
        }

        if (doc.RootElement.TryGetProperty("hits", out var hits))
        {
            response.TotalHits = ReadTotalHits(hits);
            if (hits.TryGetProperty("hits", out var hitArr) && hitArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var hit in hitArr.EnumerateArray())
                {
                    var sample = MapSample(hit);
                    if (sample is not null)
                        response.Items.Add(sample);
                }
            }
        }

        if (response.Items.Count == 0)
        {
            var channelPart = string.IsNullOrWhiteSpace(request.Channel) ? "any channel" : $"channel '{request.Channel.Trim()}'";
            var eventPart = request.EventId is > 0 ? $"Event ID {request.EventId.Value}" : "any Event ID";
            var hostPart = string.IsNullOrWhiteSpace(hostInput)
                ? "all hosts"
                : string.IsNullOrWhiteSpace(resolvedHost)
                    ? $"host '{hostInput}'"
                    : $"host '{hostInput}' → '{resolvedHost}'";
            response.Notes.Add(
                $"No Windows Event Log sample found for {eventPart}, {channelPart}, {hostPart} in the last {hours} hour(s) (hits={response.TotalHits}).");
            if (response.RecentEventIds.Count > 0)
            {
                response.Notes.Add(
                    $"Recent Event IDs in this filter window: {string.Join(", ", response.RecentEventIds)}.");
            }
            else if (!string.IsNullOrWhiteSpace(request.Channel) || !string.IsNullOrWhiteSpace(hostInput))
            {
                response.Notes.Add(
                    "No Event IDs were aggregated for this channel/host window. Try clearing the host filter or widening the lookback.");
            }
        }

        return response;
    }

    private async Task<string?> ResolveHostnameByIpAsync(
        string domain,
        string ip,
        int hours,
        CancellationToken cancellationToken)
    {
        var from = DateTime.UtcNow.AddHours(-Math.Clamp(hours, 1, 720));
        var body = new Dictionary<string, object?>
        {
            ["size"] = 1,
            ["sort"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["@timestamp"] = new Dictionary<string, string> { ["order"] = "desc" }
                }
            },
            ["_source"] = new[] { "host.name", "source.host", "fields.primaryIp", "fields.ipAddresses" },
            ["query"] = new Dictionary<string, object>
            {
                ["bool"] = new Dictionary<string, object>
                {
                    ["filter"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["range"] = new Dictionary<string, object>
                            {
                                ["@timestamp"] = new Dictionary<string, object>
                                {
                                    ["gte"] = from.ToString("o", CultureInfo.InvariantCulture)
                                }
                            }
                        }
                    },
                    ["should"] = BuildIpShouldClauses(ip),
                    ["minimum_should_match"] = 1
                }
            }
        };

        using var doc = await PostSearchAsync(domain, body, cancellationToken);
        if (doc is null)
            return null;

        if (!doc.RootElement.TryGetProperty("hits", out var hits)
            || !hits.TryGetProperty("hits", out var hitArr)
            || hitArr.ValueKind != JsonValueKind.Array
            || hitArr.GetArrayLength() == 0)
            return null;

        var hit = hitArr[0];
        if (!hit.TryGetProperty("_source", out var source) || source.ValueKind != JsonValueKind.Object)
            return null;

        if (source.TryGetProperty("host", out var hostObj)
            && hostObj.ValueKind == JsonValueKind.Object
            && hostObj.TryGetProperty("name", out var nameEl)
            && nameEl.ValueKind == JsonValueKind.String)
        {
            var name = nameEl.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        if (source.TryGetProperty("source", out var srcObj)
            && srcObj.ValueKind == JsonValueKind.Object
            && srcObj.TryGetProperty("host", out var srcHost)
            && srcHost.ValueKind == JsonValueKind.String)
        {
            var name = srcHost.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return null;
    }

    private static object BuildHostFilter(string hostInput, string? resolvedHost)
    {
        var should = new List<object>
        {
            Term("host.name", hostInput),
            Term("host.name.keyword", hostInput),
            Term("source.host.keyword", hostInput),
            Term("source.host", hostInput)
        };

        if (!string.IsNullOrWhiteSpace(resolvedHost)
            && !string.Equals(resolvedHost, hostInput, StringComparison.OrdinalIgnoreCase))
        {
            should.Add(Term("host.name", resolvedHost));
            should.Add(Term("host.name.keyword", resolvedHost));
            should.Add(Term("source.host.keyword", resolvedHost));
            should.Add(Term("source.host", resolvedHost));
        }

        if (LooksLikeIpv4(hostInput))
            should.AddRange(BuildIpShouldClauses(hostInput));

        return new Dictionary<string, object>
        {
            ["bool"] = new Dictionary<string, object>
            {
                ["should"] = should.ToArray(),
                ["minimum_should_match"] = 1
            }
        };
    }

    private static object[] BuildIpShouldClauses(string ip) =>
    [
        Term("fields.primaryIp.keyword", ip),
        Term("fields.primaryIp", ip),
        Term("host.ip", ip),
        Term("host.ip.keyword", ip),
        new Dictionary<string, object>
        {
            ["match_phrase"] = new Dictionary<string, object> { ["fields.ipAddresses"] = ip }
        },
        new Dictionary<string, object>
        {
            ["term"] = new Dictionary<string, object> { ["fields.ipAddresses.keyword"] = ip }
        }
    ];

    private static Dictionary<string, object> Term(string field, string value) =>
        new()
        {
            ["term"] = new Dictionary<string, object> { [field] = value }
        };

    private static bool LooksLikeIpv4(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && Ipv4Regex.IsMatch(value.Trim())
        && IPAddress.TryParse(value.Trim(), out var addr)
        && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

    private static long ReadTotalHits(JsonElement hits)
    {
        if (!hits.TryGetProperty("total", out var total))
            return 0;
        if (total.ValueKind == JsonValueKind.Number && total.TryGetInt64(out var n))
            return n;
        if (total.ValueKind == JsonValueKind.Object
            && total.TryGetProperty("value", out var value)
            && value.TryGetInt64(out var v))
            return v;
        return 0;
    }

    private static SecEventWindowsParseSampleDto? MapSample(JsonElement hit)
    {
        var id = hit.TryGetProperty("_id", out var idEl) ? idEl.GetString() ?? "" : "";
        if (!hit.TryGetProperty("_source", out var source) || source.ValueKind != JsonValueKind.Object)
            return null;

        var canonical = SecEventParseFieldResolver.CanonicalRawFromOpenSearchSource(source);
        using var canonicalDoc = JsonDocument.Parse(JsonSerializer.Serialize(canonical));
        var root = canonicalDoc.RootElement;

        var eventData = SecEventParseFieldResolver.DiscoverEventDataKeys(root);
        var message = SecEventParseFieldResolver.ReadMessage(root);
        var eventDataText = SecEventParseFieldResolver.ReadPath(root, "eventDataText");
        var channel = SecEventParseFieldResolver.ReadChannel(root);
        var eventId = SecEventParseFieldResolver.ReadEventId(root);

        string? GetNested(string parent, string child)
        {
            if (!source.TryGetProperty(parent, out var p) || p.ValueKind != JsonValueKind.Object)
                return null;
            if (!p.TryGetProperty(child, out var c))
                return null;
            return c.ValueKind == JsonValueKind.String ? c.GetString() : c.ToString();
        }

        var ts = DateTime.UtcNow;
        if (source.TryGetProperty("@timestamp", out var tsEl) && tsEl.ValueKind == JsonValueKind.String
            && DateTime.TryParse(tsEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            ts = parsed.ToUniversalTime();

        return new SecEventWindowsParseSampleDto
        {
            Id = id,
            Timestamp = ts,
            Host = GetNested("host", "name") ?? GetNested("source", "host"),
            Channel = channel,
            EventId = eventId,
            Provider = SecEventParseFieldResolver.ReadPath(root, "provider"),
            Package = SecEventParseFieldResolver.ReadPath(root, "package") ?? GetNested("source", "product"),
            Message = message,
            EventDataText = eventDataText,
            EventData = eventData,
            ParseModeHint = SecEventParseFieldResolver.InferParseModeHint(eventData),
            Raw = canonical,
            SourceType = GetNested("source", "type") ?? "windows-eventlog",
            SourceProduct = GetNested("source", "product")
        };
    }

    private async Task<JsonDocument?> PostSearchAsync(
        string domain,
        Dictionary<string, object?> body,
        CancellationToken cancellationToken)
    {
        var baseUrl = (_settings.OpenSearchUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        var index = SecEventOpenSearchIndexNames.BuildReadAliasPattern(domain);
        var client = _httpClientFactory.CreateClient("opensearch");
        try
        {
            using var response = await client.PostAsJsonAsync(
                $"{baseUrl}/{index}/_search",
                body,
                JsonOptions,
                cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 404)
                    return null;
                _logger.LogWarning(
                    "Parse sample search failed status={Status} body={Body}",
                    (int)response.StatusCode,
                    raw.Length > 300 ? raw[..300] : raw);
                return null;
            }

            return JsonDocument.Parse(raw);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parse sample search exception domain={Domain}", domain);
            return null;
        }
    }
}
