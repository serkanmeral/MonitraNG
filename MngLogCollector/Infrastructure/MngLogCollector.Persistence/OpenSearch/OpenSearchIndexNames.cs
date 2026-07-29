using System.Globalization;
using System.Text.RegularExpressions;

namespace MngLogCollector.Persistence.OpenSearch;

public static class OpenSearchIndexNames
{
    private static readonly Regex UnsafeIndexChars = new("[^a-z0-9-]+", RegexOptions.Compiled);

    public static string SanitizeDomain(string domain)
    {
        var d = (domain ?? "unknown").Trim().ToLowerInvariant();
        d = UnsafeIndexChars.Replace(d, "-").Trim('-');
        return string.IsNullOrEmpty(d) ? "unknown" : d;
    }

    public static string BuildDailySecEventsIndexName(string domain, DateTime ingestedAtUtc)
    {
        var safeDomain = SanitizeDomain(domain);
        var day = ingestedAtUtc.Kind == DateTimeKind.Utc
            ? ingestedAtUtc
            : ingestedAtUtc.ToUniversalTime();
        return $"mng-{safeDomain}-sec-events-{day.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)}";
    }
}
