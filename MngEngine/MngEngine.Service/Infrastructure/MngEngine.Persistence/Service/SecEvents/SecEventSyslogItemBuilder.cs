using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MngEngine.Application.Features.SecEvents;
using MngEngine.Persistence.Options;

namespace MngEngine.Persistence.Service.SecEvents;

public sealed partial class SecEventSyslogItemBuilder
{
    private static readonly Regex IsoTimestampHostRegex = IsoTimestampHostPattern();

    private readonly SecEventQueueOptions _options;

    public SecEventSyslogItemBuilder(IOptions<SecEventQueueOptions> options) =>
        _options = options.Value;

    public SecEventIngestItem FromSyslog(string rawMessage, IPEndPoint? remoteEndpoint, DateTime receivedAt)
    {
        var raw = rawMessage ?? string.Empty;
        if (_options.MaxMessageBytes > 0 && raw.Length > _options.MaxMessageBytes)
            raw = raw[.._options.MaxMessageBytes];

        var host = ExtractHost(raw)
                   ?? _options.DefaultSourceHost
                   ?? remoteEndpoint?.Address.ToString()
                   ?? "unknown";

        return new SecEventIngestItem
        {
            ReceivedAt = receivedAt,
            Source = new SecEventIngestSource
            {
                Type = _options.DefaultSourceType,
                Product = _options.DefaultSourceProduct,
                Host = host
            },
            Raw = raw
        };
    }

    private static string? ExtractHost(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var match = IsoTimestampHostRegex.Match(raw);
        if (match.Success)
            return match.Groups["host"].Value;

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0].Contains('-', StringComparison.Ordinal))
            return parts[1];

        return null;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\S+\s+(?<host>\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex IsoTimestampHostPattern();
}
