using MongoDB.Bson;
using System.Collections.Generic;
using System.Linq;

namespace MngKeeper.Persistence.Extensions;

/// <summary>
/// Extension methods for BsonDocument conversion
/// </summary>
public static class BsonDocumentExtensions
{
    /// <summary>
    /// Convert BsonDocument to Dictionary with proper type mapping
    /// </summary>
    public static Dictionary<string, object> ToDictionary(this BsonDocument document)
    {
        if (document == null)
            return new Dictionary<string, object>();

        var dictionary = new Dictionary<string, object>();

        foreach (var element in document.Elements)
        {
            dictionary[element.Name] = MapToDotNetValue(element.Value);
        }

        return dictionary;
    }

    /// <summary>
    /// Convert BsonValue to .NET object
    /// </summary>
    private static object MapToDotNetValue(BsonValue bsonValue)
    {
        if (bsonValue == null || bsonValue.IsBsonNull)
            return null!;

        return bsonValue.BsonType switch
        {
            BsonType.Document => ((BsonDocument)bsonValue).ToDictionary(),
            BsonType.Array => ((BsonArray)bsonValue).Select(MapToDotNetValue).ToList(),
            BsonType.String => bsonValue.AsString,
            BsonType.Int32 => bsonValue.AsInt32,
            BsonType.Int64 => bsonValue.AsInt64,
            BsonType.Double => bsonValue.AsDouble,
            BsonType.Decimal128 => (decimal)bsonValue.AsDecimal128,
            BsonType.Boolean => bsonValue.AsBoolean,
            BsonType.DateTime => bsonValue.ToUniversalTime(),
            BsonType.ObjectId => bsonValue.AsObjectId.ToString(),
            BsonType.Binary => bsonValue.AsBsonBinaryData.Bytes,
            _ => BsonTypeMapper.MapToDotNetValue(bsonValue)
        };
    }

    /// <summary>
    /// Convert list of BsonDocuments to list of Dictionaries
    /// </summary>
    public static List<Dictionary<string, object>> ToDictionaryList(this IEnumerable<BsonDocument> documents)
    {
        return documents?.Select(ToDictionary).ToList() ?? new List<Dictionary<string, object>>();
    }
}
