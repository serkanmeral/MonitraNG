using System.Globalization;
using MngDataGateway.Domain.Entities;
using MongoDB.Bson;

namespace MngDataGateway.Persistence.Services;

/// <summary>
/// Coerces string date operands in native $match (POST /query) to BSON Date for datetime schema fields.
/// GET ?filter= already normalizes via <see cref="FilterParser"/>; this covers match dictionaries from JSON.
/// </summary>
public static class DatetimeMatchFilterExpander
{
    public static BsonDocument? Expand(BsonDocument? match, DatasetSchema schema)
    {
        if (match == null || match.ElementCount == 0)
            return match;

        var datetimeFields = schema?.fields?
            .Where(f => string.Equals(f.fieldType, "datetime", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        if (datetimeFields.Count == 0)
            return match;

        return CoerceMatchDocument(match, datetimeFields);
    }

    private static BsonDocument CoerceMatchDocument(BsonDocument doc, HashSet<string> datetimeFields)
    {
        if (doc.Contains("$and"))
        {
            var array = doc["$and"].AsBsonArray;
            return new BsonDocument("$and", new BsonArray(
                array.Select(item => item.IsBsonDocument
                    ? CoerceMatchDocument(item.AsBsonDocument, datetimeFields)
                    : item)));
        }

        if (doc.Contains("$or"))
        {
            var array = doc["$or"].AsBsonArray;
            return new BsonDocument("$or", new BsonArray(
                array.Select(item => item.IsBsonDocument
                    ? CoerceMatchDocument(item.AsBsonDocument, datetimeFields)
                    : item)));
        }

        var result = new BsonDocument();
        foreach (var element in doc.Elements)
        {
            if (datetimeFields.Contains(element.Name) && element.Value.IsBsonDocument)
                result.Add(element.Name, CoerceDateOperators(element.Value.AsBsonDocument));
            else
                result.Add(element.Name, element.Value);
        }

        return result;
    }

    private static BsonDocument CoerceDateOperators(BsonDocument ops)
    {
        var result = new BsonDocument();
        foreach (var op in ops.Elements)
        {
            if (op.Name is "$gte" or "$lte" or "$gt" or "$lt")
            {
                var coerced = CoerceRangeOperand(op.Name, op.Value);
                result.Add(op.Name, coerced);
            }
            else
            {
                result.Add(op.Name, op.Value);
            }
        }

        return result;
    }

    private static BsonValue CoerceRangeOperand(string operatorName, BsonValue value)
    {
        if (value.BsonType == BsonType.DateTime)
            return value;

        if (!value.IsString)
            return value;

        var raw = value.AsString;
        var normalized = NormalizeDateComparisonValue(operatorName, raw);
        if (TryParseFilterDateTime(normalized, out var utc))
            return new BsonDateTime(utc);

        return value;
    }

    private static string NormalizeDateComparisonValue(string operatorStr, string valueStr)
    {
        if (!IsDateOnly(valueStr))
            return valueStr;

        return operatorStr switch
        {
            "$lte" or "lte" => $"{valueStr}T23:59:59.999Z",
            "$gt" or "gt" => $"{valueStr}T23:59:59.999Z",
            "$gte" or "gte" => $"{valueStr}T00:00:00.000Z",
            "$lt" or "lt" => $"{valueStr}T00:00:00.000Z",
            _ => valueStr,
        };
    }

    private static bool IsDateOnly(string valueStr) =>
        !string.IsNullOrWhiteSpace(valueStr)
        && DateTime.TryParseExact(
            valueStr.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);

    private static bool TryParseFilterDateTime(string valueStr, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(valueStr))
            return false;

        var trimmed = valueStr.Trim();

        if (DateTime.TryParseExact(
                trimmed,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOnly))
        {
            utc = DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc);
            return true;
        }

        if (!IsIsoDateTime(trimmed))
            return false;

        if (!DateTime.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return false;
        }

        utc = parsed.Kind switch
        {
            DateTimeKind.Utc => parsed,
            DateTimeKind.Local => parsed.ToUniversalTime(),
            _ => DateTime.SpecifyKind(parsed, DateTimeKind.Utc),
        };
        return true;
    }

    private static bool IsIsoDateTime(string valueStr) =>
        !string.IsNullOrWhiteSpace(valueStr)
        && valueStr.Contains('T', StringComparison.Ordinal)
        && DateTime.TryParse(
            valueStr.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out _);
}
