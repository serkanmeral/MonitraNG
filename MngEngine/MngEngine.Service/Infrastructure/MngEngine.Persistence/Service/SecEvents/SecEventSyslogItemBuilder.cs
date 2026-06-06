using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MngEngine.Application.Features.SecEvents;
using MngEngine.Persistence.Options;

namespace MngEngine.Persistence.Service.SecEvents;

public sealed partial class SecEventSyslogItemBuilder
{
    public const string NxlogProductValue = "windows-nxlog";

    private static readonly Regex IsoTimestampHostRegex = IsoTimestampHostPattern();
    private static readonly Regex NxlogHostnameRegex = NxlogHostnamePattern();

    private readonly SecEventQueueOptions _options;

    public SecEventSyslogItemBuilder(IOptions<SecEventQueueOptions> options) =>
        _options = options.Value;

    public SecEventIngestItem FromSyslog(
        string rawMessage,
        IPEndPoint? remoteEndpoint,
        DateTime receivedAt,
        SecEventSyslogListenerOptions? listener = null)
    {
        var raw = rawMessage ?? string.Empty;
        if (_options.MaxMessageBytes > 0 && raw.Length > _options.MaxMessageBytes)
            raw = raw[.._options.MaxMessageBytes];

        var host = ExtractHost(raw)
                   ?? _options.DefaultSourceHost
                   ?? remoteEndpoint?.Address.ToString()
                   ?? "unknown";

        var (sourceType, sourceProduct) = ClassifySource(raw, _options, listener);

        return new SecEventIngestItem
        {
            ReceivedAt = receivedAt,
            Source = new SecEventIngestSource
            {
                Type = sourceType,
                Product = sourceProduct,
                Host = host
            },
            Raw = raw
        };
    }

    internal static (string Type, string Product) ClassifySource(
        string raw,
        SecEventQueueOptions options,
        SecEventSyslogListenerOptions? listener = null)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ResolveListenerOrDefault(options, listener);

        if (raw.Contains("sshd[", StringComparison.Ordinal)
            || raw.Contains("sshd-session[", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("sudo:", StringComparison.Ordinal))
            return ("endpoint", "linux-syslog");

        if (LooksLikeNxlogJson(raw))
            return ("ad", NxlogProductValue);

        if (listener != null
            && !string.IsNullOrWhiteSpace(listener.SourceType)
            && !string.IsNullOrWhiteSpace(listener.SourceProduct))
            return (listener.SourceType.Trim(), listener.SourceProduct.Trim());

        return ResolveListenerOrDefault(options, listener);
    }

    internal static bool LooksLikeNxlogJson(string raw) =>
        !string.IsNullOrWhiteSpace(raw)
        && raw.TrimStart().StartsWith('{')
        && raw.Contains("\"EventID\"", StringComparison.Ordinal)
        && raw.Contains("\"Hostname\"", StringComparison.Ordinal);

    private static (string Type, string Product) ResolveListenerOrDefault(
        SecEventQueueOptions options,
        SecEventSyslogListenerOptions? listener)
    {
        if (listener != null
            && !string.IsNullOrWhiteSpace(listener.SourceType)
            && !string.IsNullOrWhiteSpace(listener.SourceProduct))
            return (listener.SourceType.Trim(), listener.SourceProduct.Trim());

        return (options.DefaultSourceType, options.DefaultSourceProduct);
    }

    internal static string? ExtractHost(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (raw.TrimStart().StartsWith('{'))
        {
            var nxlogHost = ExtractNxlogHostname(raw);
            if (!string.IsNullOrWhiteSpace(nxlogHost))
                return nxlogHost;
        }

        var match = IsoTimestampHostRegex.Match(raw);
        if (match.Success)
            return match.Groups["host"].Value;

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0].Contains('-', StringComparison.Ordinal))
            return parts[1];

        if (parts.Length >= 4 && parts[2].Contains(':', StringComparison.Ordinal))
            return parts[3];

        return null;
    }

    private static string? ExtractNxlogHostname(string raw)
    {
        var match = NxlogHostnameRegex.Match(raw);
        return match.Success ? match.Groups["host"].Value : null;
    }

    [GeneratedRegex(@"""Hostname""\s*:\s*""(?<host>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex NxlogHostnamePattern();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\S+\s+(?<host>\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex IsoTimestampHostPattern();
}
