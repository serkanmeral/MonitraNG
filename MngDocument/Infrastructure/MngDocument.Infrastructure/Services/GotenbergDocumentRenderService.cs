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

    public async Task<byte[]> ConvertDocxToPdfAsync(byte[] docxBytes, CancellationToken ct = default) =>
        await ConvertOfficeFileToPdfAsync(docxBytes, "document.docx", ct);

    public async Task<byte[]> ConvertOfficeFileToPdfAsync(
        byte[] fileBytes,
        string fileName,
        CancellationToken ct = default)
    {
        EnsureEnabled();
        using var content = BuildOfficeMultipart(fileBytes, fileName);
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

    private static MultipartFormDataContent BuildOfficeMultipart(byte[] fileBytes, string fileName)
    {
        var safeName = string.IsNullOrWhiteSpace(fileName) ? "document.docx" : Path.GetFileName(fileName);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(ResolveMimeType(safeName));
        content.Add(fileContent, "files", safeName);
        return content;
    }

    private static string ResolveMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
    }
}
