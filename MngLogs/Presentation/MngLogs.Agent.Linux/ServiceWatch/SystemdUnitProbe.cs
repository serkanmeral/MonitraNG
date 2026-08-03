using System.Diagnostics;
using System.Text;
using MngLogs.Agent.ServiceWatch;

namespace MngLogs.Agent.Linux.ServiceWatch;

/// <summary>Probe/restart systemd units via systemctl.</summary>
public static class SystemdUnitProbe
{
    public static string NormalizeUnitName(string name)
    {
        var n = (name ?? string.Empty).Trim();
        if (n.Length == 0)
            return string.Empty;

        if (!n.Contains('.', StringComparison.Ordinal) &&
            !n.EndsWith(".service", StringComparison.OrdinalIgnoreCase) &&
            !n.EndsWith(".socket", StringComparison.OrdinalIgnoreCase) &&
            !n.EndsWith(".timer", StringComparison.OrdinalIgnoreCase))
        {
            n += ".service";
        }

        return n;
    }

    public static (ServiceWatchHealth Health, string StatusText, string? Description) Probe(string unitName)
    {
        var unit = NormalizeUnitName(unitName);
        if (string.IsNullOrWhiteSpace(unit))
            return (ServiceWatchHealth.Unknown, "empty", null);

        var show = RunSystemctl(
            $"show {Escape(unit)} -p LoadState -p ActiveState -p SubState -p Description --no-pager",
            TimeSpan.FromSeconds(8));

        if (show.ExitCode != 0 && string.IsNullOrWhiteSpace(show.Stdout))
            return (ServiceWatchHealth.Unknown, $"systemctl exit {show.ExitCode}", null);

        var map = ParseShow(show.Stdout);
        map.TryGetValue("LoadState", out var load);
        map.TryGetValue("ActiveState", out var active);
        map.TryGetValue("SubState", out var sub);
        map.TryGetValue("Description", out var description);

        if (string.Equals(load, "not-found", StringComparison.OrdinalIgnoreCase))
            return (ServiceWatchHealth.Missing, "not-found", description);

        var statusText = string.IsNullOrWhiteSpace(sub)
            ? (active ?? "unknown")
            : $"{active}/{sub}";

        var health = active?.ToLowerInvariant() switch
        {
            "active" => ServiceWatchHealth.Running,
            "activating" => ServiceWatchHealth.Running,
            "reloading" => ServiceWatchHealth.Running,
            "inactive" => ServiceWatchHealth.NotRunning,
            "failed" => ServiceWatchHealth.NotRunning,
            "deactivating" => ServiceWatchHealth.NotRunning,
            null or "" => ServiceWatchHealth.Unknown,
            _ => ServiceWatchHealth.Unknown
        };

        return (health, statusText, description);
    }

    public static bool TryRestart(string unitName, out string? error)
    {
        error = null;
        var unit = NormalizeUnitName(unitName);
        if (string.IsNullOrWhiteSpace(unit))
        {
            error = "empty unit name";
            return false;
        }

        var result = RunSystemctl($"restart {Escape(unit)}", TimeSpan.FromSeconds(45));
        if (result.ExitCode == 0)
            return true;

        error = string.IsNullOrWhiteSpace(result.Stderr)
            ? $"systemctl restart exit {result.ExitCode}"
            : result.Stderr.Trim();
        return false;
    }

    private static Dictionary<string, string> ParseShow(string stdout)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            map[line[..eq]] = line[(eq + 1)..];
        }

        return map;
    }

    private static string Escape(string unit) =>
        $"\"{unit.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static (int ExitCode, string Stdout, string Stderr) RunSystemctl(string args, TimeSpan timeout)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/systemctl",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            if (!process.Start())
                return (-1, "", "failed to start systemctl");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return (-1, stdout.ToString(), "systemctl timeout");
            }

            process.WaitForExit();
            return (process.ExitCode, stdout.ToString(), stderr.ToString());
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }
}
