using System.Globalization;

namespace MngNotifier.Application.Utilities;

public static class PlaceholderFormatting
{
    private static readonly HashSet<string> DateFieldSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "timestamp",
        "createdAt",
        "updatedAt",
        "date",
        "occurredAt",
        "completedAt",
        "startedAt",
    };

    public static (string Path, string? Format) ParsePlaceholderExpression(string expression)
    {
        var trimmed = expression.Trim();
        var pipeIndex = trimmed.IndexOf('|');
        if (pipeIndex < 0)
            return (trimmed, null);

        var path = trimmed[..pipeIndex].Trim();
        var format = trimmed[(pipeIndex + 1)..].Trim();
        return (path, string.IsNullOrWhiteSpace(format) ? null : format);
    }

    public static bool IsDateFieldPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var last = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        return last != null && DateFieldSuffixes.Contains(last);
    }

    public static string FormatValue(string? rawValue, string path, string? formatHint, string? locale)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return string.Empty;

        var shouldFormat = formatHint != null || IsDateFieldPath(path);
        if (!shouldFormat)
            return rawValue;

        if (!TryParseDateTime(rawValue, out var utc))
            return rawValue;

        var culture = ResolveCulture(locale);
        var local = ToDisplayTimeZone(utc, locale);
        var format = ResolveFormat(formatHint, locale);

        return format switch
        {
            "short" => local.ToString(GetDefaultDateTimeFormat(locale), culture),
            "date" => local.ToString(GetDefaultDateFormat(locale), culture),
            "time" => local.ToString(GetDefaultTimeFormat(locale), culture),
            _ => local.ToString(format, culture),
        };
    }

    private static string ResolveFormat(string? formatHint, string? locale)
    {
        if (string.IsNullOrWhiteSpace(formatHint))
            return "short";

        return formatHint.Trim().ToLowerInvariant() switch
        {
            "short" or "date" or "time" => formatHint.Trim().ToLowerInvariant(),
            _ => formatHint.Trim(),
        };
    }

    private static CultureInfo ResolveCulture(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return CultureInfo.GetCultureInfo("tr-TR");

        try
        {
            return CultureInfo.GetCultureInfo(locale.Trim());
        }
        catch (CultureNotFoundException)
        {
            return locale.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                ? CultureInfo.GetCultureInfo("en-US")
                : CultureInfo.GetCultureInfo("tr-TR");
        }
    }

    private static string GetDefaultDateTimeFormat(string? locale) =>
        locale?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true
            ? "MMM d, yyyy h:mm tt"
            : "dd.MM.yyyy HH:mm";

    private static string GetDefaultDateFormat(string? locale) =>
        locale?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true ? "MMM d, yyyy" : "dd.MM.yyyy";

    private static string GetDefaultTimeFormat(string? locale) =>
        locale?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true ? "h:mm tt" : "HH:mm";

    private static bool TryParseDateTime(string raw, out DateTime utc)
    {
        utc = default;

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            utc = dto.UtcDateTime;
            return true;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var inv))
        {
            utc = inv.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(inv, DateTimeKind.Utc) : inv.ToUniversalTime();
            return true;
        }

        var tr = CultureInfo.GetCultureInfo("tr-TR");
        if (DateTime.TryParse(raw, tr, DateTimeStyles.AssumeLocal, out var trDt))
        {
            utc = trDt.ToUniversalTime();
            return true;
        }

        return false;
    }

    private static DateTime ToDisplayTimeZone(DateTime utc, string? locale)
    {
        var normalized = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var dto = new DateTimeOffset(normalized, TimeSpan.Zero);
        var tz = ResolveDisplayTimeZone(locale);
        return TimeZoneInfo.ConvertTime(dto, tz).DateTime;
    }

    private static TimeZoneInfo ResolveDisplayTimeZone(string? locale)
    {
        if (locale?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true)
            return TimeZoneInfo.Utc;

        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
