using System.Diagnostics;

namespace MngLogs.Agent.ServiceWatch;

public static class ApplicationWatchProbe
{
    public static string NormalizeProcessName(string name)
    {
        var n = (name ?? string.Empty).Trim();
        if (n.Length == 0)
            return string.Empty;

        // Accidental full path in the name field must not become a distinct watch key.
        if (n.Contains('\\') || n.Contains('/'))
        {
            try
            {
                n = Path.GetFileNameWithoutExtension(n);
            }
            catch
            {
                n = n.TrimEnd('\\', '/');
                var slash = Math.Max(n.LastIndexOf('\\'), n.LastIndexOf('/'));
                if (slash >= 0 && slash < n.Length - 1)
                    n = n[(slash + 1)..];
                if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    n = n[..^4];
            }
        }
        else if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            n = n[..^4];
        }

        return n.Trim();
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
                UseShellExecute = OperatingSystem.IsWindows()
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
