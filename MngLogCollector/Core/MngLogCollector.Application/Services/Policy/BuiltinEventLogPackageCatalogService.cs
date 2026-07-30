using MngLogCollector.Application.Abstractions.Policy;
using MngLogCollector.Application.Contracts.Policy;

namespace MngLogCollector.Application.Services.Policy;

/// <summary>
/// Built-in event-log package catalog served to field agents.
/// Later: file/DB-backed admin-editable catalog; keep response shape stable.
/// </summary>
public sealed class BuiltinEventLogPackageCatalogService : IEventLogPackageCatalogService
{
    public const string CatalogVersion = "2026-07-30.1";

    public EventLogPackageCatalogResponse GetCatalog() => new()
    {
        Version = CatalogVersion,
        Source = "collector",
        GeneratedUtc = DateTime.UtcNow,
        Packages =
        [
            Pkg("system-lifecycle", "System",
                41, 104, 6005, 6006, 7031, 7034, 7036, 7040, 7045),
            Pkg("application-signals", "Application", 1000, 1001, 1026),
            Pkg("powershell-engine", "Windows PowerShell", 400, 403, 600),
            Pkg(
                "rdp-session",
                "Microsoft-Windows-TerminalServices-LocalSessionManager/Operational",
                21, 23, 24, 25)
        ],
        OptionalPackages =
        [
            Pkg("security-auth", "Security",
                4624, 4625, 4634, 4648, 4672, 4720, 4726, 4740)
        ]
    };

    private static EventLogPackageDto Pkg(string name, string channel, params int[] eventIds) => new()
    {
        Name = name,
        Channel = channel,
        EventIds = [.. eventIds]
    };
}
