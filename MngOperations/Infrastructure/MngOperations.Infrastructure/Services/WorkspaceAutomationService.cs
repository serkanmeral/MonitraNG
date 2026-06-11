using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Automations;
using MngOperations.Application.Contracts.WorkItems;
using MngOperations.Application.Exceptions;
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
    private readonly IRequestContext _requestContext;
    private readonly ILogger<WorkspaceAutomationService> _logger;

    public WorkspaceAutomationService(
        IMetadataCache metadataCache,
        Lazy<IWorkItemCommandService> workItemCommand,
        IMngDataGatewayClient dg,
        IRequestContext requestContext,
        ILogger<WorkspaceAutomationService> logger)
    {
        _metadataCache = metadataCache;
        _workItemCommand = workItemCommand;
        _dg = dg;
        _requestContext = requestContext;
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

    public async Task<SimulateWorkspaceAutomationResult> SimulateAsync(
        string automationId,
        SimulateWorkspaceAutomationRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireManagerOrAdmin();

        var token = RequireBearerToken();
        var workItemId = request.WorkItemId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(workItemId))
        {
            throw new OperationCoreException(
                "VALIDATION_ERROR",
                "workItemId is required.",
                "Kaynak iş kaydı zorunludur.",
                400);
        }

        var automation = await LoadAutomationAsync(automationId, token, cancellationToken);
        var workItem = await LoadWorkItemAsync(workItemId, token, cancellationToken);

        var workspaceId = WorkItemDataHelper.GetString(workItem, "workspaceId")
            ?? throw new OperationCoreException(
                "WORK_ITEM_INVALID",
                "workspaceId missing on work item.",
                "Kayıtta workspaceId yok.",
                500);

        var automationWorkspaceId = automation.WorkspaceId?.Trim() ?? string.Empty;
        if (!string.Equals(automationWorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new OperationCoreException(
                "AUTOMATION_WORKSPACE_MISMATCH",
                "Work item and automation must belong to the same workspace.",
                "İş kaydı ve otomasyon aynı workspace içinde olmalıdır.",
                400);
        }

        var context = BuildSimulatedContext(automation, workItemId, workItem);

        if (automation.Trigger is not { ValueKind: JsonValueKind.Object } trigger)
        {
            return new SimulateWorkspaceAutomationResult
            {
                Matched = false,
                Reason = "Tetik tanımı eksik veya geçersiz."
            };
        }

        if (!MatchesTrigger(trigger, context))
        {
            return new SimulateWorkspaceAutomationResult
            {
                Matched = false,
                Reason = "Tetik koşulları bu kayıt için sağlanmıyor."
            };
        }

        if (!automation.IsActive)
        {
            return new SimulateWorkspaceAutomationResult
            {
                Matched = false,
                Reason = "Otomasyon pasif."
            };
        }

        var action = FindCreateWorkItemAction(automation);
        if (action is null)
        {
            return new SimulateWorkspaceAutomationResult
            {
                Matched = false,
                Reason = "Desteklenen createWorkItem aksiyonu bulunamadı."
            };
        }

        var preview = BuildActionPreview(automation, action.Value, context);

        if (!request.Execute)
        {
            return new SimulateWorkspaceAutomationResult
            {
                Matched = true,
                Preview = preview
            };
        }

        var createResult = await ExecuteCreateWorkItemActionAsync(
            automation,
            action.Value,
            context,
            token,
            cancellationToken);

        if (createResult == null)
        {
            return new SimulateWorkspaceAutomationResult
            {
                Matched = true,
                Preview = preview,
                Reason = "Aksiyon çalıştırılamadı (hedef board/tip eksik olabilir)."
            };
        }

        var automationRecordId = automation.DataId ?? automationId;
        await PatchAutomationRunMetadataAsync(
            automationRecordId,
            createResult.WorkItem.Id,
            automation.RunCount ?? 0,
            token,
            cancellationToken);

        await WriteSourceActivityAsync(
            context,
            "AutomationExecuted",
            $"Otomasyon simülasyonu «{automation.Name}» → {createResult.WorkItem.Key} oluşturuldu",
            token,
            cancellationToken,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["automationId"] = automationRecordId,
                ["createdWorkItemId"] = createResult.WorkItem.Id,
                ["createdWorkItemKey"] = createResult.WorkItem.Key,
                ["code"] = createResult.Code,
                ["simulated"] = true
            });

        return new SimulateWorkspaceAutomationResult
        {
            Matched = true,
            Executed = true,
            Preview = preview,
            CreatedWorkItem = new WorkspaceAutomationSimulateCreatedDto
            {
                Id = createResult.WorkItem.Id,
                Key = createResult.WorkItem.Key,
                Code = createResult.Code
            }
        };
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
        try
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
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to patch automation run metadata for {AutomationId}",
                automationId);
        }
    }

    private async Task WriteSourceActivityAsync(
        WorkspaceAutomationTriggerContext context,
        string activityType,
        string summary,
        string token,
        CancellationToken cancellationToken,
        Dictionary<string, object?>? extra = null)
    {
        try
        {
            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceDataset"] = OcDatasets.WorkItems,
                ["sourceRecordId"] = context.WorkItemId,
                ["activityType"] = activityType,
                ["eventType"] = context.EventName,
                ["message"] = summary,
                ["activityDate"] = DateTime.UtcNow,
                ["isSystemGenerated"] = true
            };

            if (!string.IsNullOrWhiteSpace(_requestContext.MngPersonId))
                payload["actor"] = _requestContext.MngPersonId;

            if (extra is { Count: > 0 })
            {
                extra["workItemKey"] = context.WorkItemKey;
                payload["payload"] = extra;
            }

            await _dg.CreateAsync(OcDatasets.Activities, payload, token, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Automation activity write failed for work item {WorkItemId} ({ActivityType})",
                context.WorkItemId,
                activityType);
        }
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

    private async Task<WorkspaceAutomationRecord> LoadAutomationAsync(
        string automationId,
        string token,
        CancellationToken cancellationToken)
    {
        var row = await _dg.GetByIdAsync<WorkspaceAutomationRecord>(
            OcDatasets.WorkspaceAutomations,
            automationId,
            token,
            cancellationToken,
            expand: false);

        if (row == null)
        {
            throw new OperationCoreException(
                "AUTOMATION_NOT_FOUND",
                $"Automation '{automationId}' not found.",
                $"Otomasyon '{automationId}' bulunamadı.",
                404);
        }

        return row;
    }

    private async Task<Dictionary<string, object?>> LoadWorkItemAsync(
        string workItemId,
        string token,
        CancellationToken cancellationToken)
    {
        var row = await _dg.GetByIdAsync<Dictionary<string, object?>>(
            OcDatasets.WorkItems,
            workItemId,
            token,
            cancellationToken,
            expand: false);

        if (row == null)
        {
            throw new OperationCoreException(
                "WORK_ITEM_NOT_FOUND",
                $"Work item '{workItemId}' not found.",
                $"İş kaydı '{workItemId}' bulunamadı.",
                404);
        }

        return new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase);
    }

    private static WorkspaceAutomationTriggerContext BuildSimulatedContext(
        WorkspaceAutomationRecord automation,
        string workItemId,
        IReadOnlyDictionary<string, object?> workItem)
    {
        string? transitionKey = null;
        string? toStateId = null;
        if (automation.Trigger is { ValueKind: JsonValueKind.Object } trigger)
        {
            transitionKey = GetJsonString(trigger, "transitionKey");
            toStateId = GetJsonString(trigger, "toStateId");
        }

        return new WorkspaceAutomationTriggerContext
        {
            EventName = "WorkspaceAutomationSimulated",
            WorkspaceId = automation.WorkspaceId ?? string.Empty,
            BoardId = WorkItemDataHelper.GetString(workItem, "boardId"),
            TypeId = WorkItemDataHelper.GetString(workItem, "typeId"),
            WorkItemId = workItemId,
            WorkItemKey = WorkItemDataHelper.GetString(workItem, "key") ?? workItemId,
            FromStateId = WorkItemDataHelper.GetString(workItem, "stateId"),
            ToStateId = toStateId ?? WorkItemDataHelper.GetString(workItem, "stateId"),
            TransitionKey = transitionKey,
            WorkItem = workItem
        };
    }

    private static JsonElement? FindCreateWorkItemAction(WorkspaceAutomationRecord automation)
    {
        if (automation.Actions is not { ValueKind: JsonValueKind.Array } actions)
            return null;

        foreach (var action in actions.EnumerateArray().OrderBy(GetActionOrder))
        {
            if (action.TryGetProperty("type", out var typeProp)
                && typeProp.ValueKind == JsonValueKind.String
                && string.Equals(typeProp.GetString(), "createWorkItem", StringComparison.OrdinalIgnoreCase))
            {
                return action;
            }
        }

        return null;
    }

    private static WorkspaceAutomationSimulatePreviewDto BuildActionPreview(
        WorkspaceAutomationRecord automation,
        JsonElement action,
        WorkspaceAutomationTriggerContext context)
    {
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

        string? boardId = null;
        string? typeId = null;
        if (action.TryGetProperty("target", out var target) && target.ValueKind == JsonValueKind.Object)
        {
            boardId = GetJsonString(target, "boardId");
            typeId = GetJsonString(target, "typeId");
        }

        var fields = BuildFieldDictionary(action, automation, context);

        return new WorkspaceAutomationSimulatePreviewDto
        {
            ResolvedTitle = title,
            ResolvedDescription = description,
            TargetBoardId = boardId,
            TargetTypeId = typeId,
            ResolvedAssignee = assignee,
            ResolvedFields = fields
        };
    }

    private void RequireManagerOrAdmin()
    {
        if (_requestContext.IsAdmin || _requestContext.IsManager)
            return;

        throw new OperationCoreException(
            "FORBIDDEN",
            "Only domain managers can simulate workspace automations.",
            "Otomasyon simülasyonunu yalnızca domain yöneticileri çalıştırabilir.",
            403);
    }

    private string RequireBearerToken()
    {
        if (string.IsNullOrWhiteSpace(_requestContext.BearerToken))
        {
            throw new OperationCoreException(
                "UNAUTHORIZED",
                "Bearer token is required.",
                "Bearer token gerekli.",
                401);
        }

        return _requestContext.BearerToken;
    }
}
