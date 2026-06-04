namespace MngEngine.Application.Features.SecEvents;

/// <summary>Reactor POST /api/v1/ingest/sec-events ile uyumlu batch modeli.</summary>
public record SecEventIngestRequest
{
    public required List<SecEventIngestItem> Items { get; init; }
}

public record SecEventIngestItem
{
    public required DateTime ReceivedAt { get; init; }
    public SecEventIngestSource? Source { get; init; }
    public required object Raw { get; init; }
}

public record SecEventIngestSource
{
    public string? Type { get; init; }
    public string? Product { get; init; }
    public string? Host { get; init; }
}

public record SecEventIngestResult
{
    public bool Success { get; init; }
    public int Accepted { get; init; }
    public int Rejected { get; init; }
    public int Published { get; init; }
    public string? ErrorMessage { get; init; }
}
