using MngEngine.Application.Collector.Common;
using MngEngine.Application.Features.Ingest;

namespace MngEngine.Application.Collector.SnmpHost;

/// <summary>
/// SNMP toplama yanıtı. Toplanan metrikleri içerir.
/// </summary>
public record SnmpCollectorResponse : BaseCollectorResponse
{
    public List<IngestMetric> Metrics { get; init; } = [];
}
