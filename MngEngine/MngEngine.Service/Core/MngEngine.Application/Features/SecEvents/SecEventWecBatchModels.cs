using System.Text.Json;

namespace MngEngine.Application.Features.SecEvents;

/// <summary>WEC forwarder → Engine HTTP batch (WEF→WEC→Engine push yolu).</summary>
public sealed class SecEventWecBatchRequest
{
    public List<SecEventWecBatchItem> Items { get; init; } = [];

    /// <summary>true ise kuyruk eşiğine göre flush tetiklenir; false ise periyodik/threshold flush beklenir.</summary>
    public bool AutoFlush { get; init; } = true;
}

public sealed class SecEventWecBatchItem
{
    public DateTime ReceivedAt { get; init; }
    public SecEventIngestSource? Source { get; init; }
    public JsonElement Raw { get; init; }
}

public sealed class SecEventWecBatchResponse
{
    public int Enqueued { get; init; }
    public int QueueDepth { get; init; }
    public bool Flushed { get; init; }
    public int? Accepted { get; init; }
    public int? Published { get; init; }
}
