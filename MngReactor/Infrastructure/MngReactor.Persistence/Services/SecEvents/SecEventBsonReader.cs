using MongoDB.Bson;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

internal static class SecEventBsonReader
{
    public static SecEventListItem ToListItem(BsonDocument doc, bool includeRaw = false)
    {
        var id = doc.TryGetValue("_id", out var idVal) ? idVal.ToString()! : string.Empty;
        var rawPreview = ReadString(doc, "rawPreview");
        return new SecEventListItem
        {
            Id = id,
            Timestamp = ReadDate(doc, "@timestamp"),
            IngestedAt = ReadDate(doc, "ingestedAt"),
            SourceType = ReadNestedString(doc, "source", "type"),
            SourceProduct = ReadNestedString(doc, "source", "product"),
            SourceHost = ReadNestedString(doc, "source", "host"),
            EventAction = ReadNestedString(doc, "event", "action") ?? "unknown",
            EventOutcome = ReadNestedString(doc, "event", "outcome"),
            EventCode = ReadNestedString(doc, "event", "code"),
            ActorUser = ReadNestedString(doc, "actor", "user"),
            NetworkSrcIp = ReadNestedString(doc, "network", "srcIp"),
            NetworkDstIp = ReadNestedString(doc, "network", "dstIp"),
            ParserId = ReadNestedString(doc, "parser", "id"),
            RawPreview = rawPreview,
            Raw = includeRaw ? ReadString(doc, "raw") ?? rawPreview : null,
            BaselineNewFlowPair = ReadNestedBool(doc, "baseline", "newFlowPair"),
            Fields = ReadFields(doc)
        };
    }

    private static IReadOnlyDictionary<string, object?>? ReadFields(BsonDocument doc)
    {
        if (!doc.TryGetValue("fields", out var fieldsVal) || !fieldsVal.IsBsonDocument)
            return null;

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in fieldsVal.AsBsonDocument.Elements)
            dict[el.Name] = BsonValueToClr(el.Value);
        return dict.Count == 0 ? null : dict;
    }

    private static object? BsonValueToClr(BsonValue value) => value.BsonType switch
    {
        BsonType.Null => null,
        BsonType.String => value.AsString,
        BsonType.Boolean => value.AsBoolean,
        BsonType.Int32 => value.AsInt32,
        BsonType.Int64 => value.AsInt64,
        BsonType.Double => value.AsDouble,
        BsonType.DateTime => value.ToUniversalTime(),
        BsonType.Array => value.AsBsonArray.Select(BsonValueToClr).ToList(),
        BsonType.Document => value.AsBsonDocument.Elements
            .ToDictionary(e => e.Name, e => BsonValueToClr(e.Value), StringComparer.OrdinalIgnoreCase),
        _ => value.ToString()
    };

    private static DateTime ReadDate(BsonDocument doc, string field)
    {
        if (!doc.TryGetValue(field, out var value) || value.IsBsonNull)
            return DateTime.MinValue;

        return value.BsonType switch
        {
            BsonType.DateTime => value.ToUniversalTime(),
            _ => DateTime.MinValue
        };
    }

    private static string? ReadString(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && value.IsString ? value.AsString : null;

    private static string? ReadNestedString(BsonDocument doc, string parent, string child)
    {
        if (!doc.TryGetValue(parent, out var parentVal) || !parentVal.IsBsonDocument)
            return null;

        var nested = parentVal.AsBsonDocument;
        if (!nested.TryGetValue(child, out var childVal) || childVal.IsBsonNull)
            return null;

        return childVal.IsString ? childVal.AsString : childVal.ToString();
    }

    private static bool ReadNestedBool(BsonDocument doc, string parent, string child)
    {
        if (!doc.TryGetValue(parent, out var parentVal) || !parentVal.IsBsonDocument)
            return false;

        var nested = parentVal.AsBsonDocument;
        if (!nested.TryGetValue(child, out var childVal) || childVal.IsBsonNull)
            return false;

        return childVal.IsBoolean && childVal.AsBoolean;
    }
}
