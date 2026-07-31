using System.Reflection;

namespace MngLogs.Agent;

/// <summary>Product version for Local UI / CLI / host.up (from csproj Version).</summary>
public static class AgentVersion
{
    /// <summary>Informational version, e.g. 1.0.1</summary>
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var asm = typeof(AgentVersion).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            // Strip possible "+gitsha" suffix from SourceLink
            var plus = info.IndexOf('+', StringComparison.Ordinal);
            return plus > 0 ? info[..plus] : info.Trim();
        }

        var ver = asm.GetName().Version;
        return ver is null ? "0.0.0" : $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }
}
