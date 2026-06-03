using System.Text.Json.Nodes;
using MngReactor.Application.Abstractions.Ingest;

namespace MngReactor.Tests.Helpers;

/// <summary>
/// Integration testlerinde gercek MongoDB olmadan Ingest controller'ini calistirmak icin mock.
/// </summary>
public class MockMonMetricsRepository : IMonMetricsRepository
{
    public Task<int> InsertManyAsync(string domain, IReadOnlyList<JsonObject> documents, CancellationToken ct = default)
        => Task.FromResult(documents?.Count ?? 0);
}
