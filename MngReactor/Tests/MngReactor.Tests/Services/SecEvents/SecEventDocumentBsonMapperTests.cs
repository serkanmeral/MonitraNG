using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using MongoDB.Bson;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventDocumentBsonMapperTests
{
    [Fact]
    public void ToBsonDocument_MapsTimestampAndNestedFields()
    {
        var doc = new SecEventDocument
        {
            Timestamp = DateTime.Parse("2026-06-03T14:00:02Z").ToUniversalTime(),
            IngestedAt = DateTime.Parse("2026-06-03T14:00:03Z").ToUniversalTime(),
            Domain = "odak",
            Source = new SecEventSourceInfo { Type = "ad", Product = "windows", Host = "dc01" },
            Event = new SecEventEventBlock { Action = "login_failed", Outcome = "failure", Code = "4625" },
            Actor = new SecEventActorBlock { User = "admin" },
            Network = new SecEventNetworkBlock
            {
                SrcIp = "192.168.1.50",
                DstIp = "10.0.0.10",
                DstPort = 445,
                Protocol = "tcp"
            },
            Parser = new SecEventParserBlock { Id = WindowsSecurityParser.ParserIdValue },
            Raw = "full raw payload",
            RawPreview = "preview"
        };

        var bson = SecEventDocumentBsonMapper.ToBsonDocument(doc);

        Assert.Equal(BsonType.DateTime, bson["@timestamp"].BsonType);
        Assert.Equal("full raw payload", bson["raw"].AsString);
        Assert.Equal("preview", bson["rawPreview"].AsString);
        Assert.Equal("login_failed", bson["event"]["action"].AsString);
        Assert.Equal("admin", bson["actor"]["user"].AsString);
        Assert.Equal("192.168.1.50", bson["network"]["srcIp"].AsString);
        Assert.Equal(445, bson["network"]["dstPort"].AsInt32);
        Assert.Equal(WindowsSecurityParser.ParserIdValue, bson["parser"]["id"].AsString);
    }

    [Fact]
    public void FromParsed_FirewallFixture_ProducesQueryableDocument()
    {
        var parser = new FirewallGenericSyslogParser();
        var ctx = SecEventTestData.FirewallDenyContext();
        var parsed = parser.Parse(ctx);
        var doc = SecEventDocument.FromParsed(parsed, "odak", DateTime.UtcNow);

        var bson = SecEventDocumentBsonMapper.ToBsonDocument(doc);

        Assert.Equal("denied_flow", bson["event"]["action"].AsString);
        Assert.Equal("203.0.113.5", bson["network"]["srcIp"].AsString);
        Assert.Equal("firewall", bson["source"]["type"].AsString);
    }
}
