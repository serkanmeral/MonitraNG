using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLLM.Application.Configuration;
using MngLLM.Application.DTOs.Di;
using MngLLM.Application.Services;
using MngLLM.Domain.Exceptions;
using MngLLM.Domain.Interfaces;

namespace MngLLM.Infrastructure.Services.Di;

public sealed class LlmEarsivFaturaExtractor : ILlmEarsivFaturaExtractor
{
    // CPU Ollama prompt-eval is ~0.5s/token; keep text short so extract finishes under timeout.
    private const int MaxTextChars = 2200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILLMService _llm;
    private readonly OllamaSettings _ollama;
    private readonly ILogger<LlmEarsivFaturaExtractor> _logger;

    public LlmEarsivFaturaExtractor(
        ILLMService llm,
        IOptions<MngLLMSettings> settings,
        ILogger<LlmEarsivFaturaExtractor> logger)
    {
        _llm = llm;
        _ollama = settings.Value.Ollama;
        _logger = logger;
    }

    public async Task<EarsivFaturaExtractDto> ExtractFromTextAsync(
        string pdfText,
        string? resourceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfText))
            throw new DiExtractException(
                "PDF has no extractable text layer (likely scanned). OCR is not supported in MVP.",
                422);

        var truncated = TruncateHeadTail(pdfText, MaxTextChars);
        var prompt = BuildPrompt(truncated);
        var model = string.IsNullOrWhiteSpace(_ollama.ExtractModel)
            ? _ollama.DefaultModel
            : _ollama.ExtractModel;

        string raw;
        try
        {
            raw = await _llm.GenerateJsonAsync(prompt, model, cancellationToken);
        }
        catch (LLMServiceException ex)
        {
            throw new DiExtractException($"LLM extract failed: {ex.Message}", 503, ex);
        }

        var dto = ParseJson(raw);
        dto.SchemaId = "earsiv_fatura";
        dto.SchemaVersion = 1;
        dto.Source = "llm_pdf";
        dto.ResourceId = resourceId;
        if (dto.Confidence <= 0 || dto.Confidence > 1)
            dto.Confidence = 0.7;

        if (string.IsNullOrWhiteSpace(dto.InvoiceId) && string.IsNullOrWhiteSpace(dto.Uuid))
            throw new DiExtractException("LLM extract did not return invoiceId or uuid.", 422);

        if (dto.PayableAmount <= 0 && string.IsNullOrWhiteSpace(dto.InvoiceId))
            throw new DiExtractException("LLM extract result looks incomplete.", 422);

        return dto;
    }

    private static string BuildPrompt(string invoiceText) =>
        """
        You extract fields from a Turkish e-Archive / e-Invoice PDF text.
        Return ONLY a JSON object with these properties (use null when unknown):
        {
          "profileId": string|null,
          "invoiceType": string|null,
          "invoiceId": string|null,
          "uuid": string|null,
          "issueDate": "YYYY-MM-DD"|null,
          "currency": string|null,
          "payableAmount": number|null,
          "taxExclusiveAmount": number|null,
          "supplierName": string|null,
          "supplierVkn": string|null,
          "customerName": string|null,
          "customerVkn": string|null,
          "lines": [{"lineId": string|null, "name": string|null, "quantity": number|null, "lineExtensionAmount": number|null}]|null,
          "confidence": number
        }
        Rules:
        - payableAmount is the total amount payable (ödenecek tutar / genel toplam) including tax when present.
        - supplierVkn / customerVkn are 10 or 11 digit tax ids when present.
        - invoiceType examples: SATIS, IADE.
        - profileId example: EARSIVFATURA.
        - confidence between 0 and 1.
        - Do not invent values that are not in the text.

        INVOICE TEXT:
        """ + invoiceText;

    private EarsivFaturaExtractDto ParseJson(string raw)
    {
        var json = ExtractJsonObject(raw);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var dto = new EarsivFaturaExtractDto
            {
                ProfileId = GetString(root, "profileId") ?? string.Empty,
                InvoiceType = GetString(root, "invoiceType") ?? string.Empty,
                InvoiceId = GetString(root, "invoiceId") ?? string.Empty,
                Uuid = GetString(root, "uuid") ?? string.Empty,
                IssueDate = GetString(root, "issueDate") ?? string.Empty,
                Currency = GetString(root, "currency") ?? "TRY",
                PayableAmount = GetDecimal(root, "payableAmount") ?? 0,
                TaxExclusiveAmount = GetDecimal(root, "taxExclusiveAmount"),
                SupplierName = GetString(root, "supplierName"),
                SupplierVkn = GetString(root, "supplierVkn"),
                CustomerName = GetString(root, "customerName"),
                CustomerVkn = GetString(root, "customerVkn"),
                Confidence = GetDouble(root, "confidence") ?? 0.7,
                Lines = ParseLines(root)
            };

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM JSON. Raw={Raw}", Truncate(raw, 500));
            throw new DiExtractException("LLM returned invalid JSON for earsiv_fatura.", 422, ex);
        }
    }

    private static List<EarsivFaturaLineDto>? ParseLines(JsonElement root)
    {
        if (!root.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<EarsivFaturaLineDto>();
        foreach (var item in lines.EnumerateArray())
        {
            list.Add(new EarsivFaturaLineDto
            {
                LineId = GetString(item, "lineId"),
                Name = GetString(item, "name"),
                Quantity = GetDecimal(item, "quantity"),
                LineExtensionAmount = GetDecimal(item, "lineExtensionAmount")
            });
        }

        return list.Count > 0 ? list : null;
    }

    private static string ExtractJsonObject(string raw)
    {
        var text = raw.Trim();
        var fence = Regex.Match(text, "```(?:json)?\\s*([\\s\\S]*?)```", RegexOptions.IgnoreCase);
        if (fence.Success)
            text = fence.Groups[1].Value.Trim();

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new DiExtractException("LLM response did not contain a JSON object.", 422);

        return text[start..(end + 1)];
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        var s = p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static decimal? GetDecimal(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d))
            return d;
        if (p.ValueKind == JsonValueKind.String
            && decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    private static double? GetDouble(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d))
            return d;
        if (p.ValueKind == JsonValueKind.String
            && double.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text;
        return text[..max] + "\n...[truncated]...";
    }

    /// <summary>
    /// Keep invoice header + footer (totals / tax often at end).
    /// </summary>
    private static string TruncateHeadTail(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text;

        var head = (int)(max * 0.65);
        var tail = max - head;
        return text[..head] + "\n...[truncated]...\n" + text[^tail..];
    }
}
