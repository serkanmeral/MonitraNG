using System.Text.Json;
using System.Text.Json.Nodes;
using MngDocument.Application.Contracts.Generation;
using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services.Generation;

/// <summary>
/// Loads context types from <c>dm_document_context_types</c> (DG) with built-in fallback (G3).
/// </summary>
public sealed class DocumentContextCatalogProvider
{
    private const string DatasetName = "dm_document_context_types";

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private IReadOnlyDictionary<string, DocumentContextTypeDefinition>? _cache;

    public DocumentContextCatalogProvider(IMngDataGatewayClient dg, IRequestContext ctx)
    {
        _dg = dg;
        _ctx = ctx;
    }

    public async Task<DocumentContextTypeDefinition> GetRequiredAsync(string type, CancellationToken ct = default) =>
        await TryGetAsync(type, ct) ?? throw new InvalidOperationException($"Unknown document context type: {type}");

    public async Task<DocumentContextTypeDefinition?> TryGetAsync(string? type, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(type))
            return null;

        var map = await GetMergedMapAsync(ct);
        return map.TryGetValue(type.Trim(), out var def) ? def : null;
    }

    public async Task<IReadOnlyList<DocumentContextTypeDefinition>> AllAsync(CancellationToken ct = default) =>
        (await GetMergedMapAsync(ct)).Values
            .OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<IReadOnlyDictionary<string, DocumentContextTypeDefinition>> GetMergedMapAsync(CancellationToken ct)
    {
        if (_cache is not null)
            return _cache;

        var merged = new Dictionary<string, DocumentContextTypeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var builtIn in DocumentContextCatalog.All())
            merged[builtIn.Type] = builtIn;

        var token = _ctx.BearerToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var rows = await _dg.QueryAsync<Dictionary<string, object?>>(DatasetName, "limit=200", token, ct);
                foreach (var row in rows)
                {
                    var def = ParseDatasetRow(row);
                    if (def is not null)
                        merged[def.Type] = def;
                }
            }
            catch
            {
                // Dataset may not exist yet — built-in catalog remains authoritative fallback.
            }
        }

        _cache = merged;
        return merged;
    }

    private static DocumentContextTypeDefinition? ParseDatasetRow(Dictionary<string, object?> row)
    {
        var type = ReadString(row, "type");
        var displayName = ReadString(row, "displayName");
        var rootDataset = ReadString(row, "rootDataset");
        var definitionJson = ReadString(row, "definitionJson");
        if (string.IsNullOrWhiteSpace(type)
            || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(rootDataset))
            return null;

        var isActive = ReadBool(row, "isActive") ?? true;
        if (!isActive)
            return null;

        var relations = Array.Empty<DocumentContextRelationDefinition>();
        if (!string.IsNullOrWhiteSpace(definitionJson))
        {
            try
            {
                var node = JsonNode.Parse(definitionJson) as JsonObject;
                if (node?["relations"] is JsonArray relArray)
                {
                    relations = relArray
                        .OfType<JsonObject>()
                        .Select(o => new DocumentContextRelationDefinition
                        {
                            Path = o["path"]?.ToString() ?? string.Empty,
                            Dataset = o["dataset"]?.ToString() ?? string.Empty,
                            Optional = o["optional"]?.GetValue<bool>() ?? false
                        })
                        .Where(r => !string.IsNullOrWhiteSpace(r.Path) && !string.IsNullOrWhiteSpace(r.Dataset))
                        .ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        var builtIn = DocumentContextCatalog.TryGet(type);
        return new DocumentContextTypeDefinition
        {
            Type = type.Trim(),
            DisplayName = displayName.Trim(),
            RootDataset = rootDataset.Trim(),
            Relations = relations.Length > 0 ? relations : builtIn?.Relations ?? Array.Empty<DocumentContextRelationDefinition>(),
            Fields = builtIn?.Fields ?? Array.Empty<DocumentContextFieldDto>()
        };
    }

    private static string? ReadString(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var raw) || raw is null)
            return null;
        return raw.ToString()?.Trim();
    }

    private static bool? ReadBool(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var raw) || raw is null)
            return null;
        if (raw is bool b)
            return b;
        return bool.TryParse(raw.ToString(), out var parsed) ? parsed : null;
    }
}
