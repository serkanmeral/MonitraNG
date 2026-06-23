using System.Text.Json;
using System.Text.Json.Serialization;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

public sealed class DocumentTemplateService : IDocumentTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string SchemaVersion = "1.0";
    private const string ListQuery = "limit=200&expand=false&showHistory=false&sort=-updatedAt";

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IResourceService _resources;

    public DocumentTemplateService(
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IResourceService resources)
    {
        _dg = dg;
        _ctx = ctx;
        _resources = resources;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<TemplateListResult> ListAsync(CancellationToken ct = default)
    {
        var page = await _dg.QueryPageAsync(
            DmDatasets.DocumentTemplates,
            new Dictionary<string, object?>(),
            ListQuery,
            Token,
            ct);

        var items = page.Items
            .Select(MapSummaryRow)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt ?? DateTime.MinValue)
            .ToList();

        return new TemplateListResult { Items = items, Total = page.Total };
    }

    public async Task<TemplateDetailDto> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var row = await LoadTemplateOrThrowAsync(id, ct);
        return ToDetail(row);
    }

    public async Task<TemplateDetailDto> CreateFromSourceAsync(
        CreateTemplateFromSourceRequest request,
        CancellationToken ct = default)
    {
        var sourceId = request.SourceResourceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw DocumentException.Validation(
                "SOURCE_REQUIRED",
                "sourceResourceId is required.",
                "Kaynak dosya zorunludur.");
        }

        var resource = await _resources.GetByIdAsync(sourceId, ct);
        if (!string.Equals(resource.Type, ResourceType.File, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "SOURCE_NOT_FILE",
                "Source must be a file resource.",
                "Kaynak bir dosya olmalıdır.");
        }

        if (!DocxStructureParser.IsDocxExtension(resource.Extension))
        {
            throw DocumentException.Validation(
                "SOURCE_NOT_DOCX",
                "Only DOCX sources are supported in this phase.",
                "Bu aşamada yalnızca DOCX kaynak desteklenir.");
        }

        if (string.IsNullOrWhiteSpace(resource.FilePath))
        {
            throw DocumentException.Validation(
                "SOURCE_FILE_MISSING",
                "Source file path is missing.",
                "Kaynak dosya yolu bulunamadı.");
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = Path.GetFileNameWithoutExtension(resource.FileName ?? resource.Name) + " Şablonu";

        var emptyModel = JsonSerializer.Serialize(new TemplateModelDocument
        {
            SchemaVersion = SchemaVersion,
            Parameters = Array.Empty<TemplateParameterDto>()
        }, JsonOptions);

        var now = DateTime.UtcNow;
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["description"] = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ["sourceResourceId"] = sourceId,
            ["sourceFileName"] = resource.FileName ?? resource.Name,
            ["creationMode"] = "fromTemplate",
            ["status"] = "draft",
            ["modelJson"] = emptyModel,
            ["createdBy"] = _ctx.Username,
            ["createdAt"] = now,
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = now
        };

        var created = await _dg.CreateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            payload,
            Token,
            ct);

        if (created.__dataId is null)
        {
            throw DocumentException.Validation(
                "TEMPLATE_CREATE_FAILED",
                "Template could not be created.",
                "Şablon oluşturulamadı.");
        }

        return ToDetail(created);
    }

    public async Task<DocxStructureDto> GetSourceStructureAsync(string resourceId, CancellationToken ct = default)
    {
        var id = resourceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            throw DocumentException.NotFound();

        var resource = await _resources.GetByIdAsync(id, ct);
        if (!string.Equals(resource.Type, ResourceType.File, StringComparison.OrdinalIgnoreCase)
            || !DocxStructureParser.IsDocxExtension(resource.Extension)
            || string.IsNullOrWhiteSpace(resource.FilePath))
        {
            throw DocumentException.Validation(
                "SOURCE_NOT_DOCX",
                "Resource is not a DOCX file.",
                "Kaynak geçerli bir DOCX dosyası değil.");
        }

        var bytes = await _dg.DownloadFileAsync(resource.FilePath, Token, ct);
        using var ms = new MemoryStream(bytes, writable: false);
        var parsed = DocxStructureParser.Parse(ms);

        return new DocxStructureDto
        {
            ResourceId = id,
            FileName = resource.FileName ?? resource.Name,
            TableCount = parsed.TableCount,
            Paragraphs = parsed.Paragraphs
                .Select(p => new DocxParagraphDto { Index = p.Index, Text = p.Text })
                .ToList()
        };
    }

    public async Task<TemplateDetailDto> UpdateParametersAsync(
        string id,
        UpdateTemplateParametersRequest request,
        CancellationToken ct = default)
    {
        var templateId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateId))
            throw DocumentException.NotFound();

        var existing = await LoadTemplateOrThrowAsync(templateId, ct);
        var parameters = request.Parameters ?? Array.Empty<TemplateParameterDto>();

        ValidateParameters(parameters);

        var model = new TemplateModelDocument
        {
            SchemaVersion = SchemaVersion,
            Parameters = parameters
        };

        var payload = new Dictionary<string, object?>
        {
            ["name"] = existing.name,
            ["description"] = existing.description,
            ["sourceResourceId"] = existing.sourceResourceId,
            ["sourceFileName"] = existing.sourceFileName,
            ["creationMode"] = existing.creationMode ?? "fromTemplate",
            ["status"] = existing.status ?? "draft",
            ["modelJson"] = JsonSerializer.Serialize(model, JsonOptions),
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            templateId,
            payload,
            Token,
            ct);

        return ToDetail(updated);
    }

    private static void ValidateParameters(IReadOnlyList<TemplateParameterDto> parameters)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters)
        {
            var key = p.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw DocumentException.Validation(
                    "PARAM_KEY_REQUIRED",
                    "Parameter key is required.",
                    "Parametre anahtarı zorunludur.");
            }

            if (!keys.Add(key))
            {
                throw DocumentException.Validation(
                    "PARAM_KEY_DUPLICATE",
                    $"Duplicate parameter key: {key}",
                    $"Yinelenen parametre anahtarı: {key}");
            }

            if (string.Equals(p.ValueSourceMode, "incremental", StringComparison.OrdinalIgnoreCase)
                && (p.Incremental is null || string.IsNullOrWhiteSpace(p.Incremental.Format)))
            {
                throw DocumentException.Validation(
                    "INCREMENTAL_FORMAT_REQUIRED",
                    "Incremental parameters require format.",
                    "Otomatik numara parametreleri için format zorunludur.");
            }
        }
    }

    private async Task<DmDocumentTemplate> LoadTemplateOrThrowAsync(string id, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<DmDocumentTemplate>(DmDatasets.DocumentTemplates, id, Token, ct);
        if (row is null || row.__dataId is null)
            throw DocumentException.NotFound("Şablon bulunamadı.");
        return row;
    }

    private static TemplateSummaryDto MapSummaryRow(Dictionary<string, object?> row)
    {
        var json = JsonSerializer.Serialize(row, JsonOptions);
        var entity = JsonSerializer.Deserialize<DmDocumentTemplate>(json, JsonOptions) ?? new DmDocumentTemplate();
        return ToSummary(entity);
    }

    private static TemplateSummaryDto ToSummary(DmDocumentTemplate row) =>
        new()
        {
            Id = row.__dataId ?? string.Empty,
            Name = row.name ?? string.Empty,
            Description = row.description,
            SourceResourceId = row.sourceResourceId ?? string.Empty,
            SourceFileName = row.sourceFileName,
            CreationMode = row.creationMode ?? "fromTemplate",
            Status = row.status ?? "draft",
            ParameterCount = ParseModel(row.modelJson).Parameters.Count,
            CreatedBy = row.createdBy,
            CreatedAt = row.createdAt,
            UpdatedAt = row.updatedAt
        };

    private static TemplateDetailDto ToDetail(DmDocumentTemplate row)
    {
        var model = ParseModel(row.modelJson);
        var summary = ToSummary(row);
        return new TemplateDetailDto
        {
            Id = summary.Id,
            Name = summary.Name,
            Description = summary.Description,
            SourceResourceId = summary.SourceResourceId,
            SourceFileName = summary.SourceFileName,
            CreationMode = summary.CreationMode,
            Status = summary.Status,
            ParameterCount = model.Parameters.Count,
            CreatedBy = summary.CreatedBy,
            CreatedAt = summary.CreatedAt,
            UpdatedAt = summary.UpdatedAt,
            SchemaVersion = model.SchemaVersion,
            Parameters = model.Parameters
        };
    }

    private static TemplateModelDocument ParseModel(string? modelJson)
    {
        if (string.IsNullOrWhiteSpace(modelJson))
        {
            return new TemplateModelDocument
            {
                SchemaVersion = SchemaVersion,
                Parameters = Array.Empty<TemplateParameterDto>()
            };
        }

        try
        {
            return JsonSerializer.Deserialize<TemplateModelDocument>(modelJson, JsonOptions)
                   ?? new TemplateModelDocument { SchemaVersion = SchemaVersion, Parameters = Array.Empty<TemplateParameterDto>() };
        }
        catch
        {
            return new TemplateModelDocument
            {
                SchemaVersion = SchemaVersion,
                Parameters = Array.Empty<TemplateParameterDto>()
            };
        }
    }

    private sealed class TemplateModelDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public IReadOnlyList<TemplateParameterDto> Parameters { get; set; } = Array.Empty<TemplateParameterDto>();
    }
}
