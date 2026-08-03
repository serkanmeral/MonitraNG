using MngLogs.Agent.Contracts;
using MngLogs.Agent.Runtime;

namespace MngLogs.Agent.Metrics;

public interface IHostMetricsCollector
{
    IReadOnlyList<IngestEventItem> Collect(bool includeHostResources);

    TopProcessSnapshot CollectTopProcesses(int take);

    IReadOnlyList<IngestEventItem> ToTopProcessEvents(TopProcessSnapshot snapshot);

    HostInventorySnapshot CaptureInventory();
}

/// <summary>Local UI Kestrel bind (from system.json).</summary>
public readonly record struct LocalUiBindInfo(int Port, string Host);
