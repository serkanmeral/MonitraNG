using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

internal static class SecEventQueryFilterBuilder
{
    public const int MaxLimit = 200;

    public static FilterDefinition<BsonDocument> Build(SecEventQueryFilter filter)
    {
        var builder = Builders<BsonDocument>.Filter;
        var clauses = new List<FilterDefinition<BsonDocument>>();

        if (filter.From.HasValue)
        {
            var from = filter.From.Value.Kind == DateTimeKind.Utc
                ? filter.From.Value
                : filter.From.Value.ToUniversalTime();
            clauses.Add(builder.Gte(SecEventDashboardAggregator.DashboardTimeField, new BsonDateTime(from)));
        }

        if (filter.To.HasValue)
        {
            var to = filter.To.Value.Kind == DateTimeKind.Utc
                ? filter.To.Value
                : filter.To.Value.ToUniversalTime();
            clauses.Add(builder.Lte(SecEventDashboardAggregator.DashboardTimeField, new BsonDateTime(to)));
        }

        if (!string.IsNullOrWhiteSpace(filter.SourceType))
            clauses.Add(builder.Eq("source.type", filter.SourceType.Trim()));

        if (!string.IsNullOrWhiteSpace(filter.EventAction))
        {
            var action = filter.EventAction.Trim();
            if (string.Equals(action, SecEventFlowBaselineRules.NewFlowAction, StringComparison.OrdinalIgnoreCase))
                clauses.Add(builder.Eq("baseline.newFlowPair", true));
            else
                clauses.Add(builder.Eq("event.action", action));
        }

        if (!string.IsNullOrWhiteSpace(filter.SrcIp))
            clauses.Add(builder.Eq("network.srcIp", filter.SrcIp.Trim()));

        if (!string.IsNullOrWhiteSpace(filter.ActorUser))
            clauses.Add(builder.Eq("actor.user", filter.ActorUser.Trim()));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var escaped = Regex.Escape(filter.Search.Trim());
            var regex = new BsonRegularExpression(escaped, "i");
            clauses.Add(builder.Or(
                builder.Regex("rawPreview", regex),
                builder.Regex("event.action", regex),
                builder.Regex("actor.user", regex),
                builder.Regex("network.srcIp", regex),
                builder.Regex("network.dstIp", regex),
                builder.Regex("source.host", regex)));
        }

        if (filter.ExcludeUnknown)
            clauses.Add(builder.Ne("event.action", SecEventUnknownFilter.UnknownAction));

        return clauses.Count == 0 ? builder.Empty : builder.And(clauses);
    }

    public static int NormalizeLimit(int limit) =>
        limit <= 0 ? 50 : Math.Min(limit, MaxLimit);

    public static int NormalizeSkip(int skip) =>
        skip < 0 ? 0 : skip;
}
