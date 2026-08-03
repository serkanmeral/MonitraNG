using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Application.Services.SecEvents;

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
        else if (!string.IsNullOrWhiteSpace(filter.EventActions))
        {
            var actions = ParseCsv(filter.EventActions);
            if (actions.Count == 1)
            {
                var only = actions[0];
                if (string.Equals(only, SecEventFlowBaselineRules.NewFlowAction, StringComparison.OrdinalIgnoreCase))
                    clauses.Add(builder.Eq("baseline.newFlowPair", true));
                else
                    clauses.Add(builder.Eq("event.action", only));
            }
            else if (actions.Count > 1)
            {
                // new_flow is baseline flag — keep OR with action terms when mixed.
                var actionClauses = new List<FilterDefinition<BsonDocument>>();
                foreach (var action in actions)
                {
                    if (string.Equals(action, SecEventFlowBaselineRules.NewFlowAction, StringComparison.OrdinalIgnoreCase))
                        actionClauses.Add(builder.Eq("baseline.newFlowPair", true));
                    else
                        actionClauses.Add(builder.Eq("event.action", action));
                }

                clauses.Add(builder.Or(actionClauses));
            }
        }
        else if (!string.IsNullOrWhiteSpace(filter.EventActionPrefix))
        {
            var prefix = filter.EventActionPrefix.Trim();
            if (SecEventRdpActionCodes.IsRdpActionPrefix(prefix))
            {
                // Agent path may store raw message before normalize; also match codes/product.
                clauses.Add(builder.Or(
                    builder.Regex("event.action", new BsonRegularExpression($"^{Regex.Escape(prefix)}")),
                    builder.In("event.code", SecEventRdpActionCodes.EventCodes),
                    builder.Eq("source.product", SecEventRdpActionCodes.ProductRdpSession)));
            }
            else
            {
                clauses.Add(builder.Regex("event.action", new BsonRegularExpression($"^{Regex.Escape(prefix)}")));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.EventOutcome))
            clauses.Add(builder.Eq("event.outcome", filter.EventOutcome.Trim()));

        if (!string.IsNullOrWhiteSpace(filter.SrcIp))
            clauses.Add(builder.Eq("network.srcIp", filter.SrcIp.Trim()));

        if (!string.IsNullOrWhiteSpace(filter.DstIp))
            clauses.Add(builder.Eq("network.dstIp", filter.DstIp.Trim()));

        if (!string.IsNullOrWhiteSpace(filter.DstPort))
            clauses.Add(BuildDstPortFilter(builder, filter.DstPort.Trim()));

        if (!string.IsNullOrWhiteSpace(filter.ActorUser))
            clauses.Add(builder.Eq("actor.user", filter.ActorUser.Trim()));

        if (!string.IsNullOrWhiteSpace(filter.SourceHost))
        {
            var hostEscaped = Regex.Escape(filter.SourceHost.Trim());
            clauses.Add(builder.Regex("source.host", new BsonRegularExpression(hostEscaped, "i")));
        }

        if (!string.IsNullOrWhiteSpace(filter.EventCode))
            clauses.Add(builder.Eq("event.code", filter.EventCode.Trim()));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var escaped = Regex.Escape(filter.Search.Trim());
            var regex = new BsonRegularExpression(escaped, "i");
            clauses.Add(builder.Or(
                builder.Regex("rawPreview", regex),
                builder.Regex("event.action", regex),
                builder.Regex("event.code", regex),
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

    internal static List<string> ParseCsv(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];
        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>dstPort may be stored as string or int depending on parser path.</summary>
    private static FilterDefinition<BsonDocument> BuildDstPortFilter(
        FilterDefinitionBuilder<BsonDocument> builder,
        string raw)
    {
        if (int.TryParse(raw, out var port))
        {
            return builder.Or(
                builder.Eq("network.dstPort", port),
                builder.Eq("network.dstPort", raw));
        }

        return builder.Eq("network.dstPort", raw);
    }
}
