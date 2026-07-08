using Microsoft.Extensions.Logging.Abstractions;
using MngDataGateway.Domain.Entities;
using MongoDB.Bson;
using MngDataGateway.Persistence.Services;
using Xunit;

namespace MngDataGateway.Tests.Services;

/// <summary>
/// Odak egitim year + status filter — regression for reporting ?filter= durum + gerceklesenTarih gte/lte.
/// </summary>
public class OdakEgitimYearFilterTests
{
    private static readonly FilterParser Parser = new(NullLogger<FilterParser>.Instance);

    private static DatasetSchema OdakEgitimlerSchema() => new()
    {
        name = "odak_egitimler",
        fields =
        [
            new FieldDefinition { name = "durum", fieldType = "select" },
            new FieldDefinition { name = "gerceklesenTarih", fieldType = "datetime" },
            new FieldDefinition { name = "planlananTarih", fieldType = "datetime" },
        ],
    };

    [Fact]
    public void Build_OdakEgitimYearFilter_ProducesFlatAndWithBsonDateRange()
    {
        const string filter =
            "durum:eq:Tamamlandi,gerceklesenTarih:gte:2017-01-01,gerceklesenTarih:lte:2017-12-31";

        var match = MatchFilterFactory.Build(Parser, filter, OdakEgitimlerSchema());

        Assert.NotNull(match);
        Assert.True(match!.Contains("$and"));

        var and = match["$and"].AsBsonArray;
        Assert.Equal(2, and.Count);
        Assert.Equal("Tamamlandi", and[0].AsBsonDocument["durum"].AsString);

        var dateDoc = and[1].AsBsonDocument;
        Assert.False(dateDoc.Contains("$or"));
        var range = dateDoc["gerceklesenTarih"].AsBsonDocument;
        Assert.Equal(BsonType.DateTime, range["$gte"].BsonType);
        Assert.Equal(BsonType.DateTime, range["$lte"].BsonType);
        Assert.Equal(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc), range["$gte"].ToUniversalTime());
    }

    [Fact]
    public void Parse_YearRangeOnly_MergesGteAndLte_OnSameField()
    {
        var result = Parser.Parse("gerceklesenTarih:gte:2024-01-01,gerceklesenTarih:lte:2024-12-31");

        Assert.NotNull(result);
        var range = result!["gerceklesenTarih"].AsBsonDocument;
        Assert.True(range.Contains("$gte"));
        Assert.True(range.Contains("$lte"));
        Assert.Equal(BsonType.DateTime, range["$gte"].BsonType);
        Assert.Equal(BsonType.DateTime, range["$lte"].BsonType);
    }
}
