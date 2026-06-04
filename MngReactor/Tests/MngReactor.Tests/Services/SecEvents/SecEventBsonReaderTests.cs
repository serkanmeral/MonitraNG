using MongoDB.Bson;
using MngReactor.Persistence.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventBsonReaderTests
{
    [Fact]
    public void ToListItem_WithoutRaw_ExcludesRawField()
    {
        var doc = SampleDoc(includeRawField: true);

        var item = SecEventBsonReader.ToListItem(doc, includeRaw: false);

        Assert.Null(item.Raw);
        Assert.Equal("preview512", item.RawPreview);
    }

    [Fact]
    public void ToListItem_WithRaw_IncludesStoredRaw()
    {
        var doc = SampleDoc(includeRawField: true);

        var item = SecEventBsonReader.ToListItem(doc, includeRaw: true);

        Assert.Equal("full raw text", item.Raw);
    }

    [Fact]
    public void ToListItem_LegacyDocWithoutRawField_FallsBackToPreview()
    {
        var doc = SampleDoc(includeRawField: false);

        var item = SecEventBsonReader.ToListItem(doc, includeRaw: true);

        Assert.Equal("preview512", item.Raw);
    }

    private static BsonDocument SampleDoc(bool includeRawField)
    {
        var doc = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["@timestamp"] = DateTime.UtcNow,
            ["ingestedAt"] = DateTime.UtcNow,
            ["event"] = new BsonDocument { ["action"] = "login_failed" },
            ["rawPreview"] = "preview512"
        };

        if (includeRawField)
            doc["raw"] = "full raw text";

        return doc;
    }
}
