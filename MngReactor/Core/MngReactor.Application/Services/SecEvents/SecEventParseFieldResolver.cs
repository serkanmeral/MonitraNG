using System.Globalization;
using System.Text.Json;

namespace MngReactor.Application.Services.SecEvents;

/// <summary>
/// Canonical field resolution for Windows Event Log payloads from:
/// agent raw root, OpenSearch <c>fields.*</c>, and legacy EventData casing.
/// </summary>
public static class SecEventParseFieldResolver
{
    public static string? ReadPath(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || root.ValueKind != JsonValueKind.Object)
            return null;

        return ReadJsonPath(root, path);
    }

    public static string? ReadFirst(JsonElement root, params string[] paths)
    {
        foreach (var path in paths)
        {
            var v = ReadPath(root, path);
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        return null;
    }

    public static int? ReadEventId(JsonElement root, int? explicitEventId = null)
    {
        if (explicitEventId is > 0)
            return explicitEventId;

        var s = ReadFirst(
            root,
            "EventID",
            "EventId",
            "eventId",
            "fields.EventID",
            "fields.EventId",
            "fields.eventId",
            "event.code");
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    public static string? ReadChannel(JsonElement root, string? explicitChannel = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitChannel))
            return explicitChannel.Trim();

        return ReadFirst(root, "Channel", "channel", "fields.Channel", "fields.channel");
    }

    public static string? ReadMessage(JsonElement root, string? explicitMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitMessage))
            return explicitMessage;

        return ReadFirst(
            root,
            "message",
            "MESSAGE",
            "eventDataText",
            "fields.message",
            "fields.MESSAGE",
            "fields.eventDataText");
    }

    /// <summary>
    /// Resolves EventData key <paramref name="from"/> across agent / OpenSearch / legacy shapes.
    /// </summary>
    public static string? ReadEventData(JsonElement root, string from)
    {
        if (string.IsNullOrWhiteSpace(from))
            return null;

        from = from.Trim();
        if (from.Contains('.', StringComparison.Ordinal))
        {
            // Explicit path — try as-is plus fields-prefixed alias.
            return ReadFirst(root, from, from.StartsWith("fields.", StringComparison.OrdinalIgnoreCase)
                ? from
                : $"fields.{from}");
        }

        return ReadFirst(
            root,
            $"EventData.{from}",
            $"eventData.{from}",
            $"fields.EventData.{from}",
            $"fields.eventData.{from}",
            from);
    }

    public static bool IsWindowsEventLogType(string? sourceType)
    {
        var t = (sourceType ?? string.Empty).Trim().ToLowerInvariant();
        return t is "windows-eventlog" or "ad" or "endpoint" or "windows";
    }

    /// <summary>
    /// SourceProduct match: exact product, or rule wants "windows" while event is Windows Event Log
    /// (agent packages use names like rdp-session / application-signals).
    /// </summary>
    public static bool MatchesSourceProduct(
        IReadOnlyList<string> ruleProducts,
        string? eventProduct,
        string? eventSourceType)
    {
        if (ruleProducts.Count == 0)
            return true;

        var product = (eventProduct ?? string.Empty).Trim().ToLowerInvariant();
        if (ruleProducts.Any(p => string.Equals(p, product, StringComparison.OrdinalIgnoreCase)))
            return true;

        var wantsWindows = ruleProducts.Any(p =>
            string.Equals(p, "windows", StringComparison.OrdinalIgnoreCase));
        if (wantsWindows && IsWindowsEventLogType(eventSourceType))
            return true;

        var wantsLinux = ruleProducts.Any(p =>
            string.Equals(p, "linux-journal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(p, "linux-syslog", StringComparison.OrdinalIgnoreCase)
            || string.Equals(p, "linux-auth", StringComparison.OrdinalIgnoreCase));
        if (!wantsLinux)
            return false;

        var type = (eventSourceType ?? string.Empty).Trim().ToLowerInvariant();
        return type is "linux-journal" or "linux";
    }

    /// <summary>
    /// SourceType match: empty event type does not reject; windows-eventlog is compatible with ad/endpoint.
    /// </summary>
    public static bool MatchesSourceType(IReadOnlyList<string>? ruleTypes, string? eventSourceType)
    {
        if (ruleTypes is not { Count: > 0 })
            return true;

        var type = (eventSourceType ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(type))
            return true;

        if (ruleTypes.Any(t => string.Equals(t, type, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Agent uses windows-eventlog; older seeds use ad/endpoint.
        if (type == "windows-eventlog"
            && ruleTypes.Any(t => t.Equals("ad", StringComparison.OrdinalIgnoreCase)
                                  || t.Equals("endpoint", StringComparison.OrdinalIgnoreCase)
                                  || t.Equals("windows", StringComparison.OrdinalIgnoreCase)))
            return true;

        if ((type is "ad" or "endpoint" or "windows")
            && ruleTypes.Any(t => t.Equals("windows-eventlog", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (type == "linux-journal"
            && ruleTypes.Any(t => t.Equals("linux", StringComparison.OrdinalIgnoreCase)
                                  || t.Equals("endpoint", StringComparison.OrdinalIgnoreCase)
                                  || t.Equals("linux-journal", StringComparison.OrdinalIgnoreCase)))
            return true;

        if ((type is "linux" or "endpoint")
            && ruleTypes.Any(t => t.Equals("linux-journal", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    public static Dictionary<string, string> DiscoverEventDataKeys(JsonElement root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var containerPath in new[] { "eventData", "EventData", "fields.eventData", "fields.EventData" })
        {
            var el = Navigate(root, containerPath);
            if (el is null)
                continue;
            MergeEventDataObject(el.Value, map);
        }

        if (map.Count == 0)
        {
            foreach (var kv in DeriveEventDataFromMessage(ReadMessage(root)))
                map[kv.Key] = kv.Value;
        }

        return map;
    }

    private static void MergeEventDataObject(JsonElement el, Dictionary<string, string> map)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                var value = Scalar(prop.Value);
                if (value is null)
                    continue;
                if (!map.ContainsKey(prop.Name))
                    map[prop.Name] = value;
            }
            return;
        }

        if (el.ValueKind != JsonValueKind.String)
            return;

        var text = el.GetString();
        if (string.IsNullOrWhiteSpace(text) || !text!.TrimStart().StartsWith('{'))
            return;

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                MergeEventDataObject(doc.RootElement, map);
        }
        catch (JsonException)
        {
            /* ignore */
        }
    }

    /// <summary>RDP LocalSessionManager-style message lines → synthetic EventData keys.</summary>
    public static Dictionary<string, string> DeriveEventDataFromMessage(string? message)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(message))
            return map;

        TryCapture(message, @"^\s*User:\s*(.+?)\s*$", "User", map);
        TryCapture(message, @"^\s*Session ID:\s*(.+?)\s*$", "SessionID", map);
        TryCapture(message, @"^\s*Source Network Address:\s*(.+?)\s*$", "Address", map);
        return map;
    }

    private static void TryCapture(
        string message,
        string pattern,
        string key,
        Dictionary<string, string> map)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            message,
            pattern,
            System.Text.RegularExpressions.RegexOptions.Multiline
            | System.Text.RegularExpressions.RegexOptions.IgnoreCase
            | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (m.Success && m.Groups.Count > 1)
        {
            var v = m.Groups[1].Value.Trim();
            if (v.Length > 0 && !map.ContainsKey(key))
                map[key] = v;
        }
    }

    public static string InferParseModeHint(IReadOnlyDictionary<string, string> eventData)
    {
        if (eventData.Count == 0)
            return "text";

        var named = eventData.Keys.Count(k =>
            !k.StartsWith("Data_", StringComparison.OrdinalIgnoreCase)
            && !k.StartsWith("param", StringComparison.OrdinalIgnoreCase));
        return named > 0 ? "field_map" : "text";
    }

    /// <summary>
    /// Build a canonical raw JSON object for preview/engine from an OpenSearch _source document.
    /// Flattens fields.* to root so EventData mapping works.
    /// </summary>
    public static Dictionary<string, object?> CanonicalRawFromOpenSearchSource(JsonElement source)
    {
        var raw = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (source.ValueKind != JsonValueKind.Object)
            return raw;

        if (source.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in fields.EnumerateObject())
                raw[prop.Name] = NormalizeFieldValue(prop.Name, prop.Value);
        }

        // Prefer message from top-level event.action only when fields.message missing.
        if (!raw.ContainsKey("message")
            && source.TryGetProperty("event", out var ev)
            && ev.ValueKind == JsonValueKind.Object
            && ev.TryGetProperty("action", out var action)
            && action.ValueKind == JsonValueKind.String)
        {
            var a = action.GetString();
            if (!string.IsNullOrWhiteSpace(a) && !string.Equals(a, "unknown", StringComparison.OrdinalIgnoreCase))
                raw["message"] = a;
        }

        if (source.TryGetProperty("rawPreview", out var rp) && rp.ValueKind == JsonValueKind.String)
        {
            var preview = rp.GetString();
            if (!string.IsNullOrWhiteSpace(preview) && preview!.TrimStart().StartsWith('{'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(preview);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (!raw.ContainsKey(prop.Name))
                                raw[prop.Name] = JsonElementToObject(prop.Value);
                        }

                        // Prefer journal MESSAGE as human-readable message when fields.message is absent.
                        if ((!raw.TryGetValue("message", out var existingMsg) || existingMsg is null
                             || string.IsNullOrWhiteSpace(existingMsg.ToString()))
                            && doc.RootElement.TryGetProperty("MESSAGE", out var msgEl)
                            && msgEl.ValueKind == JsonValueKind.String)
                        {
                            var msg = msgEl.GetString();
                            if (!string.IsNullOrWhiteSpace(msg))
                                raw["message"] = msg;
                        }
                    }
                }
                catch (JsonException)
                {
                    /* keep fields-only */
                }
            }
        }

        return raw;
    }

    private static object? NormalizeFieldValue(string name, JsonElement el)
    {
        if ((name.Equals("eventData", StringComparison.OrdinalIgnoreCase)
             || name.Equals("EventData", StringComparison.OrdinalIgnoreCase))
            && el.ValueKind == JsonValueKind.String)
        {
            var text = el.GetString();
            if (!string.IsNullOrWhiteSpace(text) && text!.TrimStart().StartsWith('{'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        return JsonElementToObject(doc.RootElement);
                }
                catch (JsonException)
                {
                    /* fall through */
                }
            }
        }

        return JsonElementToObject(el);
    }

    private static object? JsonElementToObject(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.Object => el.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };

    private static JsonElement? Navigate(JsonElement root, string path)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = root;
        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;

            JsonElement next = default;
            var found = false;
            foreach (var prop in current.EnumerateObject())
            {
                if (!string.Equals(prop.Name, part, StringComparison.OrdinalIgnoreCase))
                    continue;
                next = prop.Value;
                found = true;
                break;
            }

            if (!found)
                return null;
            current = next;
        }

        return current;
    }

    private static string? ReadJsonPath(JsonElement root, string path)
    {
        var current = Navigate(root, path);
        return current is null ? null : Scalar(current.Value);
    }

    private static string? Scalar(JsonElement current) =>
        current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => current.GetRawText()
        };
}
