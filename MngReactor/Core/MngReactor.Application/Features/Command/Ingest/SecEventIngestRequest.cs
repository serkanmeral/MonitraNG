using System.Text.Json;

namespace MngReactor.Application.Features.Commands.Ingest;

/// <summary>Engine → Reactor sec_events batch (Faz 1: ayrı route /ingest/sec-events).</summary>
public record SecEventIngestRequest
{
    public required List<SecEventIngestItem> Items { get; init; }
}

public record SecEventIngestItem
{
    public required DateTime ReceivedAt { get; init; }
    public SecEventIngestSource? Source { get; init; }
    public required JsonElement Raw { get; init; }
}

public record SecEventIngestSource
{
    public string? Type { get; init; }
    public string? Product { get; init; }
    public string? Host { get; init; }
}
