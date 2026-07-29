using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventOpenSearchWriterTests
{
    [Fact]
    public void BuildIndexName_SanitizesDomain_AndUsesUtcDay()
    {
        var name = SecEventOpenSearchWriter.BuildIndexName(
            "Odak.COM",
            new DateTime(2026, 7, 29, 15, 0, 0, DateTimeKind.Utc));
        Assert.Equal("mng-odak-com-sec-events-2026.07.29", name);
    }

    [Fact]
    public void BuildBulkNdjson_EmitsIndexAndDocLines()
    {
        var doc = new SecEventDocument
        {
            Timestamp = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc),
            IngestedAt = new DateTime(2026, 7, 29, 12, 1, 0, DateTimeKind.Utc),
            Domain = "odak",
            Source = new SecEventSourceInfo { Type = "firewall", Product = "fortigate", Host = "fw01" },
            Event = new SecEventEventBlock { Action = "denied_flow", Outcome = "failure", Code = null },
            Parser = new SecEventParserBlock { Id = "firewall.vendor.v1" },
            Raw = "",
            RawPreview = "deny src=1.2.3.4"
        };

        var ndjson = SecEventOpenSearchWriter.BuildBulkNdjson("odak", [doc]);
        var lines = ndjson.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("mng-odak-sec-events-2026.07.29", lines[0]);
        Assert.Contains("\"_id\":\"testid\"", lines[0]);
        Assert.Contains("denied_flow", lines[1]);
        Assert.Contains("rawPreview", lines[1]);
    }
}
