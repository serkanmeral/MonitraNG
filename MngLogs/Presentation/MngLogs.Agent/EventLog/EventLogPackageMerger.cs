using MngLogs.Agent.Configuration;

namespace MngLogs.Agent.EventLog;

/// <summary>
/// Merges server package catalog with agent-local overrides.
/// Override by name replaces channel/eventIds; new names are added; disabled names drop server packages.
/// </summary>
public static class EventLogPackageMerger
{
    public static IReadOnlyList<EventLogPackage> Merge(
        IEnumerable<EventLogPackage> serverBase,
        IEnumerable<EventLogPackage>? agentOverrides,
        IEnumerable<string>? disabledServerPackages)
    {
        var disabled = new HashSet<string>(
            (disabledServerPackages ?? []).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var map = new Dictionary<string, EventLogPackage>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in serverBase ?? [])
        {
            if (!IsValid(p))
                continue;
            if (disabled.Contains(p.Name))
                continue;
            map[p.Name.Trim()] = Clone(p);
        }

        foreach (var o in agentOverrides ?? [])
        {
            if (!IsValid(o))
                continue;
            map[o.Name.Trim()] = Clone(o);
        }

        return map.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsValid(EventLogPackage? p) =>
        p is not null &&
        !string.IsNullOrWhiteSpace(p.Name) &&
        !string.IsNullOrWhiteSpace(p.Channel) &&
        p.EventIds is { Count: > 0 };

    public static EventLogPackage Clone(EventLogPackage p) => new()
    {
        Name = p.Name.Trim(),
        Channel = p.Channel.Trim(),
        EventIds = p.EventIds.Distinct().OrderBy(x => x).ToList()
    };
}
