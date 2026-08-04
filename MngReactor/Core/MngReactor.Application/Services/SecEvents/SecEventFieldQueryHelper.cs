using System.Text.Json;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Application.Services.SecEvents;

/// <summary>
/// Resolves catalog field names to storage paths and parses fieldFilters query JSON.
/// </summary>
public static class SecEventFieldQueryHelper
{
    public const int MaxClauses = 20;

    private static readonly HashSet<string> AllowedOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq", "neq", "in", "contains", "prefix"
    };

    /// <summary>Core fields stored at document root (not under fields bag).</summary>
    private static readonly HashSet<string> TopLevelFields = new(StringComparer.Ordinal)
    {
        "event.action",
        "event.outcome",
        "event.code",
        "actor.user",
        "network.srcIp",
        "network.dstIp",
        "network.dstPort",
        "network.protocol",
    };

    /// <summary>Core fields persisted under document <c>fields</c> bag (see CollectExtraFields).</summary>
    private static readonly HashSet<string> BagCoreFields = new(StringComparer.Ordinal)
    {
        "message",
        "tags",
        "event.category",
        "event.severity",
    };

    public static bool IsAllowedField(string? field)
    {
        var name = (field ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
            return false;
        if (TopLevelFields.Contains(name) || BagCoreFields.Contains(name))
            return true;
        return SecEventTargetFieldCatalog.IsCustomField(name);
    }

    public static bool IsBagField(string field)
    {
        var name = field.Trim();
        return BagCoreFields.Contains(name) || SecEventTargetFieldCatalog.IsCustomField(name);
    }

    public static string? TopLevelPath(string field)
    {
        var name = field.Trim();
        return TopLevelFields.Contains(name) ? name : null;
    }

    public static string NormalizeOp(string? op)
    {
        var o = (op ?? "eq").Trim().ToLowerInvariant();
        return AllowedOps.Contains(o) ? o : "eq";
    }

    /// <summary>
    /// Parses <c>fieldFilters</c> query JSON:
    /// <c>[{"field":"custom.x","op":"eq","value":"1"},...]</c>
    /// </summary>
    public static IReadOnlyList<SecEventFieldFilterClause> ParseFieldFiltersJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var list = new List<SecEventFieldFilterClause>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (list.Count >= MaxClauses)
                    break;
                if (el.ValueKind != JsonValueKind.Object)
                    continue;

                var field = ReadString(el, "field") ?? ReadString(el, "Field");
                var op = ReadString(el, "op") ?? ReadString(el, "Op") ?? "eq";
                var value = ReadString(el, "value") ?? ReadString(el, "Value");
                if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(value))
                    continue;
                if (!IsAllowedField(field))
                    continue;

                list.Add(new SecEventFieldFilterClause
                {
                    Field = field.Trim(),
                    Op = NormalizeOp(op),
                    Value = value.Trim()
                });
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ReadString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop))
            return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }
}
