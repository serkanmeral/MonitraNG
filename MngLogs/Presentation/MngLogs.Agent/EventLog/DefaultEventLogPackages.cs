using MngLogs.Agent.Configuration;

namespace MngLogs.Agent.EventLog;

public static class DefaultEventLogPackages
{
    public static IReadOnlyList<EventLogPackage> All { get; } =
    [
        new EventLogPackage
        {
            Name = "security-auth",
            Channel = "Security",
            EventIds = [4624, 4625, 4634, 4648, 4672, 4720, 4726, 4740]
        },
        new EventLogPackage
        {
            Name = "system-lifecycle",
            Channel = "System",
            EventIds = [6005, 6006, 7045]
        }
    ];

    public static IReadOnlyList<EventLogPackage> Resolve(EventLogPolicy policy)
    {
        if (policy.Packages is { Count: > 0 })
            return policy.Packages.Where(p => !string.IsNullOrWhiteSpace(p.Channel) && p.EventIds.Count > 0).ToList();

        return All;
    }

    /// <summary>Builds an XPath query for EventLogQuery (EventID filter + optional RecordId lower bound).</summary>
    public static string BuildQuery(EventLogPackage package, long? afterRecordIdExclusive)
    {
        var idFilter = string.Join(" or ", package.EventIds.Distinct().Select(id => $"EventID={id}"));
        if (afterRecordIdExclusive is > 0)
            return $"*[System[({idFilter}) and (EventRecordID > {afterRecordIdExclusive.Value})]]";

        return $"*[System[({idFilter})]]";
    }
}
