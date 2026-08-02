using System.Net;
using System.Net.Sockets;

namespace MngLogCollector.Application.Services.Discovery;

public sealed class TcpPortProbeResult
{
    public required string Ip { get; init; }
    public bool Alive { get; init; }
    public IReadOnlyList<int> OpenPorts { get; init; } = [];
    /// <summary>windows | linux | unknown</summary>
    public string OsFamilyHint { get; init; } = "unknown";
}

public static class TcpPortProbe
{
    public static readonly int[] ProbePorts = [22, 80, 443, 445, 3389, 5985];
    private static readonly HashSet<int> WindowsPorts = [445, 3389, 5985];

    public static async Task<TcpPortProbeResult> ProbeAsync(
        IPAddress ip,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var open = new List<int>();
        foreach (var port in ProbePorts)
        {
            ct.ThrowIfCancellationRequested();
            if (await IsOpenAsync(ip, port, timeout, ct))
                open.Add(port);
        }

        var alive = open.Count > 0;
        return new TcpPortProbeResult
        {
            Ip = ip.ToString(),
            Alive = alive,
            OpenPorts = open,
            OsFamilyHint = alive ? Classify(open) : "unknown"
        };
    }

    public static string Classify(IReadOnlyList<int> openPorts)
    {
        var has22 = openPorts.Contains(22);
        var hasWin = openPorts.Any(p => WindowsPorts.Contains(p));
        if (hasWin && !has22) return "windows";
        if (has22 && !hasWin) return "linux";
        // Dual stack (e.g. Windows with OpenSSH): prefer classic Windows signals.
        if (hasWin && has22)
        {
            if (openPorts.Contains(445) || openPorts.Contains(3389) || openPorts.Contains(5985))
                return "windows";
            return "linux";
        }
        return "unknown";
    }

    private static async Task<bool> IsOpenAsync(IPAddress ip, int port, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ip, port, cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
