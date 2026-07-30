using System.Diagnostics;

namespace MngLogs.Agent.ServiceWatch;

public static class ApplicationWatchProbe
{
    public static string NormalizeProcessName(string name)
    {
        var n = (name ?? string.Empty).Trim();
        if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            n = n[..^4];
        return n;
    }

    /// <summary>Counts processes whose ProcessName matches (case-insensitive, .exe optional).</summary>
    public static int CountInstances(string matchName)
    {
        var needle = NormalizeProcessName(matchName);
        if (string.IsNullOrWhiteSpace(needle))
            return 0;

        var count = 0;
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (string.Equals(NormalizeProcessName(process.ProcessName), needle, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            catch
            {
                // access denied / exited
            }
            finally
            {
                process.Dispose();
            }
        }

        return count;
    }

    /// <summary>Starts an application executable. Returns false when path missing or start fails.</summary>
    public static bool TryStart(
        string? executablePath,
        string? arguments,
        string? workingDirectory,
        out string? error,
        out int? pid)
    {
        error = null;
        pid = null;

        var path = (executablePath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "executablePath is required when restartAllowed";
            return false;
        }

        if (!File.Exists(path))
        {
            error = $"Executable not found: {path}";
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(arguments))
                psi.Arguments = arguments.Trim();
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                var wd = workingDirectory.Trim();
                if (!Directory.Exists(wd))
                {
                    error = $"Working directory not found: {wd}";
                    return false;
                }
                psi.WorkingDirectory = wd;
            }

            var process = Process.Start(psi);
            if (process is null)
            {
                error = "Process.Start returned null";
                return false;
            }

            pid = process.Id;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
