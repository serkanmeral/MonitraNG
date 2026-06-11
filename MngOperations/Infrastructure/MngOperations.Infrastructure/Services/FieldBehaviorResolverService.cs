using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Exceptions;
using MngOperations.Application.FieldBehaviors;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Rules;
using MngOperations.Application.Utilities;

namespace MngOperations.Infrastructure.Services;

public sealed class FieldBehaviorResolverService : IFieldBehaviorResolver
{
    private readonly IMetadataCache _metadataCache;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<FieldBehaviorResolverService> _logger;

    public FieldBehaviorResolverService(
        IMetadataCache metadataCache,
        IRequestContext requestContext,
        ILogger<FieldBehaviorResolverService> logger)
    {
        _metadataCache = metadataCache;
        _requestContext = requestContext;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, FieldBehaviorDto>> ResolveAllAsync(
        FieldBehaviorResolveContext context,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();

        // İstek başına tek tarama: enabled field metadata'sını key→record map'ine topla
        // ve workspace kurallarını bir kez al. Aksi halde her alan için tüm enabledIds yeniden
        // taranıyordu (O(alan×enabledIds)). Çözülen davranış (alan seçimi/kurallar) birebir aynı.
        var (fieldKeys, fieldsByKey) = await CollectFieldKeysAsync(context, token, cancellationToken);
        var rules = await _metadataCache.GetRulesForWorkspaceAsync(
            context.Workspace.DataId ?? string.Empty, token, cancellationToken);

        var result = new Dictionary<string, FieldBehaviorDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var fieldKey in fieldKeys)
        {
            result[fieldKey] = await ResolveInternalAsync(context, fieldKey, token, fieldsByKey, rules, cancellationToken);
        }

        return result;
    }

    public async Task<FieldBehaviorDto> ResolveAsync(
        FieldBehaviorResolveContext context,
        string fieldName,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        return await ResolveInternalAsync(context, fieldName, token, null, null, cancellationToken);
    }

    public void EnsureWritableFields(
        FieldBehaviorResolveContext context,
        IReadOnlyDictionary<string, FieldBehaviorDto> behaviors,
        IEnumerable<string> fieldKeys)
    {
        var readonlyFields = new List<string>();

        foreach (var key in fieldKeys)
        {
            if (!behaviors.TryGetValue(key, out var behavior))
                continue;

            if (behavior.Readonly)
                readonlyFields.Add(key);
        }

        if (readonlyFields.Count == 0)
            return;

        throw new OperationCoreException(
            "FIELD_READONLY",
            $"Fields cannot be modified: {string.Join(", ", readonlyFields)}.",
            $"Şu alanlar salt okunur: {string.Join(", ", readonlyFields)}.",
            400,
            new Dictionary<string, object?> { ["fields"] = readonlyFields });
    }

    private async Task<FieldBehaviorDto> ResolveInternalAsync(
        FieldBehaviorResolveContext context,
        string fieldName,
        string token,
        IReadOnlyDictionary<string, FieldRecord>? fieldsByKey,
        IReadOnlyList<RuleRecord>? rules,
        CancellationToken cancellationToken)
    {
        var layers = new List<FieldBehaviorDto>();

        if (FieldBehaviorDefaults.SystemFieldKeys.Contains(fieldName, StringComparer.OrdinalIgnoreCase)
            && FieldBehaviorDefaults.ForSystemFields(context.Mode).TryGetValue(fieldName, out var systemDefault))
        {
            layers.Add(systemDefault);
        }
        else
        {
            layers.Add(new FieldBehaviorDto { Visible = true });
        }

        var fieldMeta = await TryLoadFieldMetadataAsync(context, fieldName, token, fieldsByKey, cancellationToken);
        // Çekirdek work item alanları (title, description, …): op_fields havuz kaydındaki
        // viewGroups/editGroups yalnızca özel havuz alanları içindir; core alan davranışını kilitlemesin.
        if (fieldMeta != null
            && !FieldBehaviorDefaults.SystemFieldKeys.Contains(fieldName, StringComparer.OrdinalIgnoreCase))
        {
            layers.Add(FromFieldDefinition(fieldMeta));
        }

        var screenBehaviors = context.Screen switch
        {
            FieldBehaviorScreen.Form => FieldBehaviorParser.ParseMap(context.Form?.FieldBehaviors),
            FieldBehaviorScreen.Profile => FieldBehaviorParser.ParseMap(context.Profile?.FieldBehaviors),
            _ => new Dictionary<string, FieldBehaviorDto>()
        };

        if (screenBehaviors.TryGetValue(fieldName, out var screenLayer))
            layers.Add(screenLayer);

        var workspacePolicies = WorkspaceFieldPolicies.Parse(context.Workspace.Settings);
        var policyHints = new WorkspaceFieldPolicies.PolicyEvaluationHints
        {
            StateId = context.StateId ?? WorkItemDataHelper.GetString(context.WorkItem, "stateId"),
            TypeId = WorkItemDataHelper.GetString(context.WorkItem, "typeId") ?? context.Form?.DefaultTypeId
        };
        layers.AddRange(WorkspaceFieldPolicies.ResolveBehaviorLayers(
            fieldName,
            workspacePolicies,
            context.WorkItem,
            policyHints));

        if (context.Board?.VisibleFields is { } visibleFields
            && context.Screen != FieldBehaviorScreen.Profile)
            layers.Add(FromBoardVisibility(fieldName, visibleFields));

        layers.Add(FromPermission(context.CanEdit, context.Mode, fieldName));
        layers.Add(await FromRulesAsync(context, fieldName, token, rules, cancellationToken));

        return FieldBehaviorMerger.MergeMany(layers);
    }

