using System.Globalization;
using System.Text.Json;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

internal static class SecEventParseHelpers
{
    /// <summary>
    /// NxLog EventTime is typically host-local (no offset). Assume Europe/Istanbul when unspecified.
    /// Falls back to receivedAt when parsed time is implausibly ahead of ingest (clock skew).
    /// </summary>
    public static DateTime ParseNxlogEventTimeUtc(string text, DateTime receivedAtUtc)
    {
        var received = receivedAtUtc.Kind == DateTimeKind.Utc
            ? receivedAtUtc
            : receivedAtUtc.ToUniversalTime();

        if (string.IsNullOrWhiteSpace(text)
            || !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return received;

        var utc = ConvertLocalEventTimeToUtc(local);
        if (utc > received.AddMinutes(2))
            return received;

        return utc;
    }

    private static DateTime ConvertLocalEventTimeToUtc(DateTime local)
    {
        var unspecified = local.Kind switch
        {
            DateTimeKind.Utc => local,
            DateTimeKind.Local => local.ToUniversalTime(),
            _ => DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
        };

        if (unspecified.Kind == DateTimeKind.Utc)
            return unspecified;

        foreach (var zoneId in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
                return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
            }
            catch (TimeZoneNotFoundException)
            {
                // try next id (Linux vs Windows)
            }
            catch (InvalidTimeZoneException)
            {
                // try next id
            }
        }

        return DateTime.SpecifyKind(unspecified, DateTimeKind.Local).ToUniversalTime();
    }

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

    public static string ToStoredRaw(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        var max = SecEventIngestLimits.MaxRawBytes;
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
