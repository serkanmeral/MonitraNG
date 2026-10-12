using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Drops stored index and summary fields when the file content changes,
/// and removes the OpenSearch document for that resource.
/// </summary>
public sealed class DocumentIndexCleaner
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRequestContext _ctx;
    private readonly SummaryJobBoard _board;
    private readonly MngDocumentSettings _settings;
    private readonly ILogger<DocumentIndexCleaner> _logger;

    public DocumentIndexCleaner(
        IHttpClientFactory httpClientFactory,
        IRequestContext ctx,
        SummaryJobBoard board,
        IOptions<MngDocumentSettings> settings,
        ILogger<DocumentIndexCleaner> logger)
    {
        _httpClientFactory = httpClientFactory;
        _ctx = ctx;
        _board = board;
        _settings = settings.Value;
        _logger = logger;
    }

    public static void Apply(Dictionary<string, object?> payload)
    {
        payload["indexStatus"] = "none";
        payload["indexedAt"] = null;
        payload["indexError"] = null;
        payload["indexCharCount"] = null;
        payload["indexedVersionNumber"] = null;
        payload["summaryStatus"] = "none";
        payload["summaryText"] = null;
        payload["summarizedAt"] = null;
        payload["summaryError"] = null;
        payload["summaryStep"] = null;
        payload["summaryChunked"] = false;
        payload["summarizedVersionNumber"] = null;
        payload["summaryCompleted"] = 0;
        payload["summaryTotal"] = 0;
        payload["summaryUpdatedAt"] = null;
        payload["summaryLogText"] = null;
    }

    public async Task DeleteSearchAsync(string id, CancellationToken ct)
    {
        _board.Remove(id);
        var baseUrl = (_settings.OpenSearch.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(id))
            return;

        var domain = string.IsNullOrWhiteSpace(_ctx.DomainName) ? "default" : _ctx.DomainName.Trim();
        var client = _httpClientFactory.CreateClient("OpenSearch");
        using var response = await client.DeleteAsync($"{baseUrl}/{DiSearchIndex.Name(domain)}/_doc/{id}", ct);
        if (response.IsSuccessStatusCode || (int)response.StatusCode == 404)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning(
            "OpenSearch index delete failed for {ResourceId}: {Status} {Body}",
            id,
            (int)response.StatusCode,
            body.Length > 300 ? body[..300] : body);
    }
}
