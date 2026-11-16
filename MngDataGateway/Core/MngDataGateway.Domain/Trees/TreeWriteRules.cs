using System.Text.Json;
using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Domain.Trees;

public enum TreeWalkStatus
{
    Ok,
    Cycle,
    TooDeep
}

/// <summary>
/// Pure tree checks used when a dataset row is written.
/// </summary>
public static class TreeWriteRules
{
    public const int MaxDepth = 64;
    public const string RootMarkerField = "__treeRoot";
    public const string RootMarkerValue = "root";

    public static bool TryNormalizeReference(object? value, out string? id, out bool many)
    {
        id = null;
        many = false;

        if (value == null)
            return true;

        if (value is string text)
        {
            id = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            return true;
        }

        if (value is JsonElement element)
            return TryNormalizeJson(element, out id, out many);

        if (value is System.Collections.IEnumerable enumerable)
        {
            string? only = null;
            var count = 0;
            foreach (var item in enumerable)
            {
                count++;
                if (count > 1)
                {
                    many = true;
                    id = null;
                    return false;
                }

                if (!TryNormalizeReference(item, out only, out many))
                    return false;
            }

            id = only;
            return true;
        }

        return false;
    }

    public static bool TryReadPersonIds(object? value, out List<string> ids, out bool duplicate)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var repeated = false;
        var ok = Read(value);
        ids = found;
        duplicate = repeated;
        return ok;

        bool Read(object? current)
        {
            if (current == null)
                return true;

            if (current is string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                    return true;

                var id = text.Trim();
                if (!seen.Add(id))
                {
                    repeated = true;
                    return false;
                }

                found.Add(id);
                return true;
            }

            if (current is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Null:
                    case JsonValueKind.Undefined:
                        return true;
                    case JsonValueKind.String:
                        return Read(element.GetString());
                    case JsonValueKind.Array:
                        foreach (var item in element.EnumerateArray())
                        {
                            if (!Read(item))
                                return false;
                        }

                        return true;
                    default:
                        return false;
                }
            }

            if (current is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (!Read(item))
                        return false;
                }

                return true;
            }

            return false;
        }
    }

    public static TreeWalkStatus WalkParents(string? selfId, string? parentId, Func<string, string?> parentOf)
    {
        if (string.IsNullOrEmpty(parentId))
            return TreeWalkStatus.Ok;

        if (!string.IsNullOrEmpty(selfId) && string.Equals(parentId, selfId, StringComparison.Ordinal))
            return TreeWalkStatus.Cycle;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cursor = parentId;
        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (!seen.Add(cursor))
                return TreeWalkStatus.Cycle;

            var next = parentOf(cursor);
            if (string.IsNullOrEmpty(next))
                return TreeWalkStatus.Ok;

            if (!string.IsNullOrEmpty(selfId) && string.Equals(next, selfId, StringComparison.Ordinal))
                return TreeWalkStatus.Cycle;

            cursor = next;
        }

        return TreeWalkStatus.TooDeep;
    }

    public static IReadOnlyList<string> RemoveBlankReferences(TreeConstraint tree, IDictionary<string, object> data)
    {
        var removed = new List<string>();
        foreach (var field in new[] { tree.parentField, tree.occupantField, tree.staffField })
        {
            if (string.IsNullOrWhiteSpace(field) || !data.ContainsKey(field))
                continue;

            if (!TryNormalizeReference(data[field], out var id, out var many) || many)
                continue;

            if (id != null)
                continue;

            data.Remove(field);
            removed.Add(field);
        }

        return removed;
    }

    public static void ApplyRootMarker(
        TreeConstraint tree,
        IDictionary<string, object> data,
        bool parentTouched,
        bool parentEmpty,
        ISet<string>? unsets)
    {
        if (!tree.singleRoot || !parentTouched)
            return;

        if (parentEmpty)
        {
            data[RootMarkerField] = RootMarkerValue;
            unsets?.Remove(RootMarkerField);
            return;
        }

        data.Remove(RootMarkerField);
        unsets?.Add(RootMarkerField);
    }

    private static bool TryNormalizeJson(JsonElement element, out string? id, out bool many)
    {
        id = null;
        many = false;

        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return true;
            case JsonValueKind.String:
                var text = element.GetString();
                id = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
                return true;
            case JsonValueKind.Array:
                if (element.GetArrayLength() == 0)
                    return true;
                if (element.GetArrayLength() > 1)
                {
                    many = true;
                    return false;
                }

                return TryNormalizeJson(element[0], out id, out many);
            default:
                return false;
        }
    }
}
