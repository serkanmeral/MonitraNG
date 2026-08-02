using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Services.Discovery;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Tests;

public class PrefixTableMatcherTests
{
    private static readonly DiscoveryPrefixEntry[] Table =
    [
        new() { Cidr = "10.0.0.0/8", Label = "Corp" },
        new() { Cidr = "10.10.0.0/16", Label = "DC" },
        new() { Cidr = "192.168.20.0/24", Label = "Odak ofis" },
    ];

    [Fact]
    public void Longest_Prefix_Wins()
    {
        var m = PrefixTableMatcher.TryMatch("10.10.5.9", Table);
        Assert.NotNull(m);
        Assert.Equal("DC", m!.Label);
        Assert.Equal("10.10.0.0/16", m.Cidr);
    }

    [Fact]
    public void Odak_Lan_Matches()
    {
        var m = PrefixTableMatcher.TryMatch("192.168.20.8", Table);
        Assert.NotNull(m);
        Assert.Equal("Odak ofis", m!.Label);
    }

    [Fact]
    public void Unmatched_Is_Unscoped()
    {
        var host = new DiscoveryHost { IpAddresses = ["8.8.8.8"] };
        PrefixTableMatcher.ApplyToHost(host, Table);
        Assert.Null(host.SubnetCidr);
        Assert.Equal(PrefixTableMatcher.UnscopedLabel, host.SiteLabel);
    }

    [Fact]
    public void No_Ip_Leaves_Site_Null()
    {
        var host = new DiscoveryHost { IpAddresses = [] };
        PrefixTableMatcher.ApplyToHost(host, Table);
        Assert.Null(host.SiteLabel);
        Assert.Null(host.SubnetCidr);
    }
}
