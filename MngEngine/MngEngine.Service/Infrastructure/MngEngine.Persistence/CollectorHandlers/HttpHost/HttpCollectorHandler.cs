using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using MngEngine.Application.Collector.HttpHost;
using MngEngine.Application.Features.Ingest;

namespace MngEngine.Persistence.CollectorHandlers.HttpHost;

/// <summary>
/// HTTP/REST collector. GET baseUrl (ve varsa path), Basic/None auth, JSON yanıttan collectible code ile metrik çıkarır.
/// </summary>
public class HttpCollectorHandler : IRequestHandler<HttpCollectorRequest, HttpCollectorResponse>
{
    private const int TimeoutSeconds = 15;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<HttpCollectorResponse> Handle(HttpCollectorRequest request, CancellationToken cancellationToken)
    {
        var info = request.HttpConnectionInfo
            ?? throw new InvalidOperationException("HttpConnectionInfo eksik.");
        var assetId = request.Asset?.Asset_Id ?? "?";
        var baseUrl = (info.BaseUrl ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException($"Asset {assetId}: HTTP için BaseUrl gereklidir.");

        // baseUrl tam endpoint ise (path içeriyorsa) olduğu gibi kullan; sadece host:port ise /api/metrics ekle
        var url = Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) && parsed.AbsolutePath != "/" && !string.IsNullOrWhiteSpace(parsed.AbsolutePath.Trim('/'))
            ? baseUrl
            : baseUrl + "/api/metrics";

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

        if (string.Equals(info.AuthType, "basic", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(info.Username))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes($"{info.Username}:{info.Password ?? ""}");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
        }
        else if (string.Equals(info.AuthType, "bearer_token", StringComparison.OrdinalIgnoreCase))
        {
            // TODO: authConfigId ile token endpoint'ten token alınacak (mon_http_auth_configs)
            throw new NotSupportedException($"Asset {assetId}: Bearer token auth henüz desteklenmiyor (authConfigId={info.AuthConfigId}).");
        }

        var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var root = JsonNode.Parse(json) as JsonObject;
        if (root == null)
            throw new InvalidOperationException($"Asset {assetId}: HTTP yanıtı geçerli JSON object değil.");

        var collectibles = request.Asset?.CollectibleItems ?? [];
        var metrics = new List<IngestMetric>();

        foreach (var c in collectibles)
        {
            var code = (c?.Code ?? "").Trim();
            if (string.IsNullOrEmpty(code)) continue;

            var value = TryGetValueByPath(root, code);
            if (value != null)
                metrics.Add(new IngestMetric { CollectibleCode = code, Value = value, Unit = null });
        }

        if (metrics.Count == 0)
            metrics.Add(new IngestMetric { CollectibleCode = "heartbeat", Value = 1, Unit = null });

        return new HttpCollectorResponse { Metrics = metrics };
    }

    /// <summary>Önce doğrudan key, sonra noktalı path (örn. sensors.temperature) dener.</summary>
    private static object? TryGetValueByPath(JsonObject root, string code)
    {
        if (root[code] != null)
            return JsonValueToObject(root[code]!);

        var parts = code.Split('.');
        JsonNode? current = root;
        foreach (var part in parts)
        {
            if (current is not JsonObject obj) return null;
            current = obj[part];
        }
        return current != null ? JsonValueToObject(current) : null;
    }

    private static object JsonValueToObject(JsonNode node)
    {
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<string>(out var s)) return s;
            if (jv.TryGetValue<double>(out var d)) return d;
            if (jv.TryGetValue<long>(out var l)) return l;
            if (jv.TryGetValue<int>(out var i)) return i;
            if (jv.TryGetValue<bool>(out var b)) return b;
        }
        return node.ToString();
    }
}
