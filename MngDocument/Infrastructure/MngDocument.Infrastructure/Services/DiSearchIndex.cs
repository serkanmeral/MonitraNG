using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MngDocument.Infrastructure.Services;

internal static class DiSearchIndex
{
    /// <summary>
    /// İ, I, ı ve i aynı yazıma iner. şğıöüç kendi sade harfine iner. Ardından küçük harf uygulanır.
    /// </summary>
    private const string TrAnalysis = """
    {
      "analysis": {
        "char_filter": {
          "tr_fold": {
            "type": "mapping",
            "mappings": [
              "İ => i",
              "I => i",
              "ı => i",
              "Ş => s",
              "ş => s",
              "Ğ => g",
              "ğ => g",
              "Ü => u",
              "ü => u",
              "Ö => o",
              "ö => o",
              "Ç => c",
              "ç => c"
            ]
          }
        },
        "analyzer": {
          "tr_text": {
            "type": "custom",
            "tokenizer": "standard",
            "char_filter": ["tr_fold"],
            "filter": ["lowercase"]
          }
        }
      }
    }
    """;

    public static string Name(string? domain) => $"mng-{Slug(domain)}-di-docs";

    /// <summary>İndeksi açar. Türkçe katlamayı ve eski kayıtlara yeniden çözümlemeyi kurar.</summary>
    public static async Task EnsureAsync(HttpClient client, string baseUrl, string index, CancellationToken ct)
    {
        var createdNew = false;
        using (var create = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/{index}"))
        {
            create.Content = new StringContent(CreateBody, Encoding.UTF8, "application/json");
            using var created = await client.SendAsync(create, ct);
            if (created.IsSuccessStatusCode)
                createdNew = true;
            else if ((int)created.StatusCode != 400)
            {
                var body = await created.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenSearch index create failed: {(int)created.StatusCode} {body}");
            }
        }

        if (createdNew)
            return;

        await EnsureAnalysisAsync(client, baseUrl, index, ct);

        var needsReindex = !await HasTrFieldAsync(client, baseUrl, index, ct);
        using (var map = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/{index}/_mapping"))
        {
            map.Content = new StringContent(MappingBody, Encoding.UTF8, "application/json");
            using var mapped = await client.SendAsync(map, ct);
            if (!mapped.IsSuccessStatusCode)
            {
                var body = await mapped.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenSearch mapping update failed: {(int)mapped.StatusCode} {body}");
            }
        }

        if (!needsReindex)
            return;

        using var reindex = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl}/{index}/_update_by_query?conflicts=proceed&refresh=true");
        reindex.Content = new StringContent("{\"query\":{\"match_all\":{}}}", Encoding.UTF8, "application/json");
        using var reindexed = await client.SendAsync(reindex, ct);
        if (!reindexed.IsSuccessStatusCode)
        {
            var body = await reindexed.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"OpenSearch reindex failed: {(int)reindexed.StatusCode} {body}");
        }
    }

    private static async Task EnsureAnalysisAsync(HttpClient client, string baseUrl, string index, CancellationToken ct)
    {
        using var current = await client.GetAsync($"{baseUrl}/{index}/_settings", ct);
        current.EnsureSuccessStatusCode();
        using var settings = await JsonDocument.ParseAsync(await current.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (settings.RootElement.TryGetProperty(index, out var root)
            && root.TryGetProperty("settings", out var indexSettings)
            && indexSettings.TryGetProperty("index", out var indexNode)
            && indexNode.TryGetProperty("analysis", out var analysis)
            && analysis.TryGetProperty("analyzer", out var analyzers)
            && analyzers.TryGetProperty("tr_text", out _))
        {
            return;
        }

        using (var close = await client.PostAsync($"{baseUrl}/{index}/_close", null, ct))
        {
            if (!close.IsSuccessStatusCode)
            {
                var body = await close.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenSearch close failed: {(int)close.StatusCode} {body}");
            }
        }

        try
        {
            using var put = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/{index}/_settings")
            {
                Content = new StringContent(TrAnalysis, Encoding.UTF8, "application/json")
            };
            using var updated = await client.SendAsync(put, ct);
            if (!updated.IsSuccessStatusCode)
            {
                var body = await updated.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenSearch analysis update failed: {(int)updated.StatusCode} {body}");
            }
        }
        finally
        {
            using var open = await client.PostAsync($"{baseUrl}/{index}/_open", null, ct);
            if (!open.IsSuccessStatusCode)
            {
                var body = await open.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenSearch open failed: {(int)open.StatusCode} {body}");
            }
        }
    }

    private static async Task<bool> HasTrFieldAsync(HttpClient client, string baseUrl, string index, CancellationToken ct)
    {
        using var response = await client.GetAsync($"{baseUrl}/{index}/_mapping", ct);
        if (!response.IsSuccessStatusCode)
            return false;
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.TryGetProperty(index, out var root)
            && root.TryGetProperty("mappings", out var mappings)
            && mappings.TryGetProperty("properties", out var properties)
            && properties.TryGetProperty("text", out var text)
            && text.TryGetProperty("fields", out var fields)
            && fields.TryGetProperty("tr", out _);
    }

    private static string CreateBody =>
        "{\"settings\":" + TrAnalysis + ",\"mappings\":{\"properties\":{"
        + TextField("name") + ","
        + TextField("title") + ","
        + TextField("text") + ","
        + "\"resourceId\":{\"type\":\"keyword\"},"
        + "\"indexedAt\":{\"type\":\"date\"},"
        + "\"extension\":{\"type\":\"keyword\"},"
        + "\"ancestorIds\":{\"type\":\"keyword\"},"
        + "\"updatedAt\":{\"type\":\"date\"}"
        + "}}}";

    private static string MappingBody =>
        "{\"properties\":{"
        + TextField("name") + ","
        + TextField("title") + ","
        + TextField("text") + ","
        + "\"extension\":{\"type\":\"keyword\"},"
        + "\"ancestorIds\":{\"type\":\"keyword\"},"
        + "\"updatedAt\":{\"type\":\"date\"}"
        + "}}";

    private static string TextField(string name) =>
        "\"" + name + "\":{\"type\":\"text\",\"fields\":{\"tr\":{\"type\":\"text\",\"analyzer\":\"tr_text\"}}}";

    public static string Slug(string? value)
    {
        var slug = Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "default" : slug;
    }
}
