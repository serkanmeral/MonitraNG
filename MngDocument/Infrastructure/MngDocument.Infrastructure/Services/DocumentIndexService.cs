using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Seçilen docx veya markdown belgenin düz metnini OpenSearch'e yazar.
/// Model çağırmaz. Aynı anda bir etki alanında tek iş çalışır.
/// </summary>
public sealed class DocumentIndexService : IDocumentIndexService
{
    private const int MaxChars = 400_000;

    private readonly IResourceService _resources;
    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MngDocumentSettings _settings;
    private readonly ILogger<DocumentIndexService> _logger;

    public DocumentIndexService(
        IResourceService resources,
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IHttpClientFactory httpClientFactory,
        IOptions<MngDocumentSettings> settings,
        ILogger<DocumentIndexService> logger)
    {
        _resources = resources;
        _dg = dg;
        _ctx = ctx;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ResourceIndexResultDto> GetStatusAsync(string id, CancellationToken ct = default)
    {
        var resource = await _resources.GetByIdAsync(id, ct);
        return FromResource(resource, Array.Empty<string>());
    }

    public async Task<ResourceIndexResultDto> IndexAsync(string id, CancellationToken ct = default)
    {
        var resource = await _resources.GetByIdAsync(id, ct);
        EnsureIndexable(resource);

        var gate = DocumentJobGate.For(DomainKey());
        if (!await gate.WaitAsync(0, ct))
        {
            throw DocumentException.Conflict(
                "DOCUMENT_JOB_BUSY",
                "Another document job is running.",
                "Başka bir belgenin indeks veya özet işi sürüyor. O bitince tekrar deneyin.");
        }

        var steps = new List<string> { "Dosya açıldı" };
        try
        {
            await WriteStatusAsync(id, "running", null, null, resource.CurrentVersionNumber, ct);
            var text = await ExtractTextAsync(resource, ct);
            steps.Add("Metin çıkarıldı");
            if (string.IsNullOrWhiteSpace(text))
            {
                return await FailAsync(id, resource.CurrentVersionNumber, "Belgeden metin çıkmadı.", steps, ct);
            }

            var stored = text.Length > MaxChars ? text[..MaxChars] : text;
            if (stored.Length < text.Length)
                steps.Add("Metin arama sınırına göre kısaltıldı");

            await WriteSearchAsync(resource, stored, ct);
            steps.Add("Aramaya yazıldı");

            var updated = await WriteStatusAsync(id, "done", null, stored.Length, resource.CurrentVersionNumber, ct);
            return new ResourceIndexResultDto
            {
                Status = "done",
                CharCount = stored.Length,
                IndexedAt = updated.IndexedAt,
                Steps = steps,
                Resource = updated
            };
        }
        catch (DocumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Document index failed for {ResourceId}", id);
            return await FailAsync(id, resource.CurrentVersionNumber, "İndeks yazılamadı.", steps, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string> ExtractTextAsync(ResourceDto resource, CancellationToken ct)
    {
        if (string.Equals(resource.Type, ResourceType.Markdown, StringComparison.OrdinalIgnoreCase))
        {
            var page = await _resources.GetMarkdownContentAsync(resource.Id, ct);
            return page.Content ?? string.Empty;
        }

        var (bytes, _) = await _resources.GetCurrentFileContentAsync(resource.Id, ct);
        using var stream = new MemoryStream(bytes);
        return DocxStructureParser.ExtractPlainText(stream);
    }

    private async Task WriteSearchAsync(ResourceDto resource, string text, CancellationToken ct)
    {
        var baseUrl = (_settings.OpenSearch.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("OpenSearch base URL is empty.");
        }

        var index = DiSearchIndex.Name(DomainKey());
        var client = _httpClientFactory.CreateClient("OpenSearch");
        using (var create = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/{index}"))
        {
            create.Content = JsonContent.Create(new
            {
                mappings = new
                {
                    properties = new
                    {
                        resourceId = new { type = "keyword" },
                        title = new { type = "text" },
                        name = new { type = "text" },
                        text = new { type = "text" },
                        indexedAt = new { type = "date" }
                    }
                }
            });
            using var created = await client.SendAsync(create, ct);
            if (!created.IsSuccessStatusCode && (int)created.StatusCode != 400)
            {
                var body = await created.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenSearch index create failed: {(int)created.StatusCode} {body}");
            }
        }

        var now = DateTime.UtcNow;
        using var put = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/{index}/_doc/{resource.Id}");
        put.Content = JsonContent.Create(new
        {
            resourceId = resource.Id,
            title = resource.Title ?? resource.Name,
            name = resource.Name,
            text,
            indexedAt = now
        });
        using var written = await client.SendAsync(put, ct);
        if (!written.IsSuccessStatusCode)
        {
            var body = await written.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"OpenSearch document write failed: {(int)written.StatusCode} {body}");
        }
    }

    private async Task<ResourceIndexResultDto> FailAsync(
        string id,
        int version,
        string error,
        List<string> steps,
        CancellationToken ct)
    {
        var updated = await WriteStatusAsync(id, "failed", error, null, version, ct);
        return new ResourceIndexResultDto
        {
            Status = "failed",
            Error = error,
            Steps = steps,
            Resource = updated
        };
    }

    private async Task<ResourceDto> WriteStatusAsync(
        string id,
        string status,
        string? error,
        int? charCount,
        int version,
        CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["indexStatus"] = status,
            ["indexedAt"] = DateTime.UtcNow,
            ["indexError"] = error,
            ["indexCharCount"] = charCount,
            ["indexedVersionNumber"] = version
        };
        await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, id, payload, _ctx.BearerToken, ct);
        return await _resources.GetByIdAsync(id, ct);
    }

    private static ResourceIndexResultDto FromResource(ResourceDto resource, IReadOnlyList<string> steps) =>
        new()
        {
            Status = resource.IndexStatus,
            CharCount = resource.IndexCharCount ?? 0,
            Error = resource.IndexError,
            IndexedAt = resource.IndexedAt,
            Steps = steps,
            Resource = resource
        };

    private static void EnsureIndexable(ResourceDto resource)
    {
        if (string.Equals(resource.Type, ResourceType.Markdown, StringComparison.OrdinalIgnoreCase))
            return;
        if (string.Equals(resource.Type, ResourceType.File, StringComparison.OrdinalIgnoreCase)
            && (DocxStructureParser.IsDocxExtension(resource.Extension)
                || DocxStructureParser.IsDocxExtension(Path.GetExtension(resource.FileName ?? string.Empty))))
            return;

        throw DocumentException.Validation(
            "INDEX_UNSUPPORTED",
            "Only docx files and markdown pages can be indexed.",
            "Yalnızca docx dosyası ve markdown sayfası indekslenir.");
    }

    private string DomainKey() =>
        string.IsNullOrWhiteSpace(_ctx.DomainName) ? "default" : _ctx.DomainName.Trim();
}
