using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MngLogs.Agent.Metrics;

/// <summary>Local IPv4/IPv6 inventory for host.up enrichment.</summary>
public static class HostNetworkAddresses
{
    public static IReadOnlyList<string> Collect(bool includeIpv6 = false)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    if (nic.OperationalStatus != OperationalStatus.Up)
                        continue;
                    if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                        continue;

                    var props = nic.GetIPProperties();
                    foreach (var uni in props.UnicastAddresses)
                    {
                        var addr = uni.Address;
                        if (IPAddress.IsLoopback(addr))
                            continue;
                        if (addr.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var s = addr.ToString();
                            if (seen.Add(s))
                                result.Add(s);
                        }
                        else if (includeIpv6 &&
                                 addr.AddressFamily == AddressFamily.InterNetworkV6 &&
                                 !addr.IsIPv6LinkLocal)
                        {
                            var s = addr.ToString();
                            if (seen.Add(s))
                                result.Add(s);
                        }
                    }
                }
                catch
                {
                    // skip NIC
                }
            }
        }
        catch
        {
            // ignore
        }

        return result;
    }

    public static string? PickPrimary(IReadOnlyList<string> addresses)
    {
        if (addresses.Count == 0)
            return null;
        // Prefer RFC1918 / typical LAN first; else first IPv4-looking entry.
        foreach (var a in addresses)
        {
            if (a.StartsWith("10.", StringComparison.Ordinal) ||
                a.StartsWith("192.168.", StringComparison.Ordinal) ||
                Is172Private(a))
                return a;
        }
        return addresses[0];
    }

    private static bool Is172Private(string ip)
    {
        if (!ip.StartsWith("172.", StringComparison.Ordinal))
            return false;
        var parts = ip.Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var second))
            return false;
        return second is >= 16 and <= 31;
    }
}
