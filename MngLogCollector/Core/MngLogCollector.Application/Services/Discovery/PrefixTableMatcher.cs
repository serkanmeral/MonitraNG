using System.Net;
using System.Net.Sockets;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Application.Services.Discovery;

public sealed record DiscoveryPrefixMatch(string Cidr, string Label, string? VlanName);

/// <summary>IPv4 longest-prefix-match against a configured site/subnet table.</summary>
public static class PrefixTableMatcher
{
    public const string UnscopedLabel = "Unscoped";

    public static DiscoveryPrefixMatch? TryMatch(
        string? ip,
        IEnumerable<DiscoveryPrefixEntry>? prefixes)
    {
        if (string.IsNullOrWhiteSpace(ip)
            || !IPAddress.TryParse(ip.Trim(), out var addr)
            || addr.AddressFamily != AddressFamily.InterNetwork)
        {
            return null;
        }

        var ipBits = ToUInt32(addr);
        DiscoveryPrefixMatch? best = null;
        var bestLen = -1;

        foreach (var entry in prefixes ?? [])
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Cidr))
                continue;
            if (!TryParseCidr(entry.Cidr.Trim(), out var network, out var prefixLen))
                continue;
            if (prefixLen < bestLen)
                continue;

            var mask = prefixLen == 0 ? 0u : uint.MaxValue << (32 - prefixLen);
            if ((ipBits & mask) != (network & mask))
                continue;

            var label = string.IsNullOrWhiteSpace(entry.Label) ? entry.Cidr.Trim() : entry.Label.Trim();
            best = new DiscoveryPrefixMatch(NormalizeCidr(network, prefixLen), label, NullIfEmpty(entry.VlanName));
            bestLen = prefixLen;
        }

        return best;
    }

    public static void ApplyToHost(DiscoveryHost host, IEnumerable<DiscoveryPrefixEntry>? prefixes)
    {
        var ip = host.IpAddresses?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        var match = TryMatch(ip, prefixes);
        if (match is null)
        {
            host.SubnetCidr = null;
            host.SiteLabel = string.IsNullOrWhiteSpace(ip) ? null : UnscopedLabel;
            host.VlanName = null;
            return;
        }

        host.SubnetCidr = match.Cidr;
        host.SiteLabel = match.Label;
        host.VlanName = match.VlanName;
    }

    public static bool TryParseCidr(string cidr, out uint network, out int prefixLen)
    {
        network = 0;
        prefixLen = 0;
        var parts = cidr.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;
        if (!IPAddress.TryParse(parts[0], out var addr) || addr.AddressFamily != AddressFamily.InterNetwork)
            return false;
        if (!int.TryParse(parts[1], out prefixLen) || prefixLen < 0 || prefixLen > 32)
            return false;

        var mask = prefixLen == 0 ? 0u : uint.MaxValue << (32 - prefixLen);
        network = ToUInt32(addr) & mask;
        return true;
    }

    private static string NormalizeCidr(uint network, int prefixLen)
    {
        var bytes = new byte[]
        {
            (byte)(network >> 24),
            (byte)(network >> 16),
            (byte)(network >> 8),
            (byte)network
        };
        return $"{new IPAddress(bytes)}/{prefixLen}";
    }

    private static uint ToUInt32(IPAddress addr)
    {
        var b = addr.GetAddressBytes();
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
