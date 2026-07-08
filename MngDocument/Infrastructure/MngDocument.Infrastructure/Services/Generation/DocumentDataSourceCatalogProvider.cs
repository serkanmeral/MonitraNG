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
    private IReadOnlyDictionary<string, DataSourceCatalogEntry>? _cache;

    public DocumentDataSourceCatalogProvider(IMngDataGatewayClient dg, IRequestContext ctx)
    {
        _dg = dg;
        _ctx = ctx;
    }

    public async Task<TemplateValueSourceModel?> TryGetValueSourceAsync(string? code, CancellationToken ct = default)
    {
        var entry = await TryGetEntryAsync(code, ct);
        return entry is null ? null : CloneValueSource(entry.Definition);
    }

    public async Task<IReadOnlyList<DataSourceCatalogEntry>> ListEntriesAsync(CancellationToken ct = default)
    {
        var map = await GetMapAsync(ct);
        return map.Values
            .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Code, StringComparer.OrdinalIgnoreCase)
            .Select(CloneEntry)
            .ToList();
    }

    public async Task<DataSourceCatalogEntry?> TryGetEntryAsync(string? code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var map = await GetMapAsync(ct);
        return map.TryGetValue(code.Trim(), out var entry) ? CloneEntry(entry) : null;
    }

    private async Task<IReadOnlyDictionary<string, DataSourceCatalogEntry>> GetMapAsync(CancellationToken ct)
    {
        if (_cache is not null)
            return _cache;

        var merged = new Dictionary<string, DataSourceCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var builtin in BuiltInEntries)
            merged[builtin.Key] = CloneEntry(builtin.Value);

        var token = _ctx.BearerToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var rows = await _dg.QueryAsync<Dictionary<string, object?>>(DatasetName, "limit=200", token, ct);
                foreach (var row in rows)
                {
                    var entry = ParseDatasetRow(row);
                    if (entry is not null)
                        merged[entry.Code] = entry;
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

    private static DataSourceCatalogEntry? ParseDatasetRow(Dictionary<string, object?> row)
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

            var displayName = ReadString(row, "displayName");
            return new DataSourceCatalogEntry
            {
                Code = code.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? code.Trim() : displayName.Trim(),
                Provider = string.IsNullOrWhiteSpace(source.Provider) ? "dg" : source.Provider.Trim(),
                Definition = source
            };
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

    private static DataSourceCatalogEntry CloneEntry(DataSourceCatalogEntry entry) =>
        new()
        {
            Code = entry.Code,
            DisplayName = entry.DisplayName,
            Provider = entry.Provider,
            Definition = CloneValueSource(entry.Definition)
        };

    private static string? ReadString(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var raw) || raw is null)
            return null;
        return raw.ToString()?.Trim();
    }

    private static readonly IReadOnlyDictionary<string, DataSourceCatalogEntry> BuiltInEntries =
        new Dictionary<string, DataSourceCatalogEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["odak.shipmentLines.byParentLine"] = new DataSourceCatalogEntry
            {
                Code = "odak.shipmentLines.byParentLine",
                DisplayName = "Sevkiyat satırları (sipariş kalemi)",
                Provider = "dg",
                Definition = DeserializeBuiltin(
                    """{"mode":"queryPage","provider":"dg","dataset":"odak_sevkiyat_kalemleri","match":{"parentLineId":"{{runtime.contextId}}"},"query":"sort=lineNo&limit=50","columns":[{"sourceField":"lineNo","header":"Kalem No"},{"sourceField":"lineDescription","header":"Tanım"},{"sourceField":"shippedQuantity","header":"Sevk Miktarı","format":"N0"},{"sourceField":"lineMode","header":"Mod"}]}""")
            },
            ["odak.packageShipmentLines.byPackage"] = new DataSourceCatalogEntry
            {
                Code = "odak.packageShipmentLines.byPackage",
                DisplayName = "Sevkiyat satırları (iş paketi)",
                Provider = "dg",
                Definition = DeserializeBuiltin(
                    """{"mode":"queryPage","provider":"dg","dataset":"odak_sevkiyat_kalemleri","match":{"parentPackageId":"{{runtime.contextId}}"},"query":"sort=lineNo&limit=200","columns":[{"sourceField":"lineNo","header":"Kalem No"},{"sourceField":"lineDescription","header":"Tanım"},{"sourceField":"shippedQuantity","header":"Sevk Miktarı","format":"N0"},{"sourceField":"lineMode","header":"Mod"}]}""")
            },
            ["odak.packageLines.byPackage"] = new DataSourceCatalogEntry
            {
                Code = "odak.packageLines.byPackage",
                DisplayName = "Sipariş kalemleri (iş paketi)",
                Provider = "dg",
                Definition = DeserializeBuiltin(
                    """{"mode":"queryPage","provider":"dg","dataset":"odak_siparis_kalemleri","match":{"parentPackageId":"{{runtime.contextId}}"},"query":"sort=lineNo&limit=500","columns":[{"sourceField":"lineNo","header":"Kalem No"},{"sourceField":"customerPoItemNo","header":"PO Kalem"},{"sourceField":"description","header":"Tanım"},{"sourceField":"quantity","header":"Miktar","format":"N0"},{"sourceField":"shippedQuantity","header":"Sevk","format":"N0"},{"sourceField":"deliveryDate","header":"Termin"},{"sourceField":"cocDocNo","header":"CoC No"}]}""")
            }
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
