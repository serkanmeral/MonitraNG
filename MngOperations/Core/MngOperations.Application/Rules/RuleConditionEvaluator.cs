using System.Text.Json;
using MngOperations.Application.Utilities;

namespace MngOperations.Application.Rules;

public static class RuleConditionEvaluator
{
    public static bool Evaluate(JsonElement? expression, IReadOnlyDictionary<string, object?> workItem)
    {
        if (expression is not { ValueKind: JsonValueKind.Object })
            return true;

        if (expression.Value.TryGetProperty("op", out var opProp)
            && opProp.ValueKind == JsonValueKind.String)
        {
            var op = opProp.GetString()?.ToLowerInvariant();
            if (op is "and" or "or")
            {
                if (!expression.Value.TryGetProperty("items", out var items)
                    || items.ValueKind != JsonValueKind.Array)
                {
                    return true;
                }

                var results = items.EnumerateArray().Select(i => Evaluate(i, workItem)).ToList();
                return op == "and" ? results.All(r => r) : results.Any(r => r);
            }
        }

        return EvaluateLeaf(expression.Value, workItem);
    }

    private static bool EvaluateLeaf(JsonElement node, IReadOnlyDictionary<string, object?> workItem)
    {
        var field = node.TryGetProperty("field", out var fieldProp) ? fieldProp.GetString() : null;
        var cmp = node.TryGetProperty("cmp", out var cmpProp)
            ? cmpProp.GetString()?.ToLowerInvariant()
            : "eq";

        if (string.IsNullOrWhiteSpace(field))
            return true;

        var actual = ResolveFieldValue(workItem, field);
        var hasExpected = node.TryGetProperty("value", out var expectedProp)
            && expectedProp.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null);
        var expected = hasExpected ? JsonElementToObject(expectedProp) : null;

        return cmp switch
        {
            "eq" => ValuesEqual(actual, expected),
            "ne" => !ValuesEqual(actual, expected),
            "empty" => IsEmpty(actual),
            "notempty" => !IsEmpty(actual),
            "in" => hasExpected && IsIn(actual, expectedProp),
            "gt" => CompareNumeric(actual, expected) > 0,
            "lt" => CompareNumeric(actual, expected) < 0,
            _ => ValuesEqual(actual, expected)
        };
    }

    private static object? ResolveFieldValue(IReadOnlyDictionary<string, object?> workItem, string fieldPath)
    {
        if (fieldPath.StartsWith("fields.", StringComparison.OrdinalIgnoreCase))
        {
            var customKey = fieldPath["fields.".Length..];
            return NormalizeValue(WorkItemDataHelper.GetFieldValue(workItem, customKey));
        }

        if (workItem.TryGetValue(fieldPath, out var value))
            return NormalizeValue(value);

        return NormalizeValue(WorkItemDataHelper.GetFieldValue(workItem, fieldPath));
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

    private static bool IsIn(object? actual, JsonElement expectedProp)
    {
        if (expectedProp.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var item in expectedProp.EnumerateArray())
        {
            if (ValuesEqual(actual, JsonElementToObject(item)))
                return true;
        }

        return false;
    }

    private static int CompareNumeric(object? actual, object? expected)
    {
        if (!TryToDecimal(actual, out var a) || !TryToDecimal(expected, out var b))
            return 0;

        return a.CompareTo(b);
    }

    private static bool TryToDecimal(object? value, out decimal result)
    {
        result = 0;
        value = NormalizeValue(value);

        return value switch
        {
            decimal d => Assign(d, out result),
            int i => Assign(i, out result),
            long l => Assign(l, out result),
            double d => Assign((decimal)d, out result),
            float f => Assign((decimal)f, out result),
            string s when decimal.TryParse(s, out var p) => Assign(p, out result),
            _ => false
        };

        static bool Assign(decimal d, out decimal r)
        {
            r = d;
            return true;
        }
    }

    private static bool ValuesEqual(object? actual, object? expected)
    {
        actual = NormalizeValue(actual);
        expected = NormalizeValue(expected);

        if (actual is null && expected is null)
            return true;

        if (actual is null || expected is null)
            return false;

        if (actual is string a && expected is string b)
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        return actual.ToString()?.Equals(expected.ToString(), StringComparison.OrdinalIgnoreCase) == true;
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is JsonElement el)
            return JsonElementToObject(el);

        return value;
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => JsonSerializer.Deserialize<object?>(element.GetRawText())
        };
    }
}
