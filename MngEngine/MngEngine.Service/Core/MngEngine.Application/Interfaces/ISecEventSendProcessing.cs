using MngEngine.Application.Features.SecEvents;

namespace MngEngine.Application.Interfaces;

/// <summary>Kuyruktaki sec-event batch'ini Reactor'a gönderir.</summary>
public interface ISecEventSendProcessing
{
    Task<SecEventIngestResult> FlushAsync(CancellationToken ct = default);
}
