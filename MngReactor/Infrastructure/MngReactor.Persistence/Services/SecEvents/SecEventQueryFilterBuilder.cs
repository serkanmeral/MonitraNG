using System.Linq;
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

        if (!string.IsNullOrWhiteSpace(filter.SourceProduct))
            clauses.Add(builder.Eq("source.product", filter.SourceProduct.Trim()));

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
        else if (!string.IsNullOrWhiteSpace(filter.SourceHosts))
        {
            var hosts = ParseCsv(filter.SourceHosts);
            if (hosts.Count == 1)
            {
                var hostEscaped = Regex.Escape(hosts[0]);
                clauses.Add(builder.Regex("source.host", new BsonRegularExpression(hostEscaped, "i")));
            }
            else if (hosts.Count > 1)
            {
                var hostClauses = hosts
                    .Select(h => builder.Regex("source.host", new BsonRegularExpression(Regex.Escape(h), "i")))
                    .ToList();
                clauses.Add(builder.Or(hostClauses));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.EventCode))
            clauses.Add(builder.Eq("event.code", filter.EventCode.Trim()));
        else if (!string.IsNullOrWhiteSpace(filter.EventCodes))
        {
            var codes = ParseCsv(filter.EventCodes);
            if (codes.Count == 1)
                clauses.Add(builder.Eq("event.code", codes[0]));
            else if (codes.Count > 1)
                clauses.Add(builder.In("event.code", codes));
        }

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

        if (filter.FieldFilters is { Count: > 0 })
        {
            foreach (var clause in filter.FieldFilters.Take(SecEventFieldQueryHelper.MaxClauses))
            {
                var fieldClause = BuildFieldFilterClause(builder, clause);
                if (fieldClause is not null)
                    clauses.Add(fieldClause);
            }
        }

        if (filter.ExcludeUnknown)
            clauses.Add(builder.Ne("event.action", SecEventUnknownFilter.UnknownAction));

        return clauses.Count == 0 ? builder.Empty : builder.And(clauses);
    }

    private static FilterDefinition<BsonDocument>? BuildFieldFilterClause(
        FilterDefinitionBuilder<BsonDocument> builder,
        SecEventFieldFilterClause clause)
    {
        if (!SecEventFieldQueryHelper.IsAllowedField(clause.Field))
            return null;

        var op = SecEventFieldQueryHelper.NormalizeOp(clause.Op);
        var value = (clause.Value ?? string.Empty).Trim();
        if (value.Length == 0)
            return null;

        if (SecEventFieldQueryHelper.IsBagField(clause.Field))
            return BuildBagFieldClause(clause.Field.Trim(), op, value);

        var path = SecEventFieldQueryHelper.TopLevelPath(clause.Field);
        if (path is null)
            return null;

        if (string.Equals(path, "network.dstPort", StringComparison.Ordinal))
            return BuildDstPortFieldClause(builder, op, value);

        return op switch
        {
            "eq" => builder.Eq(path, value),
            "neq" => builder.Ne(path, value),
            "in" => BuildInClause(builder, path, value),
            "contains" => builder.Regex(path, new BsonRegularExpression(Regex.Escape(value), "i")),
            "prefix" => builder.Regex(path, new BsonRegularExpression($"^{Regex.Escape(value)}")),
            _ => builder.Eq(path, value)
        };
    }

    private static FilterDefinition<BsonDocument>? BuildInClause(
        FilterDefinitionBuilder<BsonDocument> builder,
        string path,
        string csv)
    {
        var values = ParseCsv(csv);
        if (values.Count == 0)
            return null;
        if (values.Count == 1)
            return builder.Eq(path, values[0]);
        return builder.In(path, values);
    }

    private static FilterDefinition<BsonDocument>? BuildDstPortFieldClause(
        FilterDefinitionBuilder<BsonDocument> builder,
        string op,
        string value)
    {
        return op switch
        {
            "eq" => BuildDstPortFilter(builder, value),
            "neq" => builder.Not(BuildDstPortFilter(builder, value)),
            "in" => BuildDstPortInFilter(builder, value),
            _ => BuildDstPortFilter(builder, value)
        };
    }

    private static FilterDefinition<BsonDocument>? BuildDstPortInFilter(
        FilterDefinitionBuilder<BsonDocument> builder,
        string csv)
    {
        var ports = ParseCsv(csv);
        if (ports.Count == 0)
            return null;
        var or = ports.Select(p => BuildDstPortFilter(builder, p)).ToList();
        return builder.Or(or);
    }

    /// <summary>
    /// Query <c>fields</c> bag including dotted keys (custom.session_id) via $getField.
    /// </summary>
    private static FilterDefinition<BsonDocument> BuildBagFieldClause(
        string bagKey,
        string op,
        string value)
    {
        var getField = new BsonDocument("$getField", new BsonDocument
        {
            { "field", bagKey },
            { "input", "$fields" }
        });

        return op switch
        {
            "neq" => new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument("$expr",
                new BsonDocument("$ne", new BsonArray
                {
                    new BsonDocument("$toString", new BsonDocument("$ifNull", new BsonArray { getField, "" })),
                    value
                }))),
            "in" => BuildBagInExpr(getField, value),
            "contains" => new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument("$expr",
                new BsonDocument("$regexMatch", new BsonDocument
                {
                    {
                        "input",
                        new BsonDocument("$toString", new BsonDocument("$ifNull", new BsonArray { getField, "" }))
                    },
                    { "regex", Regex.Escape(value) },
                    { "options", "i" }
                }))),
            "prefix" => new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument("$expr",
                new BsonDocument("$regexMatch", new BsonDocument
                {
                    {
                        "input",
                        new BsonDocument("$toString", new BsonDocument("$ifNull", new BsonArray { getField, "" }))
                    },
                    { "regex", $"^{Regex.Escape(value)}" },
                    { "options", "" }
                }))),
            _ => new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument("$expr",
                new BsonDocument("$eq", new BsonArray
                {
                    new BsonDocument("$toString", new BsonDocument("$ifNull", new BsonArray { getField, "" })),
                    value
                })))
        };
    }

    private static FilterDefinition<BsonDocument> BuildBagInExpr(BsonDocument getField, string csv)
    {
        var values = ParseCsv(csv);
        if (values.Count == 0)
            return Builders<BsonDocument>.Filter.Empty;

        return new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument("$expr",
            new BsonDocument("$in", new BsonArray
            {
                new BsonDocument("$toString", new BsonDocument("$ifNull", new BsonArray { getField, "" })),
                new BsonArray(values.Select(v => (BsonValue)v))
            })));
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
