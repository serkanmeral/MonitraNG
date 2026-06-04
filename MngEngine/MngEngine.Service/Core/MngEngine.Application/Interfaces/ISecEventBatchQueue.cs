namespace MngEngine.Application.Interfaces;

using MngEngine.Application.Features.SecEvents;

/// <summary>Syslog ve fixture kaynaklarından gelen sec-event öğeleri için in-memory kuyruk.</summary>
public interface ISecEventBatchQueue
{
    void Enqueue(SecEventIngestItem item);

    IReadOnlyList<SecEventIngestItem> DequeueAll();

    int Count { get; }

    IReadOnlyList<SecEventIngestItem> PeekAll();
}
