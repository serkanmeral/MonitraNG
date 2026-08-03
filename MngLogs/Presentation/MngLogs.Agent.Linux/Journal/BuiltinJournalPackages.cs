using MngLogs.Agent.Configuration;

namespace MngLogs.Agent.Linux.Journal;

public static class BuiltinJournalPackages
{
    public static IReadOnlyList<JournalPackage> Defaults { get; } =
    [
        new JournalPackage
        {
            Name = "sshd",
            Unit = "ssh.service",
            Grep = "Failed password|Accepted password",
            IsDefault = true
        },
        new JournalPackage
        {
            Name = "sudo",
            Identifier = "sudo",
            IsDefault = true
        },
        new JournalPackage
        {
            Name = "unit-fail",
            Priority = "err",
            Grep = "Failed with result|entered failed state|Unit entered failed state",
            IsDefault = true
        }
    ];

    public static IReadOnlyList<JournalPackage> Resolve(JournalPolicy policy)
    {
        var disabled = new HashSet<string>(
            policy.DisabledPackages ?? [],
            StringComparer.OrdinalIgnoreCase);

        var map = new Dictionary<string, JournalPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Defaults.Where(x => !disabled.Contains(x.Name)))
            map[p.Name] = Clone(p);

        foreach (var o in policy.Packages ?? [])
        {
            if (string.IsNullOrWhiteSpace(o.Name))
                continue;
            if (disabled.Contains(o.Name))
            {
                map.Remove(o.Name);
                continue;
            }

            map[o.Name.Trim()] = Clone(o);
        }

        return map.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static JournalPackage Clone(JournalPackage p) => new()
    {
        Name = p.Name,
        Unit = p.Unit,
        Identifier = p.Identifier,
        Grep = p.Grep,
        Priority = p.Priority,
        IsDefault = p.IsDefault
    };
}
