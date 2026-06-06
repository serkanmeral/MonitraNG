using MongoDB.Bson;

namespace MngAlarm.Infrastructure.Services;

/// <summary>
/// MongoDB context values (BsonArray, BsonDocument, …) are not always JSON-serializable for API responses.
/// </summary>
internal static class AlarmContextApiNormalizer
{
    public static Dictionary<string, object?> ForApi(Dictionary<string, object?> context)
    {
        if (context.Count == 0)
            return context;

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in context)
            result[key] = ToJsonSafe(value);

        return result;
    }

    private static object? ToJsonSafe(object? value)
    {
        if (value is null)
            return null;

        if (value is BsonValue bson)
            return ToJsonSafe(BsonTypeMapper.MapToDotNetValue(bson));

        if (value is string or bool or int or long or float or double or decimal)
            return value;

        if (value is DateTime dt)
            return dt.ToUniversalTime().ToString("O");

        if (value is IDictionary<string, object?> typedDict)
        {
            return typedDict.ToDictionary(
                kv => kv.Key,
                kv => ToJsonSafe(kv.Value),
                StringComparer.Ordinal);
        }

        if (value is IDictionary<string, object> objectDict)
        {
            return objectDict.ToDictionary(
                kv => kv.Key,
                kv => ToJsonSafe(kv.Value),
                StringComparer.Ordinal);
        }

        if (value is System.Collections.IDictionary legacyDict)
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (System.Collections.DictionaryEntry entry in legacyDict)
            {
                if (entry.Key is string key)
                    map[key] = ToJsonSafe(entry.Value);
            }

            return map;
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
                list.Add(ToJsonSafe(item));
            return list;
        }

        return value;
    }
}
