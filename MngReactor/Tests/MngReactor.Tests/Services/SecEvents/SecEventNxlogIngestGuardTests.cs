using System.Text.Json;
using MngReactor.Application.Features.Commands.Ingest;
using MngReactor.Application.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventNxlogIngestGuardTests
{
    [Theory]
    [InlineData("windows-nxlog", true)]
    [InlineData("windows-nxlog-json", true)]
    [InlineData("WINDOWS-NXLOG", true)]
    [InlineData("fortigate", false)]
    [InlineData("linux-syslog", false)]
    [InlineData("mnglogs-agent", false)]
    [InlineData(null, false)]
    public void IsNxlogProduct_MatchesLegacyProducts(string? product, bool expected) =>
        Assert.Equal(expected, SecEventNxlogIngestGuard.IsNxlogProduct(product));

    [Fact]
    public void LooksLikeNxlogJson_DetectsEventIdHostnameShape()
    {
        const string raw = """{"EventID":4624,"Hostname":"dc01","Channel":"Security"}""";
        Assert.True(SecEventNxlogIngestGuard.LooksLikeNxlogJson(raw));
        Assert.False(SecEventNxlogIngestGuard.LooksLikeNxlogJson("sshd[1]: Accepted password"));
    }

    [Fact]
    public void ShouldReject_FalseWhenAcceptEnabled()
    {
        var item = new SecEventIngestItem
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventIngestSource { Product = "windows-nxlog" },
            Raw = JsonSerializer.SerializeToElement(new { EventID = 4624, Hostname = "dc01" })
        };
        Assert.False(SecEventNxlogIngestGuard.ShouldReject(item, acceptNxlogIngest: true));
    }
}
