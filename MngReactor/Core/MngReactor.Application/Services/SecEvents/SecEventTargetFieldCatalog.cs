using System.Text.RegularExpressions;
using MngReactor.Application.Contracts.SecEvents;

namespace MngReactor.Application.Services.SecEvents;

/// <summary>
/// Canonical SIEM field catalog: parse-rule extract targets and (later) smart query builders
/// share this single source of truth. Domain-specific extensions use <c>custom.*</c>.
/// </summary>
public static class SecEventTargetFieldCatalog
{
    /// <summary>custom.slug — lowercase letter start, then letters/digits/underscore.</summary>
    public static readonly Regex CustomFieldPattern = new(
        @"^custom\.[a-z][a-z0-9_]{0,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyList<SecEventTargetFieldDefinition> Fields =
    [
        Def("event.action", "Event action", "event", "keyword",
            extract: ["constant", "event_data", "regex", "json_path", "kv"],
            queryOps: ["eq", "neq", "in", "prefix"],
            description: "Normalized meaning of the event (e.g. login_failed, rdp.logon)."),
        Def("event.code", "Event code", "event", "keyword",
            extract: ["event_data", "constant", "regex", "json_path"],
            queryOps: ["eq", "in"],
            description: "Vendor / Windows Event ID (e.g. 4625, 24)."),
        Def("event.outcome", "Event outcome", "event", "keyword",
            extract: ["constant", "event_data", "regex"],
            queryOps: ["eq", "in"],
            description: "success | failure | unknown"),
        Def("event.category", "Event category", "event", "keyword",
            extract: ["constant", "event_data", "regex"],
            queryOps: ["eq", "in"],
            description: "High-level category (e.g. authentication, network)."),
        Def("event.severity", "Event severity", "event", "keyword",
            extract: ["constant", "event_data", "regex"],
            queryOps: ["eq", "in"],
            description: "Optional severity hint for downstream alerting."),
        Def("actor.user", "Actor user", "actor", "keyword",
            extract: ["event_data", "regex", "json_path", "kv", "constant"],
            queryOps: ["eq", "neq", "in", "contains", "prefix"],
            description: "User / account that performed or was targeted by the action."),
        Def("network.srcIp", "Source IP", "network", "ip",
            extract: ["event_data", "regex", "json_path", "kv", "constant"],
            queryOps: ["eq", "neq", "in", "cidr"],
            description: "Client / source address."),
        Def("network.dstIp", "Destination IP", "network", "ip",
            extract: ["event_data", "regex", "json_path", "kv", "constant"],
            queryOps: ["eq", "neq", "in", "cidr"],
            description: "Server / destination address."),
        Def("network.dstPort", "Destination port", "network", "port",
            extract: ["event_data", "regex", "json_path", "kv", "constant"],
            queryOps: ["eq", "neq", "in", "range"],
            description: "Destination TCP/UDP port."),
        Def("network.protocol", "Protocol", "network", "keyword",
            extract: ["constant", "event_data", "regex"],
            queryOps: ["eq", "in"],
            description: "Network protocol when known (tcp, udp, …)."),
        Def("message", "Message", "message", "text",
            extract: ["event_data", "regex", "json_path", "kv", "constant"],
            queryOps: ["contains", "prefix", "eq"],
            description: "Human-readable message or extracted detail text."),
        Def("tags", "Tags", "tags", "keyword",
            extract: ["constant"],
            queryOps: ["eq", "in", "contains"],
            description: "Free-form tags for correlation / filtering."),
    ];

    private static readonly HashSet<string> NameSet =
        new(Fields.Select(f => f.Name), StringComparer.Ordinal);

    public static IReadOnlyList<SecEventTargetFieldDefinition> All => Fields;

    public static IReadOnlySet<string> AllowedNames => NameSet;

    public static bool IsCustomField(string? field)
    {
        var name = (field ?? string.Empty).Trim().ToLowerInvariant();
        return CustomFieldPattern.IsMatch(name);
    }

    public static bool IsAllowed(string? field)
    {
        var name = (field ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return NameSet.Contains(name) || IsCustomField(name);
    }

    /// <summary>
    /// Accepts <c>custom.session_id</c> or bare <c>session_id</c> / <c>sessionId</c> (normalized).
    /// </summary>
    public static string NormalizeCustomFieldName(string? raw)
    {
        var s = (raw ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_');
        if (s.StartsWith("custom.", StringComparison.Ordinal))
            s = s["custom.".Length..];
        s = new string(s.Where(c => char.IsAsciiLetterOrDigit(c) || c == '_').ToArray());
        while (s.Contains("__", StringComparison.Ordinal))
            s = s.Replace("__", "_", StringComparison.Ordinal);
        s = s.Trim('_');
        if (string.IsNullOrEmpty(s))
            throw new ArgumentException("Custom field slug is required (e.g. session_id).");
        if (!char.IsAsciiLetter(s[0]))
            throw new ArgumentException("Custom field slug must start with a letter.");
        if (s.Length > 64)
            throw new ArgumentException("Custom field slug must be at most 64 characters.");
        var full = "custom." + s;
        if (!CustomFieldPattern.IsMatch(full))
            throw new ArgumentException($"Invalid custom field name '{full}'.");
        return full;
    }

    public static string LabelFromName(string name)
    {
        var n = name.Trim();
        if (n.StartsWith("custom.", StringComparison.OrdinalIgnoreCase))
            return n["custom.".Length..];
        return n;
    }

    public static SecEventTargetFieldDefinition ToCustomDefinition(
        string name,
        string? label = null,
        string valueType = "keyword",
        string? description = null) =>
        new()
        {
            Name = name,
            Label = string.IsNullOrWhiteSpace(label) ? LabelFromName(name) : label.Trim(),
            Group = "custom",
            ValueType = string.IsNullOrWhiteSpace(valueType) ? "keyword" : valueType.Trim().ToLowerInvariant(),
            Description = description,
            ExtractTypes = ["event_data", "regex", "json_path", "kv", "constant"],
            QueryOperators = ["eq", "neq", "in", "contains", "prefix"],
            Queryable = true,
            WizardSelectable = true,
            IsCustom = true
        };

    public static SecEventTargetFieldCatalogResponse ToResponse(
        IEnumerable<SecEventTargetFieldDefinition>? customFields = null)
    {
        var list = Fields.Select(CloneCore).ToList();
        if (customFields is not null)
        {
            foreach (var c in customFields.OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(c.Name) || !IsCustomField(c.Name))
                    continue;
                list.Add(new SecEventTargetFieldDefinition
                {
                    Name = c.Name,
                    Label = string.IsNullOrWhiteSpace(c.Label) ? LabelFromName(c.Name) : c.Label,
                    Group = "custom",
                    ValueType = string.IsNullOrWhiteSpace(c.ValueType) ? "keyword" : c.ValueType,
                    Description = c.Description,
                    ExtractTypes = c.ExtractTypes?.Count > 0
                        ? c.ExtractTypes.ToList()
                        : ["event_data", "regex", "json_path", "kv", "constant"],
                    QueryOperators = c.QueryOperators?.Count > 0
                        ? c.QueryOperators.ToList()
                        : ["eq", "neq", "in", "contains", "prefix"],
                    Queryable = c.Queryable,
                    WizardSelectable = c.WizardSelectable,
                    IsCustom = true
                });
            }
        }

        return new SecEventTargetFieldCatalogResponse
        {
            Version = "1",
            Fields = list
        };
    }

    private static SecEventTargetFieldDefinition CloneCore(SecEventTargetFieldDefinition f) =>
        new()
        {
            Name = f.Name,
            Label = f.Label,
            Group = f.Group,
            ValueType = f.ValueType,
            Description = f.Description,
            ExtractTypes = f.ExtractTypes.ToList(),
            QueryOperators = f.QueryOperators.ToList(),
            Queryable = f.Queryable,
            WizardSelectable = f.WizardSelectable,
            IsCustom = false
        };

    private static SecEventTargetFieldDefinition Def(
        string name,
        string label,
        string group,
        string valueType,
        string[] extract,
        string[] queryOps,
        string description) =>
        new()
        {
            Name = name,
            Label = label,
            Group = group,
            ValueType = valueType,
            Description = description,
            ExtractTypes = extract.ToList(),
            QueryOperators = queryOps.ToList(),
            Queryable = true,
            WizardSelectable = !string.Equals(name, "event.action", StringComparison.Ordinal)
                               && !string.Equals(name, "tags", StringComparison.Ordinal),
            IsCustom = false
        };
}
