namespace MngLogs.Agent.Configuration;

/// <summary>OS-default paths when system.json leaves directories empty.</summary>
public static class PlatformPaths
{
    public const string LinuxDataDirectory = "/var/lib/mnglogs/agent";
    public const string LinuxConfigDirectory = "/etc/mnglogs/agent";

    public static string DefaultDataDirectory()
    {
        if (OperatingSystem.IsLinux())
            return LinuxDataDirectory;

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(programData))
            programData = Path.Combine(Path.GetTempPath(), "MngLogs");

        return Path.Combine(programData, "MngLogs", "Agent");
    }

    public static string DefaultConfigDirectory(string dataDirectory)
    {
        if (OperatingSystem.IsLinux())
            return LinuxConfigDirectory;

        return dataDirectory;
    }
}
