using System.Text;
using System.Xml.Linq;

namespace MngLogs.Agent.EventLog;

/// <summary>
/// Builds SIEM-friendly message / fields / raw fragments from Windows Event Log
/// rendered text, positional properties, and Event XML (EventData / UserData).
/// </summary>
public static class WindowsEventPayloadBuilder
{
    public const int DefaultMessageMaxChars = 4000;
    public const int DefaultEventDataTextMaxChars = 16000;
    public const int DefaultXmlMaxChars = 32768;
    public const int DefaultPropertyValueMaxChars = 8192;

    public sealed record Payload(
        string Message,
        IReadOnlyList<string?> Properties,
        IReadOnlyDictionary<string, string> EventData,
        string? EventDataText,
        string? Xml);

    public static Payload Build(
        int eventId,
        string? formattedMessage,
        IReadOnlyList<string?> properties,
        string? eventXml,
        int messageMaxChars = DefaultMessageMaxChars)
    {
        var props = NormalizeProperties(properties);
        var eventData = ParseNamedData(eventXml);
        MergePositionalIntoEventData(eventData, props);
        var eventDataText = BuildEventDataText(eventData, props);
        var xml = Truncate(eventXml, DefaultXmlMaxChars);

        var message = FirstNonEmpty(
            formattedMessage,
            eventDataText,
            $"EventID {eventId}")!;

        return new Payload(
            Truncate(message.Trim(), messageMaxChars)!,
            props,
            eventData,
            Truncate(eventDataText, DefaultEventDataTextMaxChars),
            xml);
    }

    /// <summary>
    /// Parses &lt;EventData&gt;/&lt;Data&gt; and &lt;UserData&gt; leaf values from EventRecord.ToXml().
    /// </summary>
    public static Dictionary<string, string> ParseNamedData(string? eventXml)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(eventXml))
            return map;

        try
        {
            var doc = XDocument.Parse(eventXml, LoadOptions.None);
            var root = doc.Root;
            if (root is null)
                return map;

            var unnamed = 0;
            foreach (var data in root.Descendants().Where(e => e.Name.LocalName == "Data"))
            {
                var value = NormalizeValue(data.Value);
                if (value is null)
                    continue;

                var name = data.Attribute("Name")?.Value?.Trim();
                if (string.IsNullOrEmpty(name))
                    name = $"Data_{unnamed++}";

                // Keep first non-empty; later duplicates get a suffix.
                if (!map.ContainsKey(name))
                    map[name] = value;
                else if (!string.Equals(map[name], value, StringComparison.Ordinal))
                    map[$"{name}_{map.Count}"] = value;
            }

            // UserData often uses custom element names (not <Data>).
            var userData = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "UserData");
            if (userData is not null)
            {
                foreach (var el in userData.Descendants().Where(e => !e.HasElements && e != userData))
                {
                    var value = NormalizeValue(el.Value);
                    if (value is null)
                        continue;

                    var name = el.Name.LocalName;
                    if (string.IsNullOrEmpty(name) || name is "Data")
                        continue;

                    if (!map.ContainsKey(name))
                        map[name] = value;
                }
            }
        }
        catch
        {
            // Malformed provider XML — positional properties still carry values.
        }

        return map;
    }

    public static void ApplyToFields(IDictionary<string, object?> fields, Payload payload)
    {
        if (payload.Properties.Count > 0)
            fields["properties"] = payload.Properties.Select(p => (object?)p).ToArray();

        if (payload.EventData.Count > 0)
            fields["eventData"] = payload.EventData.ToDictionary(
                kv => kv.Key,
                kv => (object?)kv.Value,
                StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(payload.EventDataText))
            fields["eventDataText"] = payload.EventDataText;

        if (!string.IsNullOrWhiteSpace(payload.Xml))
            fields["xml"] = payload.Xml;
    }

    private static void MergePositionalIntoEventData(
        IDictionary<string, string> eventData,
        IReadOnlyList<string?> properties)
    {
        for (var i = 0; i < properties.Count; i++)
        {
            var value = properties[i];
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var key = $"Data_{i}";
            if (!eventData.ContainsKey(key))
                eventData[key] = value;
        }
    }

    private static string? BuildEventDataText(
        IReadOnlyDictionary<string, string> eventData,
        IReadOnlyList<string?> properties)
    {
        // Prefer named EventData values in document order; fall back to properties.
        var parts = new List<string>();
        foreach (var value in eventData.Values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add(value.Trim());
        }

        if (parts.Count == 0)
        {
            foreach (var p in properties)
            {
                if (!string.IsNullOrWhiteSpace(p))
                    parts.Add(p.Trim());
            }
        }

        if (parts.Count == 0)
            return null;

        // De-dupe consecutive identical fragments (common with Name+positional overlap).
        var sb = new StringBuilder();
        string? last = null;
        foreach (var part in parts)
        {
            if (string.Equals(part, last, StringComparison.Ordinal))
                continue;
            if (sb.Length > 0)
                sb.Append(" | ");
            sb.Append(part);
            last = part;
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    private static IReadOnlyList<string?> NormalizeProperties(IReadOnlyList<string?> properties)
    {
        if (properties.Count == 0)
            return [];

        var list = new string?[properties.Count];
        for (var i = 0; i < properties.Count; i++)
            list[i] = Truncate(NormalizeValue(properties[i]), DefaultPropertyValueMaxChars);
        return list;
    }

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        return null;
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null)
            return null;
        if (max <= 0 || value.Length <= max)
            return value;
        return value[..max];
    }
}
