using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Application.Abstractions.SecEvents;

public interface ISecEventsRepository
{
    Task<int> InsertManyAsync(
        string domain,
        IReadOnlyList<SecEventDocument> docs,
        CancellationToken cancellationToken = default);
}
