using System.Text.Json;
using MngOperations.Application.Contracts.Runtime;

namespace MngOperations.Application.Utilities;

public static class SlaSnapshotHelper
{
    public static Dictionary<string, object?> BuildSnapshot(
        DateTime anchorUtc,
        DateTime evaluatedUtc,
        double? responseTargetMinutes,
        double? resolveTargetMinutes,
        DateTime? closedAtUtc)
    {
        DateTime? responseDue = null;
        DateTime? resolveDue = null;

        if (responseTargetMinutes is > 0)
            responseDue = anchorUtc.AddMinutes(responseTargetMinutes.Value);

        if (resolveTargetMinutes is > 0)
            resolveDue = anchorUtc.AddMinutes(resolveTargetMinutes.Value);

        var isClosed = closedAtUtc.HasValue;
        var responseBreached = !isClosed && responseDue.HasValue && evaluatedUtc > responseDue.Value;
        var resolveBreached = !isClosed && resolveDue.HasValue && evaluatedUtc > resolveDue.Value;

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["responseDueAt"] = responseDue,
            ["resolveDueAt"] = resolveDue,
            ["responseBreached"] = responseBreached,
            ["resolveBreached"] = resolveBreached,
            ["calculatedAt"] = evaluatedUtc
        };
    }

    public static SlaSnapshotDto? MapFromWorkItem(
        IReadOnlyDictionary<string, object?> workItem,
        string? slaPolicyIdOverride = null)
    {
        var slaPolicyId = slaPolicyIdOverride ?? WorkItemDataHelper.GetString(workItem, "slaPolicyId");
        if (!workItem.TryGetValue("sla", out var slaRaw) || slaRaw == null)
        {
            return string.IsNullOrEmpty(slaPolicyId)
                ? null
                : new SlaSnapshotDto { SlaPolicyId = slaPolicyId };
        }

        if (slaRaw is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            return new SlaSnapshotDto
            {
                SlaPolicyId = slaPolicyId,
                ResponseDueAt = ReadDateTime(el, "responseDueAt"),
                ResolveDueAt = ReadDateTime(el, "resolveDueAt"),
                ResponseBreached = ReadBool(el, "responseBreached"),
                ResolveBreached = ReadBool(el, "resolveBreached"),
                CalculatedAt = ReadDateTime(el, "calculatedAt")
            };
        }

        if (slaRaw is Dictionary<string, object?> dict)
        {
            return new SlaSnapshotDto
            {
                SlaPolicyId = slaPolicyId,
                ResponseDueAt = WorkItemDataHelper.GetDateTime(dict, "responseDueAt"),
                ResolveDueAt = WorkItemDataHelper.GetDateTime(dict, "resolveDueAt"),
                ResponseBreached = WorkItemDataHelper.GetBool(dict, "responseBreached") ?? false,
                ResolveBreached = WorkItemDataHelper.GetBool(dict, "resolveBreached") ?? false,
                CalculatedAt = WorkItemDataHelper.GetDateTime(dict, "calculatedAt")
            };
        }

        return null;
    }

    private static DateTime? ReadDateTime(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.String when DateTime.TryParse(prop.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) => dt,
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static bool ReadBool(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var prop))
            return false;

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => false
        };
    }
}
