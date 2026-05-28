using System.Text.Json;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Utilities;

namespace MngOperations.Application.FieldBehaviors;

/// <summary>
/// Workspace <c>settings.fieldPolicies</c> — UI sözleşmesi ile uyumlu parse ve runtime merge.
/// </summary>
public static class WorkspaceFieldPolicies
{
    public const string SettingsKey = "fieldPolicies";

    public sealed class PolicyClause
    {
        public required string FieldKey { get; init; }
        public required string Operator { get; init; }
        public object? Value { get; init; }
    }

    public sealed class WorkspaceFieldPolicy
    {
        public required string Id { get; init; }
        public required string Kind { get; init; }
        public required string Scope { get; init; }
        public IReadOnlyList<PolicyClause> Clauses { get; init; } = Array.Empty<PolicyClause>();
        public bool? Visible { get; init; }
        public bool? Readonly { get; init; }
        public object? DefaultValue { get; init; }
    }

    public sealed class WorkspaceFieldPoliciesBlob
    {
        public IReadOnlyDictionary<string, IReadOnlyList<WorkspaceFieldPolicy>> PoliciesByField { get; init; }
            = new Dictionary<string, IReadOnlyList<WorkspaceFieldPolicy>>(StringComparer.OrdinalIgnoreCase);
    }

