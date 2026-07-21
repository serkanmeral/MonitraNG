using MngLLM.Application.DTOs.Di;
using MngLLM.Application.Services;
using MngLLM.Domain.Exceptions;

namespace MngLLM.Infrastructure.Services.Di;

public sealed class DiExtractService : IDiExtractService
{
    private readonly IDocumentIntelligenceClient _documentClient;
    private readonly IUblEarsivFaturaMapper _ublMapper;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly ILlmEarsivFaturaExtractor _llmExtractor;
    private readonly ILlmKeeperAuthClient _keeperAuth;

    public DiExtractService(
        IDocumentIntelligenceClient documentClient,
        IUblEarsivFaturaMapper ublMapper,
        IPdfTextExtractor pdfTextExtractor,
        ILlmEarsivFaturaExtractor llmExtractor,
        ILlmKeeperAuthClient keeperAuth)
    {
        _documentClient = documentClient;
        _ublMapper = ublMapper;
        _pdfTextExtractor = pdfTextExtractor;
        _llmExtractor = llmExtractor;
        _keeperAuth = keeperAuth;
    }

    public async Task<EarsivFaturaExtractDto> ExtractAsync(
        DiExtractRequestDto request,
        string? authorizationHeader,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ResourceId))
            throw new DiExtractException("resourceId is required.", 400);

        var schema = string.IsNullOrWhiteSpace(request.Schema)
            ? "earsiv_fatura"
            : request.Schema.Trim();

        if (!string.Equals(schema, "earsiv_fatura", StringComparison.OrdinalIgnoreCase))
            throw new DiExtractException($"Unsupported schema '{schema}'. Supported: earsiv_fatura.", 400);

        var serviceToken = await _keeperAuth.GetAccessTokenAsync(cancellationToken);
        var authHeader = $"Bearer {serviceToken}";

        var content = await _documentClient.GetFileContentAsync(
            request.ResourceId.Trim(),
            authHeader,
            cancellationToken);

        var ext = (content.Extension ?? Path.GetExtension(content.Name) ?? string.Empty).TrimStart('.').ToLowerInvariant();
        var looksXml =
            ext is "xml" ||
            string.Equals(content.MimeType, "application/xml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(content.MimeType, "text/xml", StringComparison.OrdinalIgnoreCase) ||
            LooksLikeXml(content.Content);

        if (looksXml)
            return _ublMapper.Map(content.Content, content.ResourceId);

        var looksPdf =
            ext is "pdf" ||
            string.Equals(content.MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase) ||
            LooksLikePdf(content.Content);

        if (looksPdf)
        {
            var text = _pdfTextExtractor.ExtractText(content.Content);
            return await _llmExtractor.ExtractFromTextAsync(text, content.ResourceId, cancellationToken);
        }

        throw new DiExtractException(
            "Unsupported file type for earsiv_fatura. Provide UBL XML or a text-layer PDF.",
            422);
    }

    private static bool LooksLikeXml(byte[] bytes)
    {
        if (bytes.Length < 20) return false;
        var probeLen = Math.Min(bytes.Length, 256);
        var head = System.Text.Encoding.UTF8.GetString(bytes, 0, probeLen).TrimStart();
        return head.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
               || head.Contains("<Invoice", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePdf(byte[] bytes)
    {
        if (bytes.Length < 5) return false;
        return bytes[0] == (byte)'%'
               && bytes[1] == (byte)'P'
               && bytes[2] == (byte)'D'
               && bytes[3] == (byte)'F';
    }
}
