using MngDataGateway.Domain.Entities;
using MongoDB.Bson;
using MngDataGateway.Persistence.Services;
using Xunit;

namespace MngDataGateway.Tests.Services;

public class DatetimeMatchFilterExpanderTests
{
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
    public void Expand_PassThrough_KeepsFlatBsonDateRange()
    {
        var match = new BsonDocument
        {
            { "durum", "Tamamlandi" },
            {
                "gerceklesenTarih",
                new BsonDocument
                {
                    { "$gte", new BsonDateTime(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)) },
                    { "$lte", new BsonDateTime(new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc)) },
                }
            },
        };

        var expanded = DatetimeMatchFilterExpander.Expand(match, OdakEgitimlerSchema());

        Assert.NotNull(expanded);
        Assert.False(expanded!.Contains("$or"));
        Assert.Equal("Tamamlandi", expanded["durum"].AsString);
        Assert.Equal(BsonType.DateTime, expanded["gerceklesenTarih"].AsBsonDocument["$gte"].BsonType);
    }

    [Fact]
    public void Expand_StringYearOrRange_CoercesToBsonDateTime()
    {
        var match = new BsonDocument
        {
            {
                "$and",
                new BsonArray
                {
                    new BsonDocument("durum", "Tamamlandi"),
                    new BsonDocument
                    {
                        {
                            "$or",
                            new BsonArray
                            {
                                new BsonDocument
                                {
                                    {
                                        "planlananTarih",
                                        new BsonDocument
                                        {
                                            { "$gte", "2017-01-01" },
                                            { "$lte", "2017-12-31" },
                                        }
                                    },
                                },
                                new BsonDocument
                                {
                                    {
                                        "gerceklesenTarih",
                                        new BsonDocument
                                        {
                                            { "$gte", "2017-01-01" },
                                            { "$lte", "2017-12-31" },
                                        }
                                    },
                                },
                            }
                        },
                    },
                }
            },
        };

        var expanded = DatetimeMatchFilterExpander.Expand(match, OdakEgitimlerSchema());

        Assert.NotNull(expanded);
        var and = expanded!["$and"].AsBsonArray;
        var or = and[1].AsBsonDocument["$or"].AsBsonArray;

        var planRange = or[0].AsBsonDocument["planlananTarih"].AsBsonDocument;
        Assert.Equal(BsonType.DateTime, planRange["$gte"].BsonType);
        Assert.Equal(BsonType.DateTime, planRange["$lte"].BsonType);
        Assert.Equal(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc), planRange["$gte"].ToUniversalTime());

        var actualRange = or[1].AsBsonDocument["gerceklesenTarih"].AsBsonDocument;
        Assert.Equal(BsonType.DateTime, actualRange["$gte"].BsonType);
        Assert.Equal(BsonType.DateTime, actualRange["$lte"].BsonType);
    }
}
