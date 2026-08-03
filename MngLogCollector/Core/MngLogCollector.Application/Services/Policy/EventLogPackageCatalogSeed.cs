using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Application.Services.Policy;

/// <summary>Initial seed rows (same content as former builtin catalog).</summary>
public static class EventLogPackageCatalogSeed
{
    public const string InitialVersion = "2026-07-30.1";

    public static IReadOnlyList<EventLogPackageDocument> CreateSeedDocuments()
    {
        var now = DateTime.UtcNow;
        return
        [
            Doc("system-lifecycle", "System", true, now,
                41, 104, 6005, 6006, 7031, 7034, 7036, 7040, 7045),
            Doc("application-signals", "Application", true, now, 1000, 1001, 1026, 65002),
            Doc("powershell-engine", "Windows PowerShell", true, now, 400, 403, 600),
            Doc(
                "rdp-session",
                "Microsoft-Windows-TerminalServices-LocalSessionManager/Operational",
                true,
                now,
                21, 23, 24, 25),
            Doc("security-auth", "Security", false, now,
                4624, 4625, 4634, 4648, 4672, 4720, 4722, 4726, 4728, 4732, 4738, 4740, 5136, 5137, 5139)
        ];
    }

    private static EventLogPackageDocument Doc(
        string name,
        string channel,
        bool isDefault,
        DateTime utc,
        params int[] ids) => new()
    {
        Name = name,
        Channel = channel,
        IsDefault = isDefault,
        EventIds = [.. ids],
        CreatedAtUtc = utc,
        UpdatedAtUtc = utc
    };
}
