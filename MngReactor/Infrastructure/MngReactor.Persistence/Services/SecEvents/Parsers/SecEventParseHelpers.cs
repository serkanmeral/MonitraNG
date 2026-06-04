using System.Text.Json;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

internal static class SecEventParseHelpers
{
    public static string GetRawText(JsonElement raw) =>
        raw.ValueKind switch
        {
            JsonValueKind.String => raw.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => raw.GetRawText()
        };

    public static string ToRawPreview(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        var max = SecEventIngestLimits.MaxRawPreviewBytes;
        if (raw.Length <= max)
            return raw;

        return raw[..max];
    }

    public static string NormalizeProduct(string? product) =>
        string.IsNullOrWhiteSpace(product) ? string.Empty : product.Trim();

    public static string NormalizeType(string? type) =>
        string.IsNullOrWhiteSpace(type) ? string.Empty : type.Trim();

    public static string ResolveSourceType(SecEventSourceInfo source, string fallback) =>
        string.IsNullOrWhiteSpace(source.Type) ? fallback : source.Type.Trim();

    public static string ResolveSourceProduct(SecEventSourceInfo source, string fallback) =>
        string.IsNullOrWhiteSpace(source.Product) ? fallback : source.Product.Trim();
}
