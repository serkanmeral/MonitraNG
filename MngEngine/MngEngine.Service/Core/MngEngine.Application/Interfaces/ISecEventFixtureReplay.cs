using MngEngine.Application.Features.SecEvents;

namespace MngEngine.Application.Interfaces;

/// <summary>SIEM Faz 1 spike B — fixture dosyalarından sec-events batch üretir ve Reactor'a gönderir.</summary>
public interface ISecEventFixtureReplay
{
    SecEventIngestRequest BuildFixtureRequest(DateTime? receivedAt = null);

    Task<SecEventIngestResult> ReplayFixturesAsync(CancellationToken ct = default);
}
