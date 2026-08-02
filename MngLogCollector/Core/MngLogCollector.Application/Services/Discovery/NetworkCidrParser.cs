using System.Net;
using System.Net.Sockets;

namespace MngLogCollector.Application.Services.Discovery;

public static class NetworkCidrParser
{
    public const int MaxHosts = 256;

    /// <summary>
    /// Parses CIDR (a.b.c.0/24) or dash ranges (a.b.c.1-254 / a.b.c.1-a.b.c.254).
    /// Returns host addresses only (network/broadcast excluded for /24+).
    /// </summary>
    public static bool TryParse(string input, out IReadOnlyList<IPAddress> hosts, out string? error)
    {
        hosts = [];
        error = null;
        var raw = NormalizeInput(input);
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "cidr is required.";
            return false;
        }

        try
        {
            if (raw.Contains('/'))
                return TryParseCidr(raw, out hosts, out error);

            if (raw.Contains('-'))
                return TryParseDashRange(raw, out hosts, out error);

            if (IPAddress.TryParse(raw, out var single) && single.AddressFamily == AddressFamily.InterNetwork)
            {
                hosts = [single];
                return true;
            }

            error = $"Unsupported range format ({Preview(raw)}). Use CIDR (192.168.20.0/24) or 192.168.20.1-254.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Trim, collapse spaces, map unicode slash/dash to ASCII.</summary>
    private static string NormalizeInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "";

        var s = input.Trim();
        // Unicode fraction slash / fullwidth solidus → /
        s = s.Replace('\u2044', '/').Replace('\u2215', '/').Replace('\uFF0F', '/');
        // en/em dash → hyphen for ranges
        s = s.Replace('\u2013', '-').Replace('\u2014', '-').Replace('\u2212', '-');
        // Remove spaces around separators: "192.168.20.0 / 24" → "192.168.20.0/24"
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s*/\s*", "/");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s*-\s*", "-");
        return s;
    }

    private static string Preview(string raw)
    {
        var p = raw.Length > 64 ? raw[..64] + "…" : raw;
        return $"'{p}'";
    }

    private static bool TryParseCidr(string raw, out IReadOnlyList<IPAddress> hosts, out string? error)
    {
        hosts = [];
        error = null;
        var parts = raw.Split('/', 2);
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0].Trim(), out var network)
            || network.AddressFamily != AddressFamily.InterNetwork
            || !int.TryParse(parts[1].Trim(), out var prefix)
            || prefix < 0
            || prefix > 32)
        {
            error = $"Invalid CIDR ({Preview(raw)}). Use e.g. 192.168.20.0/24.";
            return false;
        }

        if (prefix < 24)
        {
            error = $"Prefix /{prefix} too wide. D0 allows at most /24 ({MaxHosts} hosts).";
            return false;
        }

        var networkUint = ToUInt32(network);
        var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        var networkBase = networkUint & mask;
        var broadcast = networkBase | ~mask;
        var list = new List<IPAddress>();

        if (prefix == 32)
        {
            list.Add(FromUInt32(networkBase));
        }
        else if (prefix == 31)
        {
            list.Add(FromUInt32(networkBase));
            list.Add(FromUInt32(broadcast));
        }
        else
        {
            for (var ip = networkBase + 1; ip < broadcast; ip++)
                list.Add(FromUInt32(ip));
        }

        if (list.Count > MaxHosts)
        {
            error = $"Range expands to {list.Count} hosts; max is {MaxHosts}.";
            return false;
        }

        if (list.Count == 0)
        {
            error = "CIDR produced no host addresses.";
            return false;
        }

        hosts = list;
        return true;
    }

    private static bool TryParseDashRange(string raw, out IReadOnlyList<IPAddress> hosts, out string? error)
    {
        hosts = [];
        error = null;
        var idx = raw.LastIndexOf('-');
        if (idx <= 0 || idx >= raw.Length - 1)
        {
            error = "Invalid dash range.";
            return false;
        }

        var left = raw[..idx].Trim();
        var right = raw[(idx + 1)..].Trim();

        if (!IPAddress.TryParse(left, out var start) || start.AddressFamily != AddressFamily.InterNetwork)
        {
            error = "Invalid start IP.";
            return false;
        }

        uint endUint;
        // Prefer last-octet form (1-254) before IPAddress.TryParse — "40" parses as 0.0.0.40.
        if (byte.TryParse(right, out var lastOctet) && !right.Contains('.'))
        {
            var bytes = start.GetAddressBytes();
            bytes[3] = lastOctet;
            endUint = ToUInt32(new IPAddress(bytes));
        }
        else if (right.Contains('.')
                 && IPAddress.TryParse(right, out var endIp)
                 && endIp.AddressFamily == AddressFamily.InterNetwork)
        {
            endUint = ToUInt32(endIp);
        }
        else
        {
            error = "Invalid end of range (use last octet or full IP).";
            return false;
        }

        var startUint = ToUInt32(start);
        if (endUint < startUint)
        {
            error = "Range end is before start.";
            return false;
        }

        var count = (long)endUint - startUint + 1;
        if (count > MaxHosts)
        {
            error = $"Range expands to {count} hosts; max is {MaxHosts}.";
            return false;
        }

        var list = new List<IPAddress>((int)count);
        for (var ip = startUint; ip <= endUint; ip++)
            list.Add(FromUInt32(ip));

        hosts = list;
        return true;
    }

    private static uint ToUInt32(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        if (BitConverter.IsLittleEndian)
            Array.Reverse(b);
        return BitConverter.ToUInt32(b, 0);
    }

    private static IPAddress FromUInt32(uint value)
    {
        var b = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(b);
        return new IPAddress(b);
    }
}
