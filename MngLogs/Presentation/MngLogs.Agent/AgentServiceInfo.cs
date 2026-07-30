namespace MngLogs.Agent;

/// <summary>SCM registration constants (must match install scripts / future MSI).</summary>
public static class AgentServiceInfo
{
    /// <summary>Windows service name (no spaces; GPO/MSI stable id).</summary>
    public const string ServiceName = "MngLogsAgent";

    /// <summary>Services MMC display name.</summary>
    public const string DisplayName = "MngLogs Agent";

    public const string DefaultInstallDir = @"C:\Program Files\MngLogs\Agent";
}
