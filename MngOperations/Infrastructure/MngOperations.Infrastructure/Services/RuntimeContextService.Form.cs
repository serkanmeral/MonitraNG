using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Exceptions;
using MngOperations.Application.FieldBehaviors;
using MngOperations.Application.Models;
using MngOperations.Application.Permissions;
using MngOperations.Application.Rules;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public partial class RuntimeContextService
{
    public Task<FormRuntimeContext> GetFormCreateAsync(
        string workspaceId,
        string? formId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new OperationCoreException(
                "FORM_WORKSPACE_REQUIRED",
                "workspaceId query parameter is required for create form.",
                "Create form için workspaceId zorunludur.",
                400);
        }

        return BuildFormContextAsync(
            "create",
            workspaceId.Trim(),
            workItemId: null,
            workItem: null,
            formId,
            cancellationToken);
    }

    public async Task<FormRuntimeContext> GetFormEditAsync(
        string workItemId,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var workItem = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        return await GetFormEditAsync(workItemId, workItem, cancellationToken);
    }

    /// <summary>Önceden yüklenmiş work item ile (profile-view içinde tekrar DG GetById yapmamak için).</summary>
    private Task<FormRuntimeContext> GetFormEditAsync(
        string workItemId,
        Dictionary<string, object?> workItem,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = WorkItemDataHelper.GetString(workItem, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing.", "workspaceId yok.", 500);

        return BuildFormContextAsync("edit", workspaceId, workItemId, workItem, formId: null, cancellationToken);
    }

    private async Task<FormRuntimeContext> BuildFormContextAsync(
        string mode,
        string workspaceId,
        string? workItemId,
        Dictionary<string, object?>? workItem,
        string? formId,
        CancellationToken cancellationToken)
    {
        var token = RequireToken();
        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);

        if (string.Equals(mode, "create", StringComparison.OrdinalIgnoreCase))
            _permissions.EnsureWorkspace(workspace, WorkspaceAction.Create);
        else
            _permissions.EnsureWorkItemView(workspace, workItem!);

        FormRecord? form = null;
        if (!string.IsNullOrWhiteSpace(formId))
            form = await _metadataCache.GetFormAsync(formId.Trim(), token, cancellationToken);
        else
            form = await _metadataCache.ResolveDefaultFormAsync(workspaceId, token, cancellationToken);

        var poolCatalog = await WorkItemFieldCatalog.BuildEnabledPoolFieldsByKeyAsync(
            workspace, _metadataCache, token, cancellationToken);
        var fieldCatalog = FormFieldCatalog.MergeCoreAndPool(poolCatalog);
        var initialStateId = await ResolveInitialStateIdAsync(workspace, form, workItem, token, cancellationToken);
        var types = await LoadTypeOptionsAsync(workspaceId, workspace, form, token, cancellationToken);
        var isCreate = string.Equals(mode, "create", StringComparison.OrdinalIgnoreCase);
        var canEdit = isCreate || _permissions.CanEditWorkItem(workspace, workItem!);
        var canComment = canEdit
            || (!isCreate && _permissions.CanViewWorkItem(workspace, workItem!));

        var behaviorContext = new FieldBehaviorResolveContext
        {
            Screen = FieldBehaviorScreen.Form,
            Mode = mode,
            Workspace = workspace,
            WorkItem = workItem ?? new Dictionary<string, object?>(),
            Form = form,
            StateId = initialStateId,
            CanEdit = canEdit,
            RuleTrigger = isCreate ? RuleTriggers.WorkItemCreated : RuleTriggers.WorkItemUpdated
        };

        var fieldBehaviors = await _fieldBehaviors.ResolveAllAsync(behaviorContext, cancellationToken);
        var poolFields = await _metadataCache.GetWorkspacePoolFieldsAsync(workspaceId, token, cancellationToken);

        var fields = FormRuntimeBuilder.BuildFields(
            mode,
            workItem,
            form?.DefaultValues,
            form?.Layout,
            fieldCatalog);

        if (isCreate)
        {
            var workspacePolicies = WorkspaceFieldPolicies.Parse(workspace.Settings);
            var policyHints = new WorkspaceFieldPolicies.PolicyEvaluationHints
            {
                StateId = initialStateId,
                TypeId = form?.DefaultTypeId ?? types.FirstOrDefault()?.Id
            };
            var policyDefaults = WorkspaceFieldPolicies.ResolveDefaultValues(
                workspacePolicies,
                workItem ?? new Dictionary<string, object?>(),
                policyHints);

            if (policyDefaults.Count > 0)
            {
                var mutableFields = fields.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
                FormRuntimeBuilder.ApplyDefaultValues(mutableFields, policyDefaults, overwriteExisting: true);
                fields = mutableFields;
            }
        }

        return new FormRuntimeContext
        {
            Mode = mode,
            WorkspaceId = workspaceId,
            WorkItemId = workItemId,
            FormId = form?.DataId,
            FormName = form?.Name,
            DefaultTypeId = form?.DefaultTypeId ?? types.FirstOrDefault()?.Id,
            InitialStateId = initialStateId,
            Layout = form?.Layout,
            Permissions = new RuntimePermissionsDto
            {
                CanView = true,
                CanEdit = canEdit,
                CanComment = canComment
            },
            Types = types,
            Fields = fields,
            FieldBehaviors = fieldBehaviors,
            PoolFields = poolFields
        };
    }

    /// <summary>
    /// Profil detay sekmesi salt okunur görünümü — <c>op_profiles.layout</c> sırası + havuz değerleri.
    /// </summary>
    private async Task<FormRuntimeContext> BuildProfileDisplayFormAsync(
        string workItemId,
        Dictionary<string, object?> workItem,
        ProfileRuntimeContext profile,
        WorkspaceRecord workspace,
        CancellationToken cancellationToken)
    {
        var token = RequireToken();

        var poolCatalog = await WorkItemFieldCatalog.BuildEnabledPoolFieldsByKeyAsync(
            workspace, _metadataCache, token, cancellationToken);
        var fieldCatalog = FormFieldCatalog.MergeCoreAndPool(poolCatalog);

        var fields = FormRuntimeBuilder.BuildFields(
            "edit",
            workItem,
            defaultValues: null,
            profile.Layout,
            fieldCatalog);

        var profileRecord = await _metadataCache.ResolveDefaultProfileAsync(workspace.DataId!, token, cancellationToken);

        var canEdit = profile.Permissions.CanEdit;
        var behaviorContext = new FieldBehaviorResolveContext
        {
            Screen = FieldBehaviorScreen.Profile,
            Mode = "display",
            Workspace = workspace,
            WorkItem = workItem,
            Profile = profileRecord,
            Board = null,
            StateId = WorkItemDataHelper.GetString(workItem, "stateId"),
            CanEdit = canEdit,
            RuleTrigger = RuleTriggers.WorkItemUpdated
        };
        var displayFieldBehaviors = await _fieldBehaviors.ResolveAllAsync(behaviorContext, cancellationToken);

        return new FormRuntimeContext
        {
            Mode = "display",
            WorkspaceId = profile.WorkspaceId,
            WorkItemId = workItemId,
            FormId = profile.ProfileId,
            FormName = profile.ProfileName,
            DefaultTypeId = WorkItemDataHelper.GetString(workItem, "typeId"),
            InitialStateId = WorkItemDataHelper.GetString(workItem, "stateId"),
            Layout = profile.Layout,
            Permissions = profile.Permissions,
            Fields = fields,
            FieldBehaviors = displayFieldBehaviors
        };
    }

    private async Task<string?> ResolveInitialStateIdAsync(
        WorkspaceRecord workspace,
        FormRecord? form,
        Dictionary<string, object?>? workItem,
        string token,
        CancellationToken cancellationToken)
    {
        if (workItem != null)
            return WorkItemDataHelper.GetString(workItem, "stateId");

        if (!string.IsNullOrEmpty(form?.DefaultStateId))
            return form.DefaultStateId;

        var stateFlowId = form?.DefaultStateFlowId ?? workspace.DefaultStateFlowId;
        if (!string.IsNullOrEmpty(stateFlowId))
        {
            var flow = await _metadataCache.GetStateFlowAsync(stateFlowId, token, cancellationToken);
            return flow.InitialStateId;
        }

        if (!string.IsNullOrEmpty(form?.DefaultTypeId))
        {
            var type = await _metadataCache.GetWorkItemTypeAsync(form.DefaultTypeId, token, cancellationToken);
            if (!string.IsNullOrEmpty(type.DefaultStateFlowId))
            {
                var flow = await _metadataCache.GetStateFlowAsync(type.DefaultStateFlowId, token, cancellationToken);
                return flow.InitialStateId;
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<WorkItemTypeOptionDto>> LoadTypeOptionsAsync(
        string workspaceId,
        WorkspaceRecord workspace,
        FormRecord? form,
        string token,
        CancellationToken cancellationToken)
    {
        var enabledIds = MetadataRelationHelper.ParseIdList(workspace.EnabledTypeIds);
        var types = new List<WorkItemTypeOptionDto>();

        if (enabledIds.Count > 0)
        {
            var typeTasks = enabledIds.Select(typeId =>
                _metadataCache.GetWorkItemTypeAsync(typeId, token, cancellationToken));
            var loaded = await Task.WhenAll(typeTasks);
            foreach (var type in loaded)
                types.Add(MapTypeOption(type));

            await EnsureTypeOptionAsync(types, form?.DefaultTypeId, token, cancellationToken);
            return types;
        }

        var filter = $"workspaceId:eq:{workspaceId}";
        var workspaceTypes = await _dg.GetAsync<WorkItemTypeRecord>(
            OcDatasets.WorkItemTypes,
            $"filter={Uri.EscapeDataString(filter)}&limit=100",
            token,
            cancellationToken);

        foreach (var type in workspaceTypes)
            types.Add(MapTypeOption(type));

        if (types.Count == 0 && !string.IsNullOrEmpty(form?.DefaultTypeId))
        {
            var defaultType = await _metadataCache.GetWorkItemTypeAsync(form.DefaultTypeId, token, cancellationToken);
            types.Add(MapTypeOption(defaultType));
        }

        return types;
    }

    /// <summary>
    /// Form varsayılan tipi workspace enabled listesinde yoksa seçeneklere ekler (v-select ham id göstermesin).
    /// </summary>
    private async Task EnsureTypeOptionAsync(
        List<WorkItemTypeOptionDto> types,
        string? typeId,
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return;
        if (types.Any(t => string.Equals(t.Id, typeId, StringComparison.Ordinal))) return;
        var type = await _metadataCache.GetWorkItemTypeAsync(typeId, token, cancellationToken);
        types.Insert(0, MapTypeOption(type));
    }

    private static WorkItemTypeOptionDto MapTypeOption(WorkItemTypeRecord type) =>
        new()
        {
            Id = type.DataId ?? string.Empty,
            Name = type.Name ?? type.DataId ?? string.Empty,
            Category = type.Category
        };
}
