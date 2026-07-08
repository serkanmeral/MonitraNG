using System.Text.Json;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services.Generation;

/// <summary>
/// Loads reusable data sources from <c>dm_data_sources</c> (DG) for <c>dataSourceRef</c> resolution (G4).
/// </summary>
public sealed class DocumentDataSourceCatalogProvider
{
    private const string DatasetName = "dm_data_sources";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private IReadOnlyDictionary<string, TemplateValueSourceModel>? _cache;

    public DocumentDataSourceCatalogProvider(IMngDataGatewayClient dg, IRequestContext ctx)
    {
        _dg = dg;
        _ctx = ctx;
    }

    public async Task<TemplateValueSourceModel?> TryGetValueSourceAsync(string? code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var map = await GetMapAsync(ct);
        return map.TryGetValue(code.Trim(), out var source) ? CloneValueSource(source) : null;
    }

    private async Task<IReadOnlyDictionary<string, TemplateValueSourceModel>> GetMapAsync(CancellationToken ct)
    {
        if (_cache is not null)
            return _cache;

        var merged = new Dictionary<string, TemplateValueSourceModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var builtin in BuiltInValueSources)
            merged[builtin.Key] = CloneValueSource(builtin.Value);

        var token = _ctx.BearerToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var rows = await _dg.QueryAsync<Dictionary<string, object?>>(DatasetName, "limit=200", token, ct);
                foreach (var row in rows)
                {
                    var entry = ParseDatasetRow(row);
                    if (entry.HasValue)
                        merged[entry.Value.Code] = entry.Value.ValueSource;
                }
            }
            catch
            {
                // Dataset may not exist yet.
            }
        }

        _cache = merged;
        return merged;
    }

    private static (string Code, TemplateValueSourceModel ValueSource)? ParseDatasetRow(Dictionary<string, object?> row)
    {
        var code = ReadString(row, "code");
        var definitionJson = ReadString(row, "definitionJson");
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(definitionJson))
            return null;

        var isActive = ReadBool(row, "isActive") ?? true;
        if (!isActive)
            return null;

        try
        {
            var source = JsonSerializer.Deserialize<TemplateValueSourceModel>(definitionJson, JsonOptions);
            if (source is null)
                return null;

            if (string.IsNullOrWhiteSpace(source.Provider))
            {
                var provider = ReadString(row, "provider");
                if (!string.IsNullOrWhiteSpace(provider))
                    source.Provider = provider;
            }

            return (code.Trim(), source);
        }
        catch
        {
            return null;
        }
    }

    private static TemplateValueSourceModel CloneValueSource(TemplateValueSourceModel source) =>
        JsonSerializer.Deserialize<TemplateValueSourceModel>(
            JsonSerializer.Serialize(source, JsonOptions),
            JsonOptions) ?? source;

    private static string? ReadString(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var raw) || raw is null)
            return null;
        return raw.ToString()?.Trim();
    }

    private static readonly IReadOnlyDictionary<string, TemplateValueSourceModel> BuiltInValueSources =
        new Dictionary<string, TemplateValueSourceModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["odak.shipmentLines.byParentLine"] = DeserializeBuiltin(
                """{"mode":"queryPage","provider":"dg","dataset":"odak_sevkiyat_kalemleri","match":{"parentLineId":"{{runtime.contextId}}"},"query":"sort=lineNo&limit=50"}"""),
            ["odak.packageShipmentLines.byPackage"] = DeserializeBuiltin(
                """{"mode":"queryPage","provider":"dg","dataset":"odak_sevkiyat_kalemleri","match":{"parentPackageId":"{{runtime.contextId}}"},"query":"sort=lineNo&limit=200"}""")
        };

    private static TemplateValueSourceModel DeserializeBuiltin(string json) =>
        JsonSerializer.Deserialize<TemplateValueSourceModel>(json, JsonOptions)
        ?? throw new InvalidOperationException("Built-in data source definition is invalid.");

    private static bool? ReadBool(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var raw) || raw is null)
            return null;
        if (raw is bool b)
            return b;
        return bool.TryParse(raw.ToString(), out var parsed) ? parsed : null;
    }
}
