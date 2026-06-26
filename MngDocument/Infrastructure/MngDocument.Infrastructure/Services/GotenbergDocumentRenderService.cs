using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Rendering;
using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Gotenberg REST API — arka planda headless LibreOffice kullanır (on-prem, kapalı ağ).
/// </summary>
public sealed class GotenbergDocumentRenderService : IDocumentRenderService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DocumentRenderingSettings _settings;
    private readonly ILogger<GotenbergDocumentRenderService> _logger;

    public GotenbergDocumentRenderService(
        IHttpClientFactory httpClientFactory,
        IOptions<MngDocumentSettings> options,
        ILogger<GotenbergDocumentRenderService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value.DocumentRendering;
        _logger = logger;
    }

    public async Task<DocumentRenderingStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            return new DocumentRenderingStatusDto
            {
                Enabled = false,
                GotenbergConfigured = !string.IsNullOrWhiteSpace(_settings.GotenbergBaseUrl),
                GotenbergReachable = false,
                GotenbergBaseUrl = _settings.GotenbergBaseUrl,
                Message = "Document rendering is disabled in configuration."
            };
        }

        if (string.IsNullOrWhiteSpace(_settings.GotenbergBaseUrl))
        {
            return new DocumentRenderingStatusDto
            {
                Enabled = true,
                GotenbergConfigured = false,
                GotenbergReachable = false,
                Message = "GotenbergBaseUrl is not configured."
            };
        }

        try
        {
            var client = CreateClient();
            using var response = await client.GetAsync("/health", ct);
            var ok = response.IsSuccessStatusCode;
            return new DocumentRenderingStatusDto
            {
                Enabled = true,
                GotenbergConfigured = true,
                GotenbergReachable = ok,
                GotenbergBaseUrl = _settings.GotenbergBaseUrl,
                Message = ok ? "Gotenberg (LibreOffice) is reachable." : $"Gotenberg health returned {(int)response.StatusCode}."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gotenberg health check failed");
            return new DocumentRenderingStatusDto
            {
                Enabled = true,
                GotenbergConfigured = true,
                GotenbergReachable = false,
                GotenbergBaseUrl = _settings.GotenbergBaseUrl,
                Message = ex.Message
            };
        }
    }

    public byte[] MergePlaceholders(byte[] docxBytes, IReadOnlyDictionary<string, string> values) =>
        DocxPlaceholderMerger.Merge(docxBytes, values);

    public async Task<byte[]> ConvertDocxToPdfAsync(byte[] docxBytes, CancellationToken ct = default)
    {
        EnsureEnabled();
        using var content = BuildDocxMultipart(docxBytes);
        var client = CreateClient();
        using var response = await client.PostAsync("/forms/libreoffice/convert", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Gotenberg conversion failed ({(int)response.StatusCode}): {body}");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<byte[]> MergeAndConvertToPdfAsync(
        byte[] docxBytes,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default)
    {
        var merged = MergePlaceholders(docxBytes, values);
        return await ConvertDocxToPdfAsync(merged, ct);
    }

    private void EnsureEnabled()
    {
        if (!_settings.Enabled)
            throw new InvalidOperationException("Document rendering is disabled.");

        if (string.IsNullOrWhiteSpace(_settings.GotenbergBaseUrl))
            throw new InvalidOperationException("GotenbergBaseUrl is not configured.");
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("Gotenberg");
        return client;
    }

    private static MultipartFormDataContent BuildDocxMultipart(byte[] docxBytes)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(docxBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "files", "document.docx");
        return content;
    }
}
