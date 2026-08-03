using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;

namespace MngLogs.Agent.Linux.Journal;

/// <summary>Reads journald via journalctl JSON (--since watermark; no historical catch-up).</summary>
public sealed class JournalctlReader
{
    public IReadOnlyList<IngestEventItem> ReadNew(
        JournalPackage package,
        JournalBookmark? bookmark,
        int maxEvents,
        out JournalBookmark? updatedBookmark)
    {
        updatedBookmark = bookmark;
        maxEvents = Math.Clamp(maxEvents, 1, 200);

        // First run: seed watermark to now, do not replay history.
        if (bookmark?.LastEventUtc is null && bookmark?.SeededAtUtc is null)
        {
            var now = DateTime.UtcNow;
            updatedBookmark = new JournalBookmark(Cursor: null, SeededAtUtc: now, LastEventUtc: now);
            return [];
        }

        var since = bookmark!.LastEventUtc ?? bookmark.SeededAtUtc ?? DateTime.UtcNow;
        // Small overlap; dedupe by cursor/id below.
        var sinceArg = since.AddSeconds(-1).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";

        var args = BuildArgs(package, sinceArg, maxEvents);
        var (exit, stdout, stderr) = RunJournalctl(args, TimeSpan.FromSeconds(20));
        if (exit != 0 && string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(stderr) ? $"journalctl exit {exit}" : stderr.Trim());

        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(bookmark.Cursor))
            seen.Add(bookmark.Cursor);

        var items = new List<IngestEventItem>();
        string? lastCursor = bookmark.Cursor;
        var lastUtc = since;

        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith('{'))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var cursor = GetString(root, "__CURSOR");
                if (!string.IsNullOrWhiteSpace(cursor))
                {
                    if (!seen.Add(cursor))
                        continue;
                    lastCursor = cursor;
                }

                var item = ToEvent(package, root);
                if (item.TimestampUtc is { } ts && ts <= since)
                    continue;

                items.Add(item);
                if (item.TimestampUtc is { } ts2 && ts2 > lastUtc)
                    lastUtc = ts2;
            }
            catch
            {
                // skip bad line
            }
        }

        updatedBookmark = new JournalBookmark(lastCursor, bookmark.SeededAtUtc ?? DateTime.UtcNow, lastUtc);
        return items;
    }

    private static List<string> BuildArgs(JournalPackage package, string sinceArg, int n)
    {
        var args = new List<string>
        {
            "-o", "json",
            "--no-pager",
            "-n", n.ToString(CultureInfo.InvariantCulture),
            "--since", sinceArg
        };

        if (!string.IsNullOrWhiteSpace(package.Unit))
        {
            args.Add("-u");
            args.Add(package.Unit.Trim());
        }

        if (!string.IsNullOrWhiteSpace(package.Identifier))
            args.Add($"SYSLOG_IDENTIFIER={package.Identifier.Trim()}");

        if (!string.IsNullOrWhiteSpace(package.Priority))
        {
            args.Add("-p");
            args.Add(package.Priority.Trim());
        }

        if (!string.IsNullOrWhiteSpace(package.Grep))
        {
            args.Add("-g");
            args.Add(package.Grep.Trim());
        }

        return args;
    }

    private static IngestEventItem ToEvent(JournalPackage package, JsonElement root)
    {
        var message = GetString(root, "MESSAGE") ?? "";
        var unit = GetString(root, "_SYSTEMD_UNIT") ?? GetString(root, "UNIT") ?? package.Unit;
        var identifier = GetString(root, "SYSLOG_IDENTIFIER") ?? package.Identifier;
        var priority = GetString(root, "PRIORITY");
        var cursor = GetString(root, "__CURSOR");
        var ts = ParseRealtime(root);
        var action = InferAction(package.Name, message);

        return new IngestEventItem
        {
            Id = cursor ?? Guid.NewGuid().ToString("N"),
            TimestampUtc = ts,
            Source = "linux-journal",
            SourceProduct = "mnglogs-agent",
            Severity = MapSeverity(priority, action),
            Message = string.IsNullOrWhiteSpace(message) ? action : Truncate(message, 2000),
            Raw = root.Clone(),
            Fields = new Dictionary<string, object?>
            {
                ["event.action"] = action,
                ["package"] = package.Name,
                ["channel"] = unit ?? identifier ?? "journal",
                ["unit"] = unit,
                ["identifier"] = identifier,
                ["priority"] = priority,
                ["cursor"] = cursor,
                ["hostname"] = GetString(root, "_HOSTNAME"),
                ["pid"] = GetString(root, "_PID"),
                ["comm"] = GetString(root, "_COMM"),
                ["platform"] = "linux",
                ["machine"] = Environment.MachineName
            }
        };
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string InferAction(string package, string message)
    {
        if (string.Equals(package, "sshd", StringComparison.OrdinalIgnoreCase))
        {
            if (message.Contains("Failed password", StringComparison.OrdinalIgnoreCase))
                return "ssh.login_failed";
            if (message.Contains("Accepted password", StringComparison.OrdinalIgnoreCase))
                return "ssh.login_success";
            return "ssh.event";
        }

        if (string.Equals(package, "sudo", StringComparison.OrdinalIgnoreCase))
            return "sudo.event";

        if (string.Equals(package, "unit-fail", StringComparison.OrdinalIgnoreCase))
            return "unit.failed";

        return "journal.event";
    }

    private static string MapSeverity(string? priority, string action)
    {
        if (action.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("fail", StringComparison.OrdinalIgnoreCase))
            return "warning";

        if (int.TryParse(priority, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p))
        {
            if (p <= 3) return "error";
            if (p == 4) return "warning";
        }

        return "info";
    }

    private static DateTime ParseRealtime(JsonElement root)
    {
        if (root.TryGetProperty("__REALTIME_TIMESTAMP", out var ts))
        {
            var s = ts.ValueKind switch
            {
                JsonValueKind.String => ts.GetString(),
                JsonValueKind.Number => ts.GetRawText(),
                _ => null
            };
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var us) && us > 0)
                return DateTimeOffset.FromUnixTimeMilliseconds(us / 1000).UtcDateTime;
        }

        return DateTime.UtcNow;
    }

    private static string? GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static (int ExitCode, string Stdout, string Stderr) RunJournalctl(List<string> args, TimeSpan timeout)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/journalctl",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            foreach (var a in args)
                process.StartInfo.ArgumentList.Add(a);

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            if (!process.Start())
                return (-1, "", "failed to start journalctl");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return (-1, stdout.ToString(), "journalctl timeout");
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
