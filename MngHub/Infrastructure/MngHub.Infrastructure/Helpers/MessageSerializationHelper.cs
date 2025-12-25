using System.Text.Json;

namespace MngHub.Infrastructure.Helpers;

/// <summary>
/// Helper class for message serialization/deserialization
/// </summary>
public static class MessageSerializationHelper
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Serialize object to JSON string
    /// </summary>
    public static string Serialize(object? value, bool indented = false)
    {
        if (value == null)
            return string.Empty;

        var options = indented
            ? new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            : DefaultOptions;

        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>
    /// Deserialize JSON string to object
    /// </summary>
    public static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, DefaultOptions);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Deserialize JSON string to object (returns object type)
    /// </summary>
    public static object? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<object>(json, DefaultOptions);
        }
        catch
        {
            return null;
        }
    }
}

