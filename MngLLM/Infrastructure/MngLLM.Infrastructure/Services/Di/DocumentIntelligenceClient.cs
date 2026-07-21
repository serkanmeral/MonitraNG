using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLLM.Application.Configuration;
using MngLLM.Application.Services;
using MngLLM.Domain.Exceptions;

namespace MngLLM.Infrastructure.Services.Di;

/// <summary>
/// Loads DI file content: metadata from MngDocument, bytes from MngDataGateway files API.
/// </summary>
public sealed class DocumentIntelligenceClient : IDocumentIntelligenceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MngLLMSettings _settings;
    private readonly ILogger<DocumentIntelligenceClient> _logger;

    public DocumentIntelligenceClient(
        IHttpClientFactory httpClientFactory,
        IOptions<MngLLMSettings> settings,
        ILogger<DocumentIntelligenceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<DiResourceContent> GetFileContentAsync(
        string resourceId,
        string? authorizationHeader,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new DiExtractException("resourceId is required.", 400);

        var documentBase = (_settings.Actors.MngDocument ?? string.Empty).TrimEnd('/');
        var gatewayBase = (_settings.Actors.MngDataGateway ?? string.Empty).TrimEnd('/');

        if (string.IsNullOrWhiteSpace(documentBase))
            throw new DiExtractException("MngDocument actor URL is not configured.", 503);
        if (string.IsNullOrWhiteSpace(gatewayBase))
            throw new DiExtractException("MngDataGateway actor URL is not configured.", 503);

        var meta = await GetResourceMetadataAsync(documentBase, resourceId, authorizationHeader, cancellationToken);
        if (string.IsNullOrWhiteSpace(meta.FilePath))
            throw new DiExtractException(
                "Resource has no filePath. Extract expects a DI file resource (e.g. UBL XML).",
                422);

        var bytes = await DownloadFileAsync(gatewayBase, meta.FilePath!, authorizationHeader, cancellationToken);

        return new DiResourceContent(
            meta.Id,
            meta.Name ?? resourceId,
            meta.Extension,
            meta.MimeType,
            meta.FilePath,
            bytes);
    }

    private async Task<ResourceMeta> GetResourceMetadataAsync(
        string documentBase,
        string resourceId,
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var url = $"{documentBase}/api/v1/resources/{Uri.EscapeDataString(resourceId)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuth(request, authorizationHeader);

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new DiExtractException($"DI resource '{resourceId}' not found.", 404);
        if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
            throw new DiExtractException("Not allowed to access DI resource.", (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("MngDocument GET {Url} failed: {Status} {Body}", url, (int)response.StatusCode, body);
            throw new DiExtractException($"MngDocument returned {(int)response.StatusCode}.", 502);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var meta = await JsonSerializer.DeserializeAsync<ResourceMeta>(stream, JsonOptions, cancellationToken);
        if (meta is null || string.IsNullOrWhiteSpace(meta.Id))
            throw new DiExtractException("Invalid resource metadata from MngDocument.", 502);

        return meta;
    }

    private async Task<byte[]> DownloadFileAsync(
        string gatewayBase,
        string filePath,
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var url = $"{gatewayBase}/api/v1/files/download?filePath={Uri.EscapeDataString(filePath)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuth(request, authorizationHeader);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "DataGateway file download failed for {FilePath}: {Status}",
                filePath, (int)response.StatusCode);
            throw new DiExtractException(
                $"Failed to download file from DataGateway ({(int)response.StatusCode}).",
                response.StatusCode == HttpStatusCode.NotFound ? 404 : 502);
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static void ApplyAuth(HttpRequestMessage request, string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return;
        if (AuthenticationHeaderValue.TryParse(authorizationHeader, out var header))
            request.Headers.Authorization = header;
        else
            request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
    }

    private sealed class ResourceMeta
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Extension { get; set; }
        public string? MimeType { get; set; }
        public string? FilePath { get; set; }
        public string? Type { get; set; }
    }
}