    private async Task<(IReadOnlyList<string> Keys, IReadOnlyDictionary<string, FieldRecord> FieldsByKey)> CollectFieldKeysAsync(
        FieldBehaviorResolveContext context,
        string token,
        CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(FieldBehaviorDefaults.SystemFieldKeys, StringComparer.OrdinalIgnoreCase);
        // key→record (ilk gelen kazanır) — eski TryLoad davranışıyla (enabledIds sırası, ilk eşleşme) aynı.
        var fieldsByKey = new Dictionary<string, FieldRecord>(StringComparer.OrdinalIgnoreCase);

        var enabledIds = MetadataRelationHelper.ParseIdList(context.Workspace.EnabledFieldIds);
        foreach (var fieldId in enabledIds)
        {
            try
            {
                var field = await _metadataCache.GetFieldAsync(fieldId, token, cancellationToken);
                if (!string.IsNullOrWhiteSpace(field.Key))
                {
                    keys.Add(field.Key);
                    if (!fieldsByKey.ContainsKey(field.Key))
                        fieldsByKey[field.Key] = field;
                }
            }
            catch (OperationCoreException ex) when (ex.Code == "FIELD_NOT_FOUND")
            {
                _logger.LogDebug("Enabled field {FieldId} not found, skipped", fieldId);
            }
        }

        foreach (var key in FieldBehaviorParser.ParseMap(context.Form?.FieldBehaviors).Keys)
            keys.Add(key);

        foreach (var key in FormLayoutHelper.ExtractOrderedFieldKeys(context.Form?.Layout))
            keys.Add(key);

        foreach (var key in FieldBehaviorParser.ParseMap(context.Profile?.FieldBehaviors).Keys)
            keys.Add(key);

        var workspacePolicies = WorkspaceFieldPolicies.Parse(context.Workspace.Settings);
        foreach (var key in WorkspaceFieldPolicies.EnumerateFieldKeys(workspacePolicies))
            keys.Add(key);

        return (keys.ToList(), fieldsByKey);
    }

    private async Task<FieldRecord?> TryLoadFieldMetadataAsync(
        FieldBehaviorResolveContext context,
        string fieldKey,
        string token,
        IReadOnlyDictionary<string, FieldRecord>? fieldsByKey,
        CancellationToken cancellationToken)
    {
        // Toplu yol (ResolveAll): önceden kurulmuş map'ten O(1) çözüm.
        if (fieldsByKey != null)
            return fieldsByKey.TryGetValue(fieldKey, out var mapped) ? mapped : null;

        // Tekil yol (ResolveAsync): tek alan için enabledIds taraması (eski davranış).
        var enabledIds = MetadataRelationHelper.ParseIdList(context.Workspace.EnabledFieldIds);
        foreach (var fieldId in enabledIds)
        {
            try
            {
                var field = await _metadataCache.GetFieldAsync(fieldId, token, cancellationToken);
                if (string.Equals(field.Key, fieldKey, StringComparison.OrdinalIgnoreCase))
                    return field;
            }
            catch (OperationCoreException ex) when (ex.Code == "FIELD_NOT_FOUND")
            {
                // continue
            }
        }

        return null;
    }

