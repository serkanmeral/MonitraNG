using System.Text.Json;

namespace MngOperations.Application.Utilities;

public static class WorkItemDataHelper
{
    public static string? GetString(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value == null)
            return null;

        return value switch
        {
            string s => s,
            JsonElement el when el.ValueKind == JsonValueKind.String => el.GetString(),
            _ => value.ToString()
        };
    }

    private static readonly string[] PersonIdProps = { "__dataId", "_id", "id", "userId" };

    /// <summary>
    /// Person referans alanı (author/actor/createdBy ...) DG okuma sırasında tam @users nesnesine
    /// genişleyebilir. Düz id (string) ise aynen, genişletilmiş nesne ise içindeki id'yi (__dataId/id)
    /// döndürür; aksi halde null.
    /// </summary>
    public static string? GetPersonRefId(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value == null)
            return null;

        if (value is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.String)
                return el.GetString();
            if (el.ValueKind == JsonValueKind.Object)
            {
                foreach (var n in PersonIdProps)
                {
                    if (el.TryGetProperty(n, out var idEl)
                        && idEl.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(idEl.GetString()))
                    {
                        return idEl.GetString();
                    }
                }
                return null;
            }
            return null;
        }

        return value.ToString();
    }

    /// <summary>
    /// Genişletilmiş person nesnesinden görünen ad (firstName + lastName, yoksa username) üretir;
    /// alan düz id (string) ise veya nesne değilse null döner (dizin çözümü için).
    /// </summary>
    public static string? GetPersonRefName(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value)
            || value is not JsonElement { ValueKind: JsonValueKind.Object } el)
        {
            return null;
        }

        string? Prop(string n) =>
            el.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

        var full = $"{Prop("firstName")} {Prop("lastName")}".Trim();
        if (!string.IsNullOrWhiteSpace(full))
            return full;

        var username = Prop("username");
        return string.IsNullOrWhiteSpace(username) ? null : username;
    }

    public static bool? GetBool(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value == null)
            return null;

        return value switch
        {
            bool b => b,
            JsonElement el when el.ValueKind == JsonValueKind.True => true,
            JsonElement el when el.ValueKind == JsonValueKind.False => false,
            _ => null
        };
    }

    public static DateTime? GetDateTime(IReadOnlyDictionary<string, object?> data, string key)
    {
        var raw = GetString(data, key);
        if (string.IsNullOrEmpty(raw))
            return null;

        return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt
            : null;
    }

    public static string GetDataId(IReadOnlyDictionary<string, object?> data) =>
        GetString(data, "__dataId") ?? string.Empty;

    public static IReadOnlyList<string> GetStringList(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value == null)
            return Array.Empty<string>();

        if (value is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Array)
            {
                return el.EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToList();
            }

            if (el.ValueKind == JsonValueKind.String)
            {
                var single = el.GetString();
                return string.IsNullOrWhiteSpace(single) ? Array.Empty<string>() : new[] { single };
            }
        }

        if (value is IEnumerable<object?> list && value is not string)
        {
            return list
                .Select(v => v?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();
        }

        if (value is string s && !string.IsNullOrWhiteSpace(s))
            return new[] { s };

        return Array.Empty<string>();
    }

    /// <summary>Core kolon veya <see cref="WorkItemCoreFields.ExtraFieldsKey"/> içinden değer okur.</summary>
    public static object? GetFieldValue(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (data.TryGetValue(key, out var top) && !IsNullOrEmptyValue(top))
            return top;

        return TryGetFromExtraFields(data, key);
    }

    public static Dictionary<string, object?> CloneExtraFieldsDictionary(IReadOnlyDictionary<string, object?> target)
    {
        if (!target.TryGetValue(WorkItemCoreFields.ExtraFieldsKey, out var bucket) || bucket == null)
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        return ParseExtraFieldsBucket(bucket);
    }

    public static IReadOnlyDictionary<string, object?>? ReadExtraFields(
        IReadOnlyDictionary<string, object?> data)
    {
        if (!data.TryGetValue(WorkItemCoreFields.ExtraFieldsKey, out var bucket) || bucket == null)
            return null;

        var parsed = ParseExtraFieldsBucket(bucket);
        return parsed.Count > 0 ? parsed : null;
    }

    private static object? TryGetFromExtraFields(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(WorkItemCoreFields.ExtraFieldsKey, out var bucket) || bucket == null)
            return null;

        return bucket switch
        {
            JsonElement el when el.ValueKind == JsonValueKind.Object
                && el.TryGetProperty(key, out var prop) => DeserializeJsonValue(prop),
            IReadOnlyDictionary<string, object?> dict when dict.TryGetValue(key, out var v) => v,
            Dictionary<string, object?> dict when dict.TryGetValue(key, out var v) => v,
            _ => null
        };
    }

    private static Dictionary<string, object?> ParseExtraFieldsBucket(object bucket)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (bucket is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
                result[prop.Name] = DeserializeJsonValue(prop.Value);
            return result;
        }

        if (bucket is IReadOnlyDictionary<string, object?> readOnly)
        {
            foreach (var kv in readOnly)
                result[kv.Key] = kv.Value;
            return result;
        }

        if (bucket is Dictionary<string, object?> dict)
        {
            foreach (var kv in dict)
                result[kv.Key] = kv.Value;
        }

        return result;
    }

    private static object? DeserializeJsonValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => JsonSerializer.Deserialize<object?>(element.GetRawText())
        };

    private static bool IsNullOrEmptyValue(object? value) =>
        value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            JsonElement el when el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonElement el when el.ValueKind == JsonValueKind.String => string.IsNullOrWhiteSpace(el.GetString()),
            _ => false
        };
}
