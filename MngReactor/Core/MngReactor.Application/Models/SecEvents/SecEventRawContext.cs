using System.Text.Json;
using MngReactor.Application.Features.Commands.Ingest;

namespace MngReactor.Application.Models.SecEvents;

/// <summary>Parser girdisi — source + ham payload + alım zamanı.</summary>
public sealed class SecEventRawContext
{
    public required DateTime ReceivedAt { get; init; }
    public required SecEventSourceInfo Source { get; init; }
    public required JsonElement Raw { get; init; }

    public static SecEventRawContext From(SecEventIngestItem item) =>
        new()
        {
            ReceivedAt = item.ReceivedAt,
            Source = new SecEventSourceInfo
            {
                Type = item.Source?.Type,
                Product = item.Source?.Product,
                Host = item.Source?.Host
            },
            Raw = item.Raw
        };
}
