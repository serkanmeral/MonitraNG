using MngDataGateway.Domain.Entities;
using MongoDB.Bson;

namespace MngDataGateway.Persistence.Services;

/// <summary>
/// REST ?filter= → MongoDB $match (datetime schema-aware coercion).
/// </summary>
public static class MatchFilterFactory
{
    public static BsonDocument? Build(FilterParser parser, string? filter, DatasetSchema schema)
    {
        var match = parser.Parse(filter);
        return DatetimeMatchFilterExpander.Expand(match, schema);
    }
}
