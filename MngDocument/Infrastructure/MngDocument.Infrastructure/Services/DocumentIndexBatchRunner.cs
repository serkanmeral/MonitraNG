using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Walks an already collected file list. The caller holds the domain job gate.
/// </summary>
public sealed class DocumentIndexBatchRunner
{
    private const int MaxChars = 400_000;
    private const string EmptyText = "Belgeden metin çıkmadı.";
    private const string Unreadable = "Dosya okunamadı.";
    private const string WriteFailed = "İndeks yazılamadı.";

    private readonly IMngDataGatewayClient _dg;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MngDocumentSettings _settings;
    private readonly ILogger<DocumentIndexBatchRunner> _logger;

    public DocumentIndexBatchRunner(
        IMngDataGatewayClient dg,
        IHttpClientFactory httpClientFactory,
        IOptions<MngDocumentSettings> settings,
        ILogger<DocumentIndexBatchRunner> logger)
    {
        _dg = dg;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        BulkIndexJob job,
        IReadOnlyList<(string Id, string Name)> files,
        string token,
        string domain,
        CancellationToken stop)
    {
        var stopped = false;
        try
        {
            foreach (var file in files)
            {
                if (stop.IsCancellationRequested)
                {
                    stopped = true;
                    break;
                }

                job.SetCurrent(file.Name);
                try
                {
                    var outcome = await IndexOneAsync(file.Id, file.Name, token, domain, CancellationToken.None);
                    if (outcome == "skipped")
                        job.AddSkipped(file.Id, file.Name);
                    else if (outcome == "done")
                        job.AddIndexed(file.Id, file.Name);
                    else
                        job.AddFailed(file.Id, file.Name, outcome);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Bulk index item failed for {ResourceId}", file.Id);
                    job.AddFailed(file.Id, file.Name, WriteFailed);
                }
            }
        }
        finally
        {
            job.Finish(stopped || stop.IsCancellationRequested);
        }
    }

    /// <returns>done, skipped, or a short failure reason.</returns>
    private async Task<string> IndexOneAsync(
        string id,
        string name,
        string token,
        string domain,
        CancellationToken ct)
    {
        var resource = await _dg.GetByIdAsync<DmResource>(DmDatasets.Resources, id, token, ct);
        if (resource is null || string.IsNullOrWhiteSpace(resource.__dataId))
            return "Dosya bulunamadı.";

        var version = resource.currentVersionNumber ?? 1;
        if (IsCurrent(resource))
            return "skipped";

        await WriteStatusAsync(id, "running", null, null, version, token, ct);

        string text;
        try
        {
            text = await ReadTextAsync(resource, token, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bulk index could not read {ResourceId}", id);
            await WriteStatusAsync(id, "failed", Unreadable, null, version, token, ct);
            return Unreadable;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            await WriteStatusAsync(id, "failed", EmptyText, null, version, token, ct);
            return EmptyText;
        }

        var stored = text.Length > MaxChars ? text[..MaxChars] : text;
        try
        {
            await WriteSearchAsync(resource, name, stored, token, domain, ct);
            await WriteStatusAsync(id, "done", null, stored.Length, version, token, ct);
            return "done";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bulk index could not write {ResourceId}", id);
            await WriteStatusAsync(id, "failed", WriteFailed, null, version, token, ct);
            return WriteFailed;
        }
    }

    private async Task<string> ReadTextAsync(DmResource resource, string token, CancellationToken ct)
    {
        if (string.Equals(resource.type, ResourceType.Markdown, StringComparison.OrdinalIgnoreCase))
            return resource.content ?? string.Empty;

        var (path, fileName) = ReadFileField(resource.file);
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException("File path is missing.");

        var bytes = await _dg.DownloadFileAsync(path, token, ct);
        return IndexPlainText.ExtractFile(resource.extension, fileName ?? resource.name, bytes);
    }

    private static bool IsCurrent(DmResource resource)
    {
        var status = string.IsNullOrWhiteSpace(resource.indexStatus) ? "none" : resource.indexStatus.Trim();
        if (!string.Equals(status, "done", StringComparison.OrdinalIgnoreCase))
            return false;
        var version = resource.currentVersionNumber ?? 1;
        return resource.indexedVersionNumber is int indexed && indexed == version;
    }

    private async Task WriteSearchAsync(
        DmResource resource,
        string name,
        string text,
        string token,
        string domain,
        CancellationToken ct)
    {
        var baseUrl = (_settings.OpenSearch.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("OpenSearch base URL is empty.");

        var index = DiSearchIndex.Name(domain);
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

        using var put = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/{index}/_doc/{resource.__dataId}");
        put.Content = JsonContent.Create(new
        {
            resourceId = resource.__dataId,
            title = resource.title ?? name,
            name,
            text,
            indexedAt = DateTime.UtcNow
        });
        using var written = await client.SendAsync(put, ct);
        if (!written.IsSuccessStatusCode)
        {
            var body = await written.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"OpenSearch document write failed: {(int)written.StatusCode} {body}");
        }
    }

    private async Task WriteStatusAsync(
        string id,
        string status,
        string? error,
        int? charCount,
        int version,
        string token,
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
        await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, id, payload, token, ct);
    }

    private static (string? Path, string? Name) ReadFileField(JsonElement? file)
    {
        if (file is null || file.Value.ValueKind != JsonValueKind.Object)
            return (null, null);

        string? path = file.Value.TryGetProperty("path", out var pathValue) && pathValue.ValueKind == JsonValueKind.String
            ? pathValue.GetString()
            : null;
        string? name = file.Value.TryGetProperty("file_name", out var nameValue) && nameValue.ValueKind == JsonValueKind.String
            ? nameValue.GetString()
            : null;
        return (string.IsNullOrWhiteSpace(path) ? null : path, string.IsNullOrWhiteSpace(name) ? null : name);
    }
}
