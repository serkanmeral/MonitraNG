using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// İndeksli belgelerde ad ve içerik arar. OpenSearch ilk 200 adayı verir.
/// Klasör kapsamı, tür ve görüntüleme izni kayıt yüklendikten sonra elenir.
/// </summary>
public sealed class DocumentContentSearchService : IDocumentContentSearchService
{
    private const int CandidateWindow = 200;
    private const string MarkOn = "[[m]]";
    private const string MarkOff = "[[/m]]";

    private static readonly Regex QueryTokens = new("\"([^\"]+)\"|(\\S+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IResourceService _resources;
    private readonly IRequestContext _ctx;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MngDocumentSettings _settings;
    private readonly ILogger<DocumentContentSearchService> _logger;

    public DocumentContentSearchService(
        IResourceService resources,
        IRequestContext ctx,
        IHttpClientFactory httpClientFactory,
        IOptions<MngDocumentSettings> settings,
        ILogger<DocumentContentSearchService> logger)
    {
        _resources = resources;
        _ctx = ctx;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ContentSearchResultDto> SearchAsync(
        string query,
        string scope,
        string? scopeId,
        string? extension,
        string match,
        string sort,
        CancellationToken ct = default)
    {
        var text = query?.Trim() ?? string.Empty;
        if (text.Length < 3)
            return Empty();

        var scopeName = NormalizeScope(scope);
        var scopeKey = scopeId?.Trim();
        if (scopeName != "all" && string.IsNullOrWhiteSpace(scopeKey))
            return Empty();

        var matchName = NormalizeMatch(match);
        var sortName = string.Equals(sort, "updated", StringComparison.OrdinalIgnoreCase) ? "updated" : "relevance";
        var extensionName = NormalizeExtension(extension);
        var (phrases, terms) = SplitQuery(text);
        if (phrases.Count == 0 && terms.Count == 0)
            return Empty();

        var baseUrl = (_settings.OpenSearch.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return Empty();

        var index = DiSearchIndex.Name(string.IsNullOrWhiteSpace(_ctx.DomainName) ? "default" : _ctx.DomainName.Trim());
        var body = BuildQuery(terms, phrases, matchName);
        var client = _httpClientFactory.CreateClient("OpenSearch");
        await DiSearchIndex.EnsureAsync(client, baseUrl, index, ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{index}/_search")
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return Empty();
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Content search failed: {Status} {Body}", (int)response.StatusCode, errorBody);
            throw DocumentException.ServiceUnavailable(
                "SEARCH_FAILED",
                "Indexed search failed.",
                "İndeks araması tamamlanamadı.");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var hits = ReadHits(json);
        var capped = hits.Count >= CandidateWindow;
        if (hits.Count == 0)
            return new ContentSearchResultDto { Capped = capped };

        var visible = await _resources.LoadVisibleIndexedAsync(hits.Select(hit => hit.Id).ToList(), ct);
        var byId = visible.ToDictionary(row => row.Resource.Id, row => row, StringComparer.OrdinalIgnoreCase);
        var rows = new List<ContentSearchHitDto>();
        foreach (var hit in hits)
        {
            if (!byId.TryGetValue(hit.Id, out var context))
                continue;
            if (!InScope(context.Resource, scopeName, scopeKey))
                continue;
            if (extensionName.Length > 0 && !string.Equals(NormalizeExtension(context.Resource.Extension), extensionName, StringComparison.OrdinalIgnoreCase))
                continue;

            rows.Add(new ContentSearchHitDto
            {
                Resource = context.Resource,
                Match = hit.Match,
                Snippets = hit.Snippets,
                Folders = context.Folders,
                Score = hit.Score
            });
        }

        if (sortName == "updated")
        {
            rows = rows
                .OrderByDescending(row => row.Resource.UpdatedAt ?? row.Resource.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(row => row.Score)
                .ToList();
        }

        return new ContentSearchResultDto
        {
            Items = rows,
            Total = rows.Count,
            Capped = capped
        };
    }

    private static ContentSearchResultDto Empty() =>
        new() { Items = Array.Empty<ContentSearchHitDto>(), Total = 0, Capped = false };

    private static string NormalizeScope(string? scope)
    {
        if (string.Equals(scope, "folder", StringComparison.OrdinalIgnoreCase))
            return "folder";
        if (string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
            return "all";
        return "library";
    }

    private static string NormalizeMatch(string? match)
    {
        if (string.Equals(match, "name", StringComparison.OrdinalIgnoreCase))
            return "name";
        if (string.Equals(match, "content", StringComparison.OrdinalIgnoreCase))
            return "content";
        return "all";
    }

    private static string NormalizeExtension(string? extension)
    {
        var value = (extension ?? string.Empty).Trim().TrimStart('.');
        return value.ToLowerInvariant();
    }

    private static bool InScope(ResourceDto resource, string scope, string? scopeId)
    {
        if (scope == "all")
            return true;
        if (string.IsNullOrWhiteSpace(scopeId))
            return false;
        if (string.Equals(resource.ParentId, scopeId, StringComparison.OrdinalIgnoreCase))
            return true;
        return resource.AncestorIds.Any(id => string.Equals(id, scopeId, StringComparison.OrdinalIgnoreCase));
    }

    private static (List<string> Phrases, List<string> Terms) SplitQuery(string query)
    {
        var phrases = new List<string>();
        var terms = new List<string>();
        foreach (Match token in QueryTokens.Matches(query))
        {
            if (token.Groups[1].Success)
            {
                var phrase = token.Groups[1].Value.Trim();
                if (phrase.Length > 0)
                    phrases.Add(phrase);
            }
            else
            {
                var term = token.Groups[2].Value.Trim();
                if (term.Length > 0)
                    terms.Add(term);
            }
        }

        return (phrases, terms);
    }

    private const int PrefixMinLength = 4;

    private static JsonObject TermClause(string term, IReadOnlyList<string> fields, IReadOnlyList<string> phraseFields)
    {
        var folded = FoldForPrefix(term);
        if (folded.Length < PrefixMinLength)
        {
            var fieldNodes = new JsonArray();
            foreach (var field in fields)
                fieldNodes.Add(field);
            return new JsonObject
            {
                ["multi_match"] = new JsonObject
                {
                    ["query"] = term,
                    ["type"] = "best_fields",
                    ["operator"] = "and",
                    ["fields"] = fieldNodes
                }
            };
        }

        var should = new JsonArray();
        foreach (var field in phraseFields)
        {
            var prefix = new JsonObject { ["value"] = folded };
            if (field.StartsWith("name.", StringComparison.Ordinal) || field.StartsWith("title.", StringComparison.Ordinal))
                prefix["boost"] = 4;
            should.Add(new JsonObject
            {
                ["prefix"] = new JsonObject { [field] = prefix }
            });
        }

        return new JsonObject
        {
            ["bool"] = new JsonObject
            {
                ["should"] = should,
                ["minimum_should_match"] = 1
            }
        };
    }

    /// <summary>İndeks çözümleyicisiyle aynı katlama: Türkçe harfler, sonra küçük harf.</summary>
    private static string FoldForPrefix(string value)
    {
        var folded = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            folded.Append(ch switch
            {
                'İ' or 'I' or 'ı' or 'i' => 'i',
                'Ş' or 'ş' => 's',
                'Ğ' or 'ğ' => 'g',
                'Ü' or 'ü' => 'u',
                'Ö' or 'ö' => 'o',
                'Ç' or 'ç' => 'c',
                _ => ch
            });
        }

        return folded.ToString().ToLowerInvariant();
    }

    private static JsonObject BuildQuery(IReadOnlyList<string> terms, IReadOnlyList<string> phrases, string match)
    {
        var fields = match switch
        {
            "name" => new[] { "name.tr^4", "title.tr^4" },
            "content" => new[] { "text.tr" },
            _ => new[] { "name.tr^4", "title.tr^4", "text.tr" }
        };
        var phraseFields = match switch
        {
            "name" => new[] { "name.tr", "title.tr" },
            "content" => new[] { "text.tr" },
            _ => new[] { "name.tr", "title.tr", "text.tr" }
        };

        var must = new JsonArray();
        foreach (var term in terms)
            must.Add(TermClause(term, fields, phraseFields));

        foreach (var phrase in phrases)
        {
            var should = new JsonArray();
            foreach (var field in phraseFields)
            {
                should.Add(new JsonObject
                {
                    ["match_phrase"] = new JsonObject
                    {
                        [field] = phrase
                    }
                });
            }

            must.Add(new JsonObject
            {
                ["bool"] = new JsonObject
                {
                    ["should"] = should,
                    ["minimum_should_match"] = 1
                }
            });
        }

        var highlightFields = new JsonObject();
        foreach (var field in phraseFields)
        {
            highlightFields[field] = field == "text.tr"
                ? new JsonObject { ["fragment_size"] = 180, ["number_of_fragments"] = 3 }
                : new JsonObject { ["number_of_fragments"] = 1 };
        }

        return new JsonObject
        {
            ["size"] = CandidateWindow,
            ["query"] = new JsonObject
            {
                ["bool"] = new JsonObject { ["must"] = must }
            },
            ["highlight"] = new JsonObject
            {
                ["pre_tags"] = new JsonArray(MarkOn),
                ["post_tags"] = new JsonArray(MarkOff),
                ["fields"] = highlightFields
            }
        };
    }

    private static List<SearchHit> ReadHits(JsonElement json)
    {
        var hits = new List<SearchHit>();
        if (!json.TryGetProperty("hits", out var outer) || !outer.TryGetProperty("hits", out var inner) || inner.ValueKind != JsonValueKind.Array)
            return hits;

        foreach (var hit in inner.EnumerateArray())
        {
            var id = hit.TryGetProperty("_source", out var source) && source.TryGetProperty("resourceId", out var resourceId)
                ? resourceId.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(id) && hit.TryGetProperty("_id", out var docId))
                id = docId.GetString();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var score = hit.TryGetProperty("_score", out var scoreNode) && scoreNode.TryGetDouble(out var parsed) ? parsed : 0;
            var nameHit = false;
            var contentHit = false;
            var snippets = new List<string>();
            if (hit.TryGetProperty("highlight", out var highlight) && highlight.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in highlight.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Array)
                        continue;
                    var isText = property.Name.StartsWith("text", StringComparison.Ordinal);
                    if (isText)
                        contentHit = true;
                    else
                        nameHit = true;
                    foreach (var fragment in property.Value.EnumerateArray())
                    {
                        var raw = fragment.GetString();
                        if (string.IsNullOrWhiteSpace(raw))
                            continue;
                        var marked = Mark(raw);
                        if (isText)
                            snippets.Add(marked);
                        else if (snippets.Count == 0)
                            snippets.Add(marked);
                    }
                }
            }

            hits.Add(new SearchHit(id, score, nameHit && contentHit ? "both" : nameHit ? "name" : "content", snippets));
        }

        return hits;
    }

    private static string Mark(string raw)
    {
        var encoded = WebUtility.HtmlEncode(raw);
        return encoded.Replace(MarkOn, "<mark>", StringComparison.Ordinal).Replace(MarkOff, "</mark>", StringComparison.Ordinal);
    }

    private sealed record SearchHit(string Id, double Score, string Match, IReadOnlyList<string> Snippets);
}
