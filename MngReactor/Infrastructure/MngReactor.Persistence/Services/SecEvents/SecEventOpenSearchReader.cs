using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngReactor.Application.Configuration;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

/// <summary>G2: query OpenSearch with the same contract as Mongo sec_events reads.</summary>
internal sealed class SecEventOpenSearchReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly SecEventsSettings _settings;

    public SecEventOpenSearchReader(
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        SecEventsSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _settings = settings;
    }

    public async Task<SecEventQueryResult> QueryAsync(
        string domain,
        SecEventQueryFilter filter,
        CancellationToken cancellationToken)
    {
        var skip = SecEventQueryFilterBuilder.NormalizeSkip(filter.Skip);
        var limit = SecEventQueryFilterBuilder.NormalizeLimit(filter.Limit);
        var body = new Dictionary<string, object?>
        {
            ["from"] = skip,
            ["size"] = limit,
            ["track_total_hits"] = true,
            ["sort"] = new object[]
            {
                new Dictionary<string, object> { ["@timestamp"] = new Dictionary<string, string> { ["order"] = "desc" } }
            },
            ["query"] = BuildBoolQuery(filter)
        };

        using var doc = await PostSearchAsync(domain, body, cancellationToken);
        if (doc is null)
            return new SecEventQueryResult { Items = Array.Empty<SecEventListItem>(), Total = 0 };

        var total = ReadTotal(doc);
        var items = new List<SecEventListItem>();
        if (doc.RootElement.TryGetProperty("hits", out var hits)
            && hits.TryGetProperty("hits", out var hitArr)
            && hitArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var hit in hitArr.EnumerateArray())
                items.Add(MapHit(hit, includeRaw: false));
        }

        return new SecEventQueryResult { Items = items, Total = total };
    }

    public async Task<SecEventListItem?> GetByIdAsync(
        string domain,
        string id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var body = new Dictionary<string, object?>
        {
            ["size"] = 1,
            ["query"] = new Dictionary<string, object>
            {
                ["ids"] = new Dictionary<string, object> { ["values"] = new[] { id.Trim() } }
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

        return MapHit(hitArr[0], includeRaw: true);
    }

    public async Task<SecEventDashboardSummary> GetDashboardSummaryAsync(
        string domain,
        int rangeHours,
        bool excludeUnknown,
        DateTime from,
        DateTime to,
        IReadOnlyList<DateTime> hourStarts,
        CancellationToken cancellationToken)
    {
        var filters = new List<object>
        {
            new Dictionary<string, object>
            {
                ["range"] = new Dictionary<string, object>
                {
                    ["ingestedAt"] = new Dictionary<string, object>
                    {
                        ["gte"] = from.ToUniversalTime().ToString("o"),
                        ["lte"] = to.ToUniversalTime().ToString("o")
                    }
                }
            }
        };

        var mustNot = new List<object>();
        if (excludeUnknown)
        {
            mustNot.Add(new Dictionary<string, object>
            {
                ["term"] = new Dictionary<string, object> { ["event.action"] = SecEventUnknownFilter.UnknownAction }
            });
        }

        var boolQuery = new Dictionary<string, object> { ["filter"] = filters };
        if (mustNot.Count > 0)
            boolQuery["must_not"] = mustNot;

        var body = new Dictionary<string, object?>
        {
            ["size"] = 0,
            ["track_total_hits"] = true,
            ["query"] = new Dictionary<string, object> { ["bool"] = boolQuery },
            ["aggs"] = new Dictionary<string, object>
            {
                ["by_action"] = new Dictionary<string, object>
                {
                    ["terms"] = new Dictionary<string, object>
                    {
                        ["field"] = "event.action",
                        ["size"] = 50
                    }
                },
                ["hourly"] = new Dictionary<string, object>
                {
                    ["date_histogram"] = new Dictionary<string, object>
                    {
                        ["field"] = "ingestedAt",
                        ["calendar_interval"] = "1h",
                        ["min_doc_count"] = 0,
                        ["extended_bounds"] = new Dictionary<string, object>
                        {
                            ["min"] = from.ToUniversalTime().ToString("o"),
                            ["max"] = to.ToUniversalTime().ToString("o")
                        }
                    }
                },
                ["new_flow"] = new Dictionary<string, object>
                {
                    ["filter"] = new Dictionary<string, object>
                    {
                        ["term"] = new Dictionary<string, object> { ["baseline.newFlowPair"] = true }
                    }
                }
            }
        };

        using var doc = await PostSearchAsync(domain, body, cancellationToken);
        if (doc is null)
        {
            return new SecEventDashboardSummary
            {
                Range = $"{rangeHours}h",
                From = from,
                To = to,
                EventsTotal = 0,
                ByAction = new Dictionary<string, long>(),
                Hourly = hourStarts.Select(h => new SecEventHourlyBucket { HourStart = h, Count = 0 }).ToList()
            };
        }

        var total = ReadTotal(doc);
        var byAction = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.TryGetProperty("aggregations", out var aggs))
        {
            if (aggs.TryGetProperty("by_action", out var byActionAgg)
                && byActionAgg.TryGetProperty("buckets", out var actionBuckets))
            {
                foreach (var b in actionBuckets.EnumerateArray())
                {
                    var key = b.GetProperty("key").GetString() ?? "unknown";
                    var count = b.GetProperty("doc_count").GetInt64();
                    byAction[key] = count;
                }
            }

            if (aggs.TryGetProperty("new_flow", out var newFlowAgg)
                && newFlowAgg.TryGetProperty("doc_count", out var nf))
            {
                byAction[SecEventFlowBaselineRules.NewFlowAction] = nf.GetInt64();
            }

            var hourlyMap = new Dictionary<DateTime, long>();
            if (aggs.TryGetProperty("hourly", out var hourlyAgg)
                && hourlyAgg.TryGetProperty("buckets", out var hourBuckets))
            {
                foreach (var b in hourBuckets.EnumerateArray())
                {
                    DateTime hourStart;
                    if (b.TryGetProperty("key_as_string", out var kas)
                        && DateTime.TryParse(kas.GetString(), CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out var parsed))
                        hourStart = parsed.ToUniversalTime();
                    else if (b.TryGetProperty("key", out var keyEl) && keyEl.ValueKind == JsonValueKind.Number)
                        hourStart = DateTimeOffset.FromUnixTimeMilliseconds(keyEl.GetInt64()).UtcDateTime;
                    else
                        continue;

                    hourStart = new DateTime(hourStart.Year, hourStart.Month, hourStart.Day, hourStart.Hour, 0, 0, DateTimeKind.Utc);
                    hourlyMap[hourStart] = b.GetProperty("doc_count").GetInt64();
                }
            }

            var hourly = hourStarts
                .Select(h => new SecEventHourlyBucket
                {
                    HourStart = h,
                    Count = hourlyMap.TryGetValue(h, out var c) ? c : 0
                })
                .ToList();

            return new SecEventDashboardSummary
            {
                Range = $"{rangeHours}h",
                From = from,
                To = to,
                EventsTotal = total,
                ByAction = byAction,
                Hourly = hourly
            };
        }

        return new SecEventDashboardSummary
        {
            Range = $"{rangeHours}h",
            From = from,
            To = to,
            EventsTotal = total,
            ByAction = byAction,
            Hourly = hourStarts.Select(h => new SecEventHourlyBucket { HourStart = h, Count = 0 }).ToList()
        };
    }

    private static Dictionary<string, object> BuildBoolQuery(SecEventQueryFilter filter)
    {
        var filters = new List<object>();
        var mustNot = new List<object>();
        var should = new List<object>();

        if (filter.From.HasValue || filter.To.HasValue)
        {
            var range = new Dictionary<string, object>();
            if (filter.From.HasValue)
            {
                var from = filter.From.Value.Kind == DateTimeKind.Utc
                    ? filter.From.Value
                    : filter.From.Value.ToUniversalTime();
                range["gte"] = from.ToString("o");
            }

            if (filter.To.HasValue)
            {
                var to = filter.To.Value.Kind == DateTimeKind.Utc
                    ? filter.To.Value
                    : filter.To.Value.ToUniversalTime();
                range["lte"] = to.ToString("o");
            }

            filters.Add(new Dictionary<string, object>
            {
                ["range"] = new Dictionary<string, object> { ["ingestedAt"] = range }
            });
        }

        if (!string.IsNullOrWhiteSpace(filter.SourceType))
        {
            filters.Add(new Dictionary<string, object>
            {
                ["term"] = new Dictionary<string, object> { ["source.type"] = filter.SourceType.Trim() }
            });
        }

        if (!string.IsNullOrWhiteSpace(filter.EventAction))
        {
            var action = filter.EventAction.Trim();
            if (string.Equals(action, SecEventFlowBaselineRules.NewFlowAction, StringComparison.OrdinalIgnoreCase))
            {
                filters.Add(new Dictionary<string, object>
                {
                    ["term"] = new Dictionary<string, object> { ["baseline.newFlowPair"] = true }
                });
            }
            else
            {
                filters.Add(new Dictionary<string, object>
                {
                    ["term"] = new Dictionary<string, object> { ["event.action"] = action }
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.SrcIp))
        {
            filters.Add(new Dictionary<string, object>
            {
                ["term"] = new Dictionary<string, object> { ["network.srcIp"] = filter.SrcIp.Trim() }
            });
        }

        if (!string.IsNullOrWhiteSpace(filter.ActorUser))
        {
            filters.Add(new Dictionary<string, object>
            {
                ["term"] = new Dictionary<string, object> { ["actor.user"] = filter.ActorUser.Trim() }
            });
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var q = filter.Search.Trim();
            should.Add(new Dictionary<string, object>
            {
                ["multi_match"] = new Dictionary<string, object>
                {
                    ["query"] = q,
                    ["fields"] = new[]
                    {
                        "rawPreview", "event.action", "actor.user", "network.srcIp", "network.dstIp", "source.host"
                    },
                    ["type"] = "best_fields"
                }
            });
        }

        if (filter.ExcludeUnknown)
        {
            mustNot.Add(new Dictionary<string, object>
            {
                ["term"] = new Dictionary<string, object> { ["event.action"] = SecEventUnknownFilter.UnknownAction }
            });
        }

        var boolQuery = new Dictionary<string, object>();
        if (filters.Count > 0)
            boolQuery["filter"] = filters;
        if (mustNot.Count > 0)
            boolQuery["must_not"] = mustNot;
        if (should.Count > 0)
        {
            boolQuery["should"] = should;
            boolQuery["minimum_should_match"] = 1;
        }

        if (boolQuery.Count == 0)
            return new Dictionary<string, object> { ["match_all"] = new Dictionary<string, object>() };

        return new Dictionary<string, object> { ["bool"] = boolQuery };
    }

    private async Task<JsonDocument?> PostSearchAsync(
        string domain,
        Dictionary<string, object?> body,
        CancellationToken cancellationToken)
    {
        var baseUrl = (_settings.OpenSearchUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("OpenSearch read enabled but OpenSearchUrl is empty");
            return null;
        }

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
                // Empty index pattern → 404; treat as empty
                if ((int)response.StatusCode == 404)
                    return null;

                _logger.LogWarning(
                    "OpenSearch search failed status={Status} domain={Domain} body={Body}",
                    (int)response.StatusCode,
                    domain,
                    raw.Length > 400 ? raw[..400] : raw);
                return null;
            }

            return JsonDocument.Parse(raw);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenSearch search exception domain={Domain}", domain);
            return null;
        }
    }

    private static long ReadTotal(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("hits", out var hits)
            || !hits.TryGetProperty("total", out var total))
            return 0;

        if (total.ValueKind == JsonValueKind.Number)
            return total.GetInt64();

        if (total.ValueKind == JsonValueKind.Object && total.TryGetProperty("value", out var value))
            return value.GetInt64();

        return 0;
    }

    private static SecEventListItem MapHit(JsonElement hit, bool includeRaw)
    {
        var id = hit.TryGetProperty("_id", out var idEl) ? idEl.GetString() ?? "" : "";
        var src = hit.TryGetProperty("_source", out var source) ? source : default;

        string? GetNested(string parent, string child)
        {
            if (src.ValueKind != JsonValueKind.Object
                || !src.TryGetProperty(parent, out var p)
                || p.ValueKind != JsonValueKind.Object
                || !p.TryGetProperty(child, out var c)
                || c.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;
            return c.ValueKind == JsonValueKind.String ? c.GetString() : c.ToString();
        }

        DateTime GetDate(string field)
        {
            if (src.ValueKind != JsonValueKind.Object
                || !src.TryGetProperty(field, out var v)
                || v.ValueKind != JsonValueKind.String)
                return DateTime.MinValue;
            return DateTime.TryParse(v.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToUniversalTime()
                : DateTime.MinValue;
        }

        var rawPreview = src.ValueKind == JsonValueKind.Object
            && src.TryGetProperty("rawPreview", out var rp)
            && rp.ValueKind == JsonValueKind.String
            ? rp.GetString()
            : null;

        string? raw = null;
        if (includeRaw)
        {
            if (src.ValueKind == JsonValueKind.Object
                && src.TryGetProperty("raw", out var rawEl)
                && rawEl.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(rawEl.GetString()))
                raw = rawEl.GetString();
            else
                raw = rawPreview;
        }

        var baseline = false;
        if (src.ValueKind == JsonValueKind.Object
            && src.TryGetProperty("baseline", out var bl)
            && bl.ValueKind == JsonValueKind.Object
            && bl.TryGetProperty("newFlowPair", out var nf)
            && (nf.ValueKind == JsonValueKind.True || nf.ValueKind == JsonValueKind.False))
            baseline = nf.GetBoolean();

        return new SecEventListItem
        {
            Id = id,
            Timestamp = GetDate("@timestamp"),
            IngestedAt = GetDate("ingestedAt"),
            SourceType = GetNested("source", "type"),
            SourceProduct = GetNested("source", "product"),
            SourceHost = GetNested("source", "host"),
            EventAction = GetNested("event", "action") ?? "unknown",
            EventOutcome = GetNested("event", "outcome"),
            EventCode = GetNested("event", "code"),
            ActorUser = GetNested("actor", "user"),
            NetworkSrcIp = GetNested("network", "srcIp"),
            NetworkDstIp = GetNested("network", "dstIp"),
            ParserId = GetNested("parser", "id"),
            RawPreview = rawPreview,
            Raw = raw,
            BaselineNewFlowPair = baseline,
            Fields = ReadFields(src)
        };
    }

    private static IReadOnlyDictionary<string, object?>? ReadFields(JsonElement src)
    {
        if (src.ValueKind != JsonValueKind.Object
            || !src.TryGetProperty("fields", out var fields)
            || fields.ValueKind != JsonValueKind.Object)
            return null;

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in fields.EnumerateObject())
            dict[prop.Name] = JsonElementToClr(prop.Value);
        return dict.Count == 0 ? null : dict;
    }

    private static object? JsonElementToClr(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToClr).ToList(),
        JsonValueKind.Object => el.EnumerateObject()
            .ToDictionary(p => p.Name, p => JsonElementToClr(p.Value), StringComparer.OrdinalIgnoreCase),
        _ => el.ToString()
    };
}
