using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MngDataGateway.Persistence.Services;
using Xunit;

namespace MngDataGateway.Tests.Services;

public class FilterParserTests
{
    private readonly FilterParser _parser = new(NullLogger<FilterParser>.Instance);

    [Fact]
    public void Parse_SameField_GteAndLte_MergesIntoSingleFieldCondition()
    {
        var result = _parser.Parse("gerceklesenTarih:gte:2025-01-01,gerceklesenTarih:lte:2025-12-31");

        Assert.NotNull(result);
        Assert.False(result!.Contains("$and"));
        var field = result["gerceklesenTarih"].AsBsonDocument;
        Assert.True(field.Contains("$gte"));
        Assert.True(field.Contains("$lte"));
    }

    [Fact]
    public void Parse_DifferentFields_BothPresent()
    {
        var result = _parser.Parse("durum:eq:Tamamlandi,gerceklesenTarih:gte:2025-01-01");

        Assert.NotNull(result);
        Assert.True(result!.Contains("$and"));
        var and = result["$and"].AsBsonArray;
        var durumDoc = and.First(x => x.AsBsonDocument.Contains("durum")).AsBsonDocument;
        var dateDoc = and.First(x => x.AsBsonDocument.Contains("gerceklesenTarih")).AsBsonDocument;
        Assert.Equal("Tamamlandi", durumDoc["durum"].AsString);
        Assert.Equal(BsonType.DateTime, dateDoc["gerceklesenTarih"].AsBsonDocument["$gte"].BsonType);
    }

    [Fact]
    public void Parse_DateRangeOnDatetimeField_UsesBsonDateTimeOperands()
    {
        var result = _parser.Parse(
            "durum:eq:Tamamlandi,gerceklesenTarih:gte:2021-01-01,gerceklesenTarih:lte:2021-12-31");

        Assert.NotNull(result);
        Assert.True(result!.Contains("$and"));
        var dateDoc = result["$and"].AsBsonArray
            .First(x => x.AsBsonDocument.Contains("gerceklesenTarih"))
            .AsBsonDocument;
        var range = dateDoc["gerceklesenTarih"].AsBsonDocument;
        Assert.Equal(BsonType.DateTime, range["$gte"].BsonType);
        Assert.Equal(BsonType.DateTime, range["$lte"].BsonType);
        Assert.Equal(new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc), range["$gte"].ToUniversalTime());
    }

    [Fact]
    public void Parse_ConflictingEqOnSameField_UsesAnd()
    {
        var result = _parser.Parse("durum:eq:Planlandi,durum:eq:Tamamlandi");

        Assert.NotNull(result);
        Assert.True(result!.Contains("$and"));
        var and = result["$and"].AsBsonArray;
        Assert.Equal(2, and.Count);
    }

    [Fact]
    public void Parse_OdakYearFilter_DoesNotThrow_OnNormalizedIsoDateOperands()
    {
        var result = _parser.Parse(
            "durum:eq:Tamamlandi,gerceklesenTarih:gte:2017-01-01,gerceklesenTarih:lte:2017-12-31");

        Assert.NotNull(result);
        Assert.True(result!.Contains("$and"));
        Assert.Contains(
            result["$and"].AsBsonArray,
            x => x.AsBsonDocument.Contains("gerceklesenTarih"));
    }
}