    private FieldBehaviorDto FromFieldDefinition(FieldRecord field)
    {
        var viewGroups = GroupListParser.Parse(field.ViewGroups);
        var editGroups = GroupListParser.Parse(field.EditGroups);

        var visible = viewGroups.Count == 0
            || _requestContext.IsAdmin
            || _requestContext.IsManager
            || GroupListParser.Intersects(_requestContext.UserGroups, viewGroups);

        var canEditField = editGroups.Count == 0
            || _requestContext.IsAdmin
            || _requestContext.IsManager
            || GroupListParser.Intersects(_requestContext.UserGroups, editGroups);

        var required = field.ValidationRules is { ValueKind: JsonValueKind.Object } rules
            && rules.TryGetProperty("required", out var reqProp)
            && reqProp.ValueKind == JsonValueKind.True;

        return new FieldBehaviorDto
        {
            Visible = visible,
            Readonly = !canEditField,
            Required = required,
            Masked = field.IsSensitive == true
        };
    }

    private static FieldBehaviorDto FromBoardVisibility(string fieldName, JsonElement visibleFields)
    {
        if (visibleFields.ValueKind != JsonValueKind.Array)
            return new FieldBehaviorDto { Visible = true };

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in visibleFields.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    allowed.Add(s);
            }
            else if (item.ValueKind == JsonValueKind.Object
                     && item.TryGetProperty("name", out var nameProp)
                     && nameProp.ValueKind == JsonValueKind.String)
            {
                var s = nameProp.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    allowed.Add(s);
            }
        }

        if (allowed.Count == 0)
            return new FieldBehaviorDto { Visible = true };

        return new FieldBehaviorDto { Visible = allowed.Contains(fieldName) };
    }

    private static FieldBehaviorDto FromPermission(bool canEdit, string mode, string fieldName)
    {
        var isEdit = string.Equals(mode, "edit", StringComparison.OrdinalIgnoreCase);
        var readOnly = !canEdit || (isEdit && fieldName.Equals("typeId", StringComparison.OrdinalIgnoreCase));

        return new FieldBehaviorDto
        {
            Visible = true,
            Readonly = readOnly
        };
    }

    private async Task<FieldBehaviorDto> FromRulesAsync(
        FieldBehaviorResolveContext context,
        string fieldName,
        string token,
        IReadOnlyList<RuleRecord>? rules,
        CancellationToken cancellationToken)
    {
        // Toplu yolda kurallar bir kez çekilip paylaşılır; tekil yolda burada alınır (eski davranış).
        rules ??= await _metadataCache.GetRulesForWorkspaceAsync(
            context.Workspace.DataId ?? string.Empty,
            token,
            cancellationToken);
        var workspaceId = context.Workspace.DataId ?? string.Empty;

        var layers = new List<FieldBehaviorDto>();

        foreach (var rule in rules.Where(r =>
                     string.Equals(r.RuleType, RuleTypes.Default, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(r.Trigger, context.RuleTrigger, StringComparison.OrdinalIgnoreCase)
                     && r.IsActive != false
                     && RuleScopeMatcher.Matches(r, new RuleExecutionContext
                     {
                         WorkspaceId = workspaceId,
                         Trigger = context.RuleTrigger,
                         WorkItem = context.WorkItem,
                         TypeId = WorkItemDataHelper.GetString(context.WorkItem, "typeId"),
                         BoardId = WorkItemDataHelper.GetString(context.WorkItem, "boardId"),
                         StateId = context.StateId
                     }, DateTime.UtcNow)))
        {
            if (!RuleConditionEvaluator.Evaluate(rule.Conditions, context.WorkItem))
                continue;

            foreach (var action in RuleActionParser.ParseActions(rule.Actions))
            {
                if (!string.Equals(RuleActionParser.GetActionType(action), "setFieldBehavior", StringComparison.OrdinalIgnoreCase))
                    continue;

                var targetField = RuleActionParser.GetString(action, "field");
                if (!string.Equals(targetField, fieldName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (action.TryGetProperty("behavior", out var behaviorEl) && behaviorEl.ValueKind == JsonValueKind.Object)
                    layers.Add(FieldBehaviorParser.ParseObject(behaviorEl));
            }
        }

        return FieldBehaviorMerger.MergeMany(layers);
    }

    private string RequireToken() =>
        _requestContext.BearerToken
        ?? throw new InvalidOperationException("Bearer token is required for field behavior resolution.");
}
