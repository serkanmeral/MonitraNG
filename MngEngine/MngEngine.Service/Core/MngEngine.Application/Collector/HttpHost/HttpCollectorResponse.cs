using MngEngine.Application.Collector.Common;
using MngEngine.Application.Features.Ingest;

namespace MngEngine.Application.Collector.HttpHost;

public record HttpCollectorResponse : BaseCollectorResponse
{
    public List<IngestMetric> Metrics { get; init; } = [];
}
