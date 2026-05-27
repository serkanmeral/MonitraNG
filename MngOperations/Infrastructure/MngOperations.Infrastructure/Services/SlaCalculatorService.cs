using MngOperations.Application.Interfaces;
using MngOperations.Application.Utilities;

namespace MngOperations.Infrastructure.Services;

public class SlaCalculatorService : ISlaCalculator
{
    private readonly IMetadataCache _metadataCache;

    public SlaCalculatorService(IMetadataCache metadataCache)
    {
        _metadataCache = metadataCache;
    }

    public async Task ApplyOnCreateAsync(
        Dictionary<string, object?> payload,
        string workspaceId,
        string typeId,
        string? priorityId,
        DateTime anchorUtc,
        string token,
        CancellationToken cancellationToken = default)
    {
        var policy = await _metadataCache.ResolveSlaPolicyAsync(workspaceId, typeId, priorityId, token, cancellationToken);
        if (policy == null || string.IsNullOrEmpty(policy.DataId))
            return;

        payload["slaPolicyId"] = policy.DataId;
        payload["sla"] = SlaSnapshotHelper.BuildSnapshot(
            anchorUtc,
            anchorUtc,
            policy.ResponseTargetMinutes,
            policy.ResolveTargetMinutes,
            closedAtUtc: null);
    }

    public async Task ApplyOnTransitionAsync(
        Dictionary<string, object?> merged,
        IReadOnlyDictionary<string, object?> existing,
        DateTime nowUtc,
        string token,
        CancellationToken cancellationToken = default)
    {
        var policyId = WorkItemDataHelper.GetString(merged, "slaPolicyId")
            ?? WorkItemDataHelper.GetString(existing, "slaPolicyId");

        if (string.IsNullOrEmpty(policyId))
            return;

        var anchor = WorkItemDataHelper.GetDateTime(existing, "createdAt") ?? nowUtc;
        var closedAt = WorkItemDataHelper.GetDateTime(merged, "closedAt");

        double? responseMinutes = null;
        double? resolveMinutes = null;

        if (existing.TryGetValue("sla", out var existingSla) && existingSla != null)
        {
            var responseDue = ReadDueAt(existingSla, "responseDueAt");
            var resolveDue = ReadDueAt(existingSla, "resolveDueAt");
            if (responseDue.HasValue)
                responseMinutes = (responseDue.Value - anchor).TotalMinutes;
            if (resolveDue.HasValue)
                resolveMinutes = (resolveDue.Value - anchor).TotalMinutes;
        }

        if (responseMinutes == null && resolveMinutes == null)
        {
            var workspaceId = WorkItemDataHelper.GetString(existing, "workspaceId") ?? string.Empty;
            var typeId = WorkItemDataHelper.GetString(existing, "typeId") ?? string.Empty;
            var priorityId = WorkItemDataHelper.GetString(existing, "priorityId");
            var policy = await _metadataCache.ResolveSlaPolicyAsync(workspaceId, typeId, priorityId, token, cancellationToken);
            if (policy != null)
            {
                responseMinutes = policy.ResponseTargetMinutes;
                resolveMinutes = policy.ResolveTargetMinutes;
                merged["slaPolicyId"] = policy.DataId;
            }
        }

        merged["sla"] = SlaSnapshotHelper.BuildSnapshot(
            anchor,
            nowUtc,
            responseMinutes,
            resolveMinutes,
            closedAt);
    }

    private static DateTime? ReadDueAt(object slaRaw, string key)
    {
        if (slaRaw is System.Text.Json.JsonElement el && el.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (!el.TryGetProperty(key, out var prop) || prop.ValueKind != System.Text.Json.JsonValueKind.String)
                return null;

            return DateTime.TryParse(prop.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? dt
                : null;
        }

        if (slaRaw is IReadOnlyDictionary<string, object?> dict)
            return WorkItemDataHelper.GetDateTime(dict, key);

        return null;
    }
}
