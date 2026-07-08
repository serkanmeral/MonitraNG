using System.Text.Json;
using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace MngDocument.Infrastructure.Services.Generation;

/// <summary>
/// Loads document producers from <c>dm_document_producers</c> (DG) with appsettings fallback (G4).
/// </summary>
public sealed class DocumentProducerCatalogProvider
{
    private const string DatasetName = "dm_document_producers";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly DocumentGenerationSettings _fallbackSettings;
    private IReadOnlyDictionary<string, DocumentGenerationProfileSettings>? _cache;

    public DocumentProducerCatalogProvider(
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IOptions<MngDocument.Application.Configuration.MngDocumentSettings> settings)
    {
        _dg = dg;
        _ctx = ctx;
        _fallbackSettings = settings.Value.DocumentGeneration ?? new DocumentGenerationSettings();
    }

    public async Task<DocumentGenerationProfileSettings> GetRequiredAsync(string code, CancellationToken ct = default) =>
        await TryGetAsync(code, ct) ?? throw new InvalidOperationException($"Unknown document producer: {code}");

    public async Task<DocumentGenerationProfileSettings?> TryGetAsync(string? code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var map = await GetMergedMapAsync(ct);
        return map.TryGetValue(code.Trim(), out var profile) ? profile : null;
    }

    public async Task<IReadOnlyList<DocumentProducerSummary>> AllAsync(CancellationToken ct = default)
    {
        var map = await GetMergedMapAsync(ct);
        return map.Values
            .Select(p => new DocumentProducerSummary
            {
                Code = p.Code,
                DisplayName = p.DisplayName,
                ContextType = p.ContextType,
                TemplateCode = p.TemplateCode
            })
            .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyDictionary<string, DocumentGenerationProfileSettings>> GetMergedMapAsync(
        CancellationToken ct)
    {
        if (_cache is not null)
            return _cache;

        var merged = new Dictionary<string, DocumentGenerationProfileSettings>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in _fallbackSettings.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Code))
                continue;
            merged[profile.Code.Trim()] = CloneProfile(profile);
        }

        var token = _ctx.BearerToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var rows = await _dg.QueryAsync<Dictionary<string, object?>>(DatasetName, "limit=200", token, ct);
                foreach (var row in rows)
                {
                    var profile = ParseDatasetRow(row);
                    if (profile is not null)
                        merged[profile.Code] = profile;
                }
            }
            catch
            {
                // Dataset may not exist yet — appsettings profiles remain authoritative.
            }
        }

        _cache = merged;
        return merged;
    }

    private static DocumentGenerationProfileSettings? ParseDatasetRow(Dictionary<string, object?> row)
    {
        var code = ReadString(row, "code");
        var displayName = ReadString(row, "displayName");
        var contextType = ReadString(row, "contextType");
        var templateCode = ReadString(row, "templateCode");
        var definitionJson = ReadString(row, "definitionJson");
        if (string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(contextType)
            || string.IsNullOrWhiteSpace(templateCode))
            return null;

        var isActive = ReadBool(row, "isActive") ?? true;
        if (!isActive)
            return null;

        ProducerDefinitionPayload? payload = null;
        if (!string.IsNullOrWhiteSpace(definitionJson))
        {
            try
            {
                payload = JsonSerializer.Deserialize<ProducerDefinitionPayload>(definitionJson, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        return new DocumentGenerationProfileSettings
        {
            Code = code.Trim(),
            DisplayName = displayName?.Trim() ?? code.Trim(),
            TemplateCode = templateCode.Trim(),
            ContextType = contextType.Trim(),
            OutputFormat = NormalizeOutputFormat(payload?.OutputFormat),
            OutputFolderPath = payload?.OutputFolderPath ?? new List<string>(),
            FileNamePattern = payload?.FileNamePattern ?? "{docNo}.docx",
            Idempotency = payload?.Idempotency,
            Defaults = payload?.Defaults ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static DocumentGenerationProfileSettings CloneProfile(DocumentGenerationProfileSettings source) =>
        new()
        {
            Code = source.Code,
            DisplayName = source.DisplayName,
            TemplateCode = source.TemplateCode,
            ContextType = source.ContextType,
            OutputFormat = NormalizeOutputFormat(source.OutputFormat),
            OutputFolderPath = source.OutputFolderPath?.ToList() ?? new List<string>(),
            FileNamePattern = source.FileNamePattern,
            Idempotency = source.Idempotency is null
                ? null
                : new DocumentGenerationIdempotencySettings
                {
                    Dataset = source.Idempotency.Dataset,
                    GuardField = source.Idempotency.GuardField,
                    WritebackFields = source.Idempotency.WritebackFields?.ToList() ?? new List<string>()
                },
            Defaults = new Dictionary<string, string>(source.Defaults, StringComparer.OrdinalIgnoreCase)
        };

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

    private static string NormalizeOutputFormat(string? format)
    {
        var normalized = format?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "xlsx" => "xlsx",
            "pptx" => "pptx",
            _ => "docx"
        };
    }

    private sealed class ProducerDefinitionPayload
    {
        public string? OutputFormat { get; set; }
        public List<string>? OutputFolderPath { get; set; }
        public string? FileNamePattern { get; set; }
        public DocumentGenerationIdempotencySettings? Idempotency { get; set; }
        public Dictionary<string, string>? Defaults { get; set; }
    }
}

public sealed class DocumentProducerSummary
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ContextType { get; init; } = string.Empty;
    public string TemplateCode { get; init; } = string.Empty;
}
