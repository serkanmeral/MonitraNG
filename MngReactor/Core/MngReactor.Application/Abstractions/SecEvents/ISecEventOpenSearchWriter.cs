using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Application.Abstractions.SecEvents;

/// <summary>G1 dual-write: best-effort OpenSearch index after Mongo insert.</summary>
public interface ISecEventOpenSearchWriter
{
    Task IndexManyAsync(
        string domain,
        IReadOnlyList<(string Id, SecEventDocument Document)> items,
        CancellationToken cancellationToken = default);
}