    public static WorkspaceFieldPoliciesBlob Parse(JsonElement? settings)
    {
        if (settings is not { ValueKind: JsonValueKind.Object })
            return new WorkspaceFieldPoliciesBlob();

        if (!settings.Value.TryGetProperty(SettingsKey, out var blobEl)
            || blobEl.ValueKind != JsonValueKind.Object)
        {
            return new WorkspaceFieldPoliciesBlob();
        }

        var policiesByField = new Dictionary<string, IReadOnlyList<WorkspaceFieldPolicy>>(StringComparer.OrdinalIgnoreCase);

        if (blobEl.TryGetProperty("policiesByField", out var byFieldEl)
            && byFieldEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in byFieldEl.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Array)
                    continue;

                var policies = prop.Value.EnumerateArray()
                    .Select(ParsePolicy)
                    .Where(p => p != null)
                    .Cast<WorkspaceFieldPolicy>()
                    .ToList();

                if (policies.Count > 0)
                    policiesByField[prop.Name] = policies;
            }
        }

        MigrateLegacyVisibility(blobEl, policiesByField);

        return new WorkspaceFieldPoliciesBlob { PoliciesByField = policiesByField };
    }

    public static IReadOnlyList<FieldBehaviorDto> ResolveBehaviorLayers(
        string fieldName,
        WorkspaceFieldPoliciesBlob blob,
        IReadOnlyDictionary<string, object?> workItem,
        PolicyEvaluationHints? hints = null)
    {
        if (!blob.PoliciesByField.TryGetValue(fieldName, out var policies))
            return Array.Empty<FieldBehaviorDto>();

        var layers = new List<FieldBehaviorDto>();
        foreach (var policy in policies)
        {
            if (!PolicyApplies(policy, workItem, hints))
                continue;

            switch (policy.Kind.ToLowerInvariant())
            {
                case "visibility" when policy.Visible is bool visible:
                    layers.Add(new FieldBehaviorDto { Visible = visible });
                    break;
                case "readonly" when policy.Readonly is bool readOnly:
                    layers.Add(new FieldBehaviorDto { Readonly = readOnly });
                    break;
            }
        }

        return layers;
    }

    /// <summary>Create modunda eşleşen <c>defaultValue</c> politikaları; son eşleşen kazanır.</summary>
    public static IReadOnlyDictionary<string, object?> ResolveDefaultValues(
        WorkspaceFieldPoliciesBlob blob,
        IReadOnlyDictionary<string, object?> workItem,
        PolicyEvaluationHints? hints = null)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fieldKey, policies) in blob.PoliciesByField)
        {
            foreach (var policy in policies)
            {
                if (!string.Equals(policy.Kind, "defaultValue", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!PolicyApplies(policy, workItem, hints))
                    continue;

                result[fieldKey] = policy.DefaultValue;
            }
        }

        return result;
    }

    public static IEnumerable<string> EnumerateFieldKeys(WorkspaceFieldPoliciesBlob blob) =>
        blob.PoliciesByField.Keys;

    public sealed class PolicyEvaluationHints
    {
        public string? StateId { get; init; }
        public string? TypeId { get; init; }
    }

    private static bool PolicyApplies(
        WorkspaceFieldPolicy policy,
        IReadOnlyDictionary<string, object?> workItem,
        PolicyEvaluationHints? hints)
    {
        if (!string.Equals(policy.Scope, "conditional", StringComparison.OrdinalIgnoreCase))
            return true;

        if (policy.Clauses.Count == 0)
            return false;

        return policy.Clauses.All(clause => EvaluateClause(clause, workItem, hints));
    }

    private static bool EvaluateClause(
        PolicyClause clause,
        IReadOnlyDictionary<string, object?> workItem,
        PolicyEvaluationHints? hints)
    {
        var actual = ResolveFieldValue(clause.FieldKey, workItem, hints);
        var op = clause.Operator.ToLowerInvariant();

        return op switch
        {
            "ne" or "neq" or "notequals" => !ValuesMatch(actual, clause.Value),
            _ => ValuesMatch(actual, clause.Value)
        };
    }

    private static object? ResolveFieldValue(
        string fieldKey,
        IReadOnlyDictionary<string, object?> workItem,
        PolicyEvaluationHints? hints)
    {
        var fromItem = WorkItemDataHelper.GetFieldValue(workItem, fieldKey);
        if (!IsEmpty(fromItem))
            return NormalizeValue(fromItem);

        if (hints == null)
            return null;

        if (string.Equals(fieldKey, "stateId", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(hints.StateId))
        {
            return hints.StateId;
        }

        if (string.Equals(fieldKey, "typeId", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(hints.TypeId))
        {
            return hints.TypeId;
        }

        return null;
    }

    private static bool ValuesMatch(object? actual, object? expected)
    {
        if (expected is IEnumerable<object?> enumerable && expected is not string)
        {
            foreach (var item in enumerable)
            {
                if (SingleValueEquals(actual, item))
                    return true;
            }

            return false;
        }

        if (expected is JsonElement el && el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                if (SingleValueEquals(actual, JsonElementToObject(item)))
                    return true;
            }

            return false;
        }

        return SingleValueEquals(actual, expected);
    }

    private static bool SingleValueEquals(object? actual, object? expected)
    {
        actual = NormalizeValue(actual);
        expected = NormalizeValue(expected);

        if (actual is null && expected is null)
            return true;

        if (actual is null || expected is null)
            return false;

        if (actual is bool ab && expected is bool eb)
            return ab == eb;

        if (actual is string a && expected is string b)
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        return string.Equals(actual.ToString(), expected.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEmpty(object? value) =>
        value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            JsonElement el when el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonElement el when el.ValueKind == JsonValueKind.String => string.IsNullOrWhiteSpace(el.GetString()),
            JsonElement el when el.ValueKind == JsonValueKind.Array => !el.EnumerateArray().Any(),
            System.Collections.IEnumerable e when value is not string => !e.GetEnumerator().MoveNext(),
            _ => false
        };

    private static object? NormalizeValue(object? value) =>
        value is JsonElement el ? JsonElementToObject(el) : value;

    private static object? JsonElementToObject(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => JsonSerializer.Deserialize<object?>(element.GetRawText())
        };

    private static WorkspaceFieldPolicy? ParsePolicy(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object)
            return null;

        var id = ReadString(raw, "id");
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var scope = ReadString(raw, "scope") == "conditional" ? "conditional" : "always";
        var kind = ReadString(raw, "kind");
        if (string.IsNullOrWhiteSpace(kind) && raw.TryGetProperty("visible", out _))
            kind = "visibility";

        var clauses = scope == "conditional" ? ParseClauses(raw) : Array.Empty<PolicyClause>();

        if (string.Equals(kind, "visibility", StringComparison.OrdinalIgnoreCase)
            && raw.TryGetProperty("visible", out var visEl)
            && visEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return new WorkspaceFieldPolicy
            {
                Id = id,
                Kind = "visibility",
                Scope = scope,
                Clauses = clauses,
                Visible = visEl.GetBoolean()
            };
        }

        if (string.Equals(kind, "readonly", StringComparison.OrdinalIgnoreCase)
            && raw.TryGetProperty("readonly", out var roEl)
            && roEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return new WorkspaceFieldPolicy
            {
                Id = id,
                Kind = "readonly",
                Scope = scope,
                Clauses = clauses,
                Readonly = roEl.GetBoolean()
            };
        }

        if (string.Equals(kind, "defaultValue", StringComparison.OrdinalIgnoreCase)
            && raw.TryGetProperty("value", out var valEl))
        {
            return new WorkspaceFieldPolicy
            {
                Id = id,
                Kind = "defaultValue",
                Scope = scope,
                Clauses = clauses,
                DefaultValue = JsonElementToObject(valEl)
            };
        }

        return null;
    }

    private static IReadOnlyList<PolicyClause> ParseClauses(JsonElement policyEl)
    {
        if (!policyEl.TryGetProperty("conditions", out var condEl)
            || condEl.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<PolicyClause>();
        }

        if (condEl.TryGetProperty("clauses", out var clausesEl) && clausesEl.ValueKind == JsonValueKind.Array)
        {
            return clausesEl.EnumerateArray()
                .Select(ParseClause)
                .Where(c => c != null)
                .Cast<PolicyClause>()
                .ToList();
        }

        return MigrateLegacyConditions(condEl);
    }

    private static PolicyClause? ParseClause(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object)
            return null;

        var fieldKey = ReadString(raw, "fieldKey");
        if (string.IsNullOrWhiteSpace(fieldKey))
            fieldKey = ReadString(raw, "field");

        if (string.IsNullOrWhiteSpace(fieldKey))
            return null;

        if (!raw.TryGetProperty("value", out var valueEl))
            return null;

        var value = JsonElementToObject(valueEl);
        if (IsEmpty(value))
            return null;

        var op = ReadString(raw, "operator");
        if (string.IsNullOrWhiteSpace(op))
            op = ReadString(raw, "op");
        if (string.IsNullOrWhiteSpace(op))
            op = "eq";

        return new PolicyClause
        {
            FieldKey = fieldKey,
            Operator = op,
            Value = value
        };
    }

    private static IReadOnlyList<PolicyClause> MigrateLegacyConditions(JsonElement condEl)
    {
        var clauses = new List<PolicyClause>();

        if (condEl.TryGetProperty("stateId", out var stateEl)
            && stateEl.ValueKind == JsonValueKind.String)
        {
            var stateId = stateEl.GetString();
            if (!string.IsNullOrWhiteSpace(stateId))
            {
                clauses.Add(new PolicyClause
                {
                    FieldKey = "stateId",
                    Operator = "eq",
                    Value = stateId
                });
            }
        }

        if (condEl.TryGetProperty("userGroups", out var groupsEl)
            && groupsEl.ValueKind == JsonValueKind.Array)
        {
            var groups = groupsEl.EnumerateArray()
                .Select(g => g.ValueKind == JsonValueKind.String ? g.GetString() : g.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (groups.Count == 1)
            {
                clauses.Add(new PolicyClause
                {
                    FieldKey = "assignmentGroups",
                    Operator = "eq",
                    Value = groups[0]!
                });
            }
            else if (groups.Count > 1)
            {
                clauses.Add(new PolicyClause
                {
                    FieldKey = "assignmentGroups",
                    Operator = "eq",
                    Value = groups
                });
            }
        }

        return clauses;
    }

    private static void MigrateLegacyVisibility(
        JsonElement blobEl,
        Dictionary<string, IReadOnlyList<WorkspaceFieldPolicy>> policiesByField)
    {
        JsonElement? legacy = null;
        if (blobEl.TryGetProperty("visibilityByField", out var vbf))
            legacy = vbf;
        else if (blobEl.TryGetProperty("visibility", out var vis))
            legacy = vis;

        if (legacy is not { ValueKind: JsonValueKind.Object })
            return;

        foreach (var prop in legacy.Value.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array)
                continue;

            var bucket = policiesByField.TryGetValue(prop.Name, out var existing)
                ? existing.ToList()
                : new List<WorkspaceFieldPolicy>();

            var ids = new HashSet<string>(bucket.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);

            foreach (var item in prop.Value.EnumerateArray())
            {
                var parsed = ParsePolicy(item);
                if (parsed == null || ids.Contains(parsed.Id))
                    continue;

                bucket.Add(parsed);
                ids.Add(parsed.Id);
            }

            if (bucket.Count > 0)
                policiesByField[prop.Name] = bucket;
        }
    }

    private static string? ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}
