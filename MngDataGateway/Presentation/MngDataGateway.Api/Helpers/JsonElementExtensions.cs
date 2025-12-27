using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MngDataGateway.Api.Helpers;

/// <summary>
/// Extension methods for JsonElement conversion
/// </summary>
public static class JsonElementExtensions
{
    /// <summary>
    /// Convert JsonElement to Dictionary with proper type preservation
    /// </summary>
    public static Dictionary<string, object> ToDictionary(this JsonElement element)
    {
        var dictionary = new Dictionary<string, object>();

        if (element.ValueKind != JsonValueKind.Object)
            return dictionary;

        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = GetValue(property.Value);
        }

        return dictionary;
    }

    /// <summary>
    /// Convert JsonElement array to List of Dictionaries
    /// </summary>
    public static List<Dictionary<string, object>> ToDictionaryList(this JsonElement element)
    {
        var list = new List<Dictionary<string, object>>();

        if (element.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var itemElement in element.EnumerateArray())
        {
            list.Add(itemElement.ToDictionary());
        }

        return list;
    }

    /// <summary>
    /// Get typed value from JsonElement
    /// </summary>
    private static object GetValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Array => element.EnumerateArray().Select(GetValue).ToList(),
            JsonValueKind.Object => element.ToDictionary(),
            _ => element.ToString()!
        };
    }

    /// <summary>
    /// Check if JsonElement has a property
    /// </summary>
    public static bool HasProperty(this JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out _);
    }

    /// <summary>
    /// Get property value as string, or default if not found
    /// </summary>
    public static string? GetPropertyString(this JsonElement element, string propertyName, string? defaultValue = null)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
        {
            return property.ValueKind == JsonValueKind.String ? property.GetString() : defaultValue;
        }
        return defaultValue;
    }
}

