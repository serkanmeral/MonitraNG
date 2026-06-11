using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Automations;
using MngOperations.Application.Contracts.WorkItems;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Rules;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed class WorkspaceAutomationService : IWorkspaceAutomationService
{
    private readonly IMetadataCache _metadataCache;
    private readonly Lazy<IWorkItemCommandService> _workItemCommand;
    private readonly IMngDataGatewayClient _dg;
    private readonly ILogger<WorkspaceAutomationService> _logger;

    public WorkspaceAutomationService(
        IMetadataCache metadataCache,
        Lazy<IWorkItemCommandService> workItemCommand,
        IMngDataGatewayClient dg,
        ILogger<WorkspaceAutomationService> logger)
    {
        _metadataCache = metadataCache;
        _workItemCommand = workItemCommand;
        _dg = dg;
        _logger = logger;
    }

    public async Task ExecuteOnWorkItemTransitionAsync(
        WorkspaceAutomationTriggerContext context,
        string token,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkspaceAutomationRecord> automations;
        try
        {
            automations = await _metadataCache.GetWorkspaceAutomationsForWorkspaceAsync(
                context.WorkspaceId,
                token,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load workspace automations for workspace {WorkspaceId}",
                context.WorkspaceId);
            return;
        }

        foreach (var automation in automations.Where(a => a.IsActive))
        {
            try
            {
                await TryExecuteAutomationAsync(automation, context, token, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Workspace automation {AutomationId} ({Name}) failed for work item {WorkItemKey}",
                    automation.DataId,
                    automation.Name,
                    context.WorkItemKey);

                await WriteSourceActivityAsync(
                    context,
                    "AutomationFailed",
                    $"Otomasyon başarısız: {automation.Name ?? automation.DataId} — {ex.Message}",
                    token,
                    cancellationToken);
            }
        }
    }

    private async Task TryExecuteAutomationAsync(
        WorkspaceAutomationRecord automation,
        WorkspaceAutomationTriggerContext context,
        string token,
        CancellationToken cancellationToken)
    {
        if (automation.Trigger is not { ValueKind: JsonValueKind.Object } trigger)
            return;

        if (!MatchesTrigger(trigger, context))
            return;

        if (automation.Actions is not { ValueKind: JsonValueKind.Array } actions)
            return;

        var automationId = automation.DataId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(automationId))
            return;

        foreach (var action in actions.EnumerateArray().OrderBy(GetActionOrder))
        {
            if (!action.TryGetProperty("type", out var typeProp)
                || typeProp.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var actionType = typeProp.GetString();
            if (!string.Equals(actionType, "createWorkItem", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(actionType, "generateDocument", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug(
                        "Skipping generateDocument action for automation {AutomationId} (not implemented)",
                        automationId);
                }

                continue;
            }

            var createResult = await ExecuteCreateWorkItemActionAsync(
                automation,
                action,
                context,
                token,
                cancellationToken);

            if (createResult != null)
            {
                await PatchAutomationRunMetadataAsync(
                    automationId,
                    createResult.WorkItem.Id,
                    automation.RunCount ?? 0,
                    token,
                    cancellationToken);

                await WriteSourceActivityAsync(
                    context,
                    "AutomationExecuted",
                    $"Otomasyon «{automation.Name}» → {createResult.WorkItem.Key} oluşturuldu",
                    token,
                    cancellationToken,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["automationId"] = automationId,
                        ["createdWorkItemId"] = createResult.WorkItem.Id,
                        ["createdWorkItemKey"] = createResult.WorkItem.Key,
                        ["code"] = createResult.Code
                    });
            }

            break;
        }
    }

    private async Task<CreateWorkItemResponse?> ExecuteCreateWorkItemActionAsync(
        WorkspaceAutomationRecord automation,
        JsonElement action,
        WorkspaceAutomationTriggerContext context,
        string token,
        CancellationToken cancellationToken)
    {
        if (!action.TryGetProperty("target", out var target)
            || target.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var boardId = GetJsonString(target, "boardId");
        var typeId = GetJsonString(target, "typeId");
        if (string.IsNullOrWhiteSpace(boardId) || string.IsNullOrWhiteSpace(typeId))
            return null;

        var titleTemplate = GetJsonString(action, "title");
        var title = AutomationTokenResolver.Resolve(titleTemplate, context);
        if (string.IsNullOrWhiteSpace(title))
            title = $"Auto — {context.WorkItemKey}";

        var description = GetJsonString(action, "description");
        if (!string.IsNullOrWhiteSpace(description))
            description = AutomationTokenResolver.Resolve(description, context);

        var assigneeRaw = GetJsonString(action, "assignee");
        var assignee = string.IsNullOrWhiteSpace(assigneeRaw)
            ? null
            : AutomationTokenResolver.Resolve(assigneeRaw, context);

        var priorityId = GetJsonString(action, "priorityId");

        var fieldDict = BuildFieldDictionary(action, automation, context);
        JsonElement? fieldsElement = null;
        if (fieldDict.Count > 0)
        {
            var json = JsonSerializer.Serialize(fieldDict);
            fieldsElement = JsonDocument.Parse(json).RootElement;
        }

        var automationId = automation.DataId ?? string.Empty;
        var correlationId = BuildCorrelationId(automation, context);

        var request = new CreateFromOriginRequest
        {
            WorkspaceId = context.WorkspaceId,
            TypeId = typeId,
            Title = title,
            Description = description,
            BoardId = boardId,
            Assignee = assignee,
            PriorityId = priorityId,
            Fields = fieldsElement,
            Origin = new WorkItemOriginInput
            {
                SourceType = "workspace_automation",
                SourceSystem = "MngOperations",
                SourceId = automationId,
                CorrelationId = correlationId
            }
        };

        return await _workItemCommand.Value.CreateFromOriginAsync(request, cancellationToken);
    }

    private static Dictionary<string, object?> BuildFieldDictionary(
        JsonElement action,
        WorkspaceAutomationRecord automation,
        WorkspaceAutomationTriggerContext context)
    {
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (action.TryGetProperty("fieldMappings", out var mappings)
            && mappings.ValueKind == JsonValueKind.Array)
        {
            foreach (var mapping in mappings.EnumerateArray())
            {
                var target = GetJsonString(mapping, "target");
                if (string.IsNullOrWhiteSpace(target))
                    continue;

                var source = GetJsonString(mapping, "source")?.ToLowerInvariant();
                object? value = source switch
                {
                    "static" => GetJsonString(mapping, "value"),
                    "token" => AutomationTokenResolver.Resolve(
                        GetJsonString(mapping, "template") ?? GetJsonString(mapping, "value"),
                        context),
                    "field" => ResolveFieldMappingValue(mapping, context),
                    "relation" when string.Equals(
                        GetJsonString(mapping, "relation"),
                        "parent",
                        StringComparison.OrdinalIgnoreCase)
                        || GetRelationMode(automation) == "parent" =>
                        context.WorkItemId,
                    _ => null
                };

                if (value is not null && !string.IsNullOrWhiteSpace(value.ToString()))
                    fields[target] = value;
            }
        }

        if (GetRelationMode(automation) == "parent"
            && !fields.ContainsKey("parentItemId"))
        {
            fields["parentItemId"] = context.WorkItemId;
        }

        return fields;
    }

    private static object? ResolveFieldMappingValue(
        JsonElement mapping,
        WorkspaceAutomationTriggerContext context)
    {
        var path = GetJsonString(mapping, "path");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return AutomationTokenResolver.ResolvePath($"source.{path}", context)
            ?? AutomationTokenResolver.ResolvePath(path, context);
    }

    private static string BuildCorrelationId(
        WorkspaceAutomationRecord automation,
        WorkspaceAutomationTriggerContext context)
    {
        var automationId = automation.DataId ?? "unknown";
        var mode = GetIdempotencyMode(automation);

        return mode switch
        {
            "one_per_source" => $"{automationId}:{context.WorkItemId}",
            _ => $"{automationId}:{context.WorkItemId}:{Guid.NewGuid():N}"
        };
    }

    private static bool MatchesTrigger(
        JsonElement trigger,
        WorkspaceAutomationTriggerContext context)
    {
        var kind = GetJsonString(trigger, "kind") ?? "workItemStateReached";
        if (!string.Equals(kind, "workItemStateReached", StringComparison.OrdinalIgnoreCase))
            return false;

        if (TryGetNonEmptyString(trigger, "boardId", out var boardId)
            && !string.Equals(boardId, context.BoardId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryGetNonEmptyString(trigger, "typeId", out var typeId)
            && !string.Equals(typeId, context.TypeId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryGetNonEmptyString(trigger, "toStateId", out var toStateId)
            && !string.Equals(toStateId, context.ToStateId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryGetNonEmptyString(trigger, "transitionKey", out var transitionKey)
            && !string.Equals(transitionKey, context.TransitionKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (trigger.TryGetProperty("conditions", out var conditions)
            && conditions.ValueKind != JsonValueKind.Null
            && conditions.ValueKind != JsonValueKind.Undefined)
        {
            if (!RuleConditionEvaluator.Evaluate(conditions, context.WorkItem))
                return false;
        }

        return true;
    }

    private async Task PatchAutomationRunMetadataAsync(
        string automationId,
        string createdWorkItemId,
        int previousRunCount,
        string token,
        CancellationToken cancellationToken)
    {
        var patch = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["lastRunAt"] = DateTime.UtcNow,
            ["lastCreatedWorkItemId"] = createdWorkItemId,
            ["runCount"] = previousRunCount + 1
        };

        await _dg.UpdateAsync(
            OcDatasets.WorkspaceAutomations,
            automationId,
            patch,
            token,
            cancellationToken);
    }

    private async Task WriteSourceActivityAsync(
        WorkspaceAutomationTriggerContext context,
        string activityType,
        string summary,
        string token,
        CancellationToken cancellationToken,
        Dictionary<string, object?>? extra = null)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["workItemId"] = context.WorkItemId,
            ["workItemKey"] = context.WorkItemKey,
            ["activityType"] = activityType,
            ["eventType"] = context.EventName,
            ["summary"] = summary,
            ["actor"] = "system"
        };

        if (extra != null)
        {
            foreach (var kv in extra)
                payload[kv.Key] = kv.Value;
        }

        await _dg.CreateAsync(OcDatasets.Activities, payload, token, cancellationToken);
    }

    private static string GetRelationMode(WorkspaceAutomationRecord automation)
    {
        if (automation.Relation is not { ValueKind: JsonValueKind.Object } relation)
            return "parent";

        return GetJsonString(relation, "mode")?.ToLowerInvariant() ?? "parent";
    }

    private static string GetIdempotencyMode(WorkspaceAutomationRecord automation)
    {
        if (automation.Idempotency is not { ValueKind: JsonValueKind.Object } idempotency)
            return "none";

        return GetJsonString(idempotency, "mode")?.ToLowerInvariant() ?? "none";
    }

    private static int GetActionOrder(JsonElement action)
    {
        if (action.TryGetProperty("order", out var order)
            && order.ValueKind == JsonValueKind.Number
            && order.TryGetInt32(out var n))
        {
            return n;
        }

        return 0;
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool TryGetNonEmptyString(JsonElement element, string propertyName, out string value)
    {
        value = GetJsonString(element, propertyName) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
