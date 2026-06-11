using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public partial class MetadataCacheService : IMetadataCache
{
    private readonly IMemoryCache _cache;
    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<MetadataCacheService> _logger;
    private readonly TimeSpan _ttl;
    private readonly TimeSpan _catalogTtl;

    public MetadataCacheService(
        IMemoryCache cache,
        IMngDataGatewayClient dg,
        IRequestContext requestContext,
        ILogger<MetadataCacheService> logger,
        IOptions<MngOperationsSettings> settings)
    {
        _cache = cache;
        _dg = dg;
        _requestContext = requestContext;
        _logger = logger;
        _ttl = TimeSpan.FromSeconds(Math.Max(30, settings.Value.MetadataCache.TtlSeconds));
        _catalogTtl = TimeSpan.FromSeconds(Math.Max(30, settings.Value.MetadataCache.CatalogTtlSeconds));
    }

    public Task<WorkspaceRecord> GetWorkspaceAsync(string workspaceId, string token, CancellationToken cancellationToken = default) =>
        GetOrLoadAsync(
            CacheKey($"workspace:{workspaceId}"),
            () => _dg.GetByIdAsync<WorkspaceRecord>(OcDatasets.Workspaces, workspaceId, token, cancellationToken),
            "WORKSPACE_NOT_FOUND",
            $"Workspace '{workspaceId}' not found.",
            $"Workspace '{workspaceId}' bulunamadı.",
            cancellationToken);

    public Task<WorkItemTypeRecord> GetWorkItemTypeAsync(string typeId, string token, CancellationToken cancellationToken = default) =>
        GetOrLoadAsync(
            CacheKey($"type:{typeId}"),
            () => _dg.GetByIdAsync<WorkItemTypeRecord>(OcDatasets.WorkItemTypes, typeId, token, cancellationToken),
            "WORK_ITEM_TYPE_NOT_FOUND",
            $"Work item type '{typeId}' not found.",
            $"İş tipi '{typeId}' bulunamadı.",
            cancellationToken);

    public Task<StateFlowRecord> GetStateFlowAsync(string stateFlowId, string token, CancellationToken cancellationToken = default) =>
        GetOrLoadAsync(
            CacheKey($"flow:{stateFlowId}"),
            () => _dg.GetByIdAsync<StateFlowRecord>(OcDatasets.StateFlows, stateFlowId, token, cancellationToken),
            "STATE_FLOW_NOT_FOUND",
            $"State flow '{stateFlowId}' not found.",
            $"State flow '{stateFlowId}' bulunamadı.",
            cancellationToken);

    public Task<BoardRecord> GetBoardAsync(string boardId, string token, CancellationToken cancellationToken = default) =>
        GetOrLoadAsync(
            CacheKey($"board:{boardId}"),
            () => _dg.GetByIdAsync<BoardRecord>(OcDatasets.Boards, boardId, token, cancellationToken),
            "BOARD_NOT_FOUND",
            $"Board '{boardId}' not found.",
            $"Board '{boardId}' bulunamadı.",
            cancellationToken);

    public async Task<FormRecord?> ResolveDefaultFormAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey($"form:default:{workspaceId}");
        if (_cache.TryGetValue(cacheKey, out FormRecord? cached))
            return cached;

        var filter = $"workspaceId:eq:{workspaceId}";
        var forms = (await _dg.GetAsync<FormRecord>(
            OcDatasets.Forms,
            $"filter={Uri.EscapeDataString(filter)}&limit=50",
            token,
            cancellationToken)).ToList();

        var selected = forms.FirstOrDefault(f => f.IsDefault == true) ?? forms.FirstOrDefault();
        if (selected != null)
            _cache.Set(cacheKey, selected, _ttl);

        return selected;
    }

    public Task<FormRecord> GetFormAsync(string formId, string token, CancellationToken cancellationToken = default) =>
        GetOrLoadAsync(
            CacheKey($"form:{formId}"),
            () => _dg.GetByIdAsync<FormRecord>(OcDatasets.Forms, formId, token, cancellationToken),
            "FORM_NOT_FOUND",
            $"Form '{formId}' not found.",
            $"Form '{formId}' bulunamadı.",
            cancellationToken);

    public async Task<IReadOnlyList<RuleRecord>> GetRulesForWorkspaceAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey($"rules:{workspaceId}");
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<RuleRecord>? cached) && cached != null)
            return cached;

        var filter = $"workspaceId:eq:{workspaceId}";
        var rules = (await _dg.GetAsync<RuleRecord>(
            OcDatasets.Rules,
            $"filter={Uri.EscapeDataString(filter)}&limit=500",
            token,
            cancellationToken)).ToList();

        _cache.Set(cacheKey, rules, _ttl);
        _logger.LogDebug("Metadata cache set {CacheKey} ({Count} rules)", cacheKey, rules.Count);
        return rules;
    }

    public async Task<IReadOnlyList<WorkspaceAutomationRecord>> GetWorkspaceAutomationsForWorkspaceAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey($"automations:{workspaceId}");
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<WorkspaceAutomationRecord>? cached) && cached != null)
            return cached;

        var filter = $"workspaceId:eq:{workspaceId}";
        var automations = (await _dg.GetAsync<WorkspaceAutomationRecord>(
            OcDatasets.WorkspaceAutomations,
            $"filter={Uri.EscapeDataString(filter)}&limit=200",
            token,
            cancellationToken)).ToList();

        _cache.Set(cacheKey, automations, _ttl);
        _logger.LogDebug("Metadata cache set {CacheKey} ({Count} automations)", cacheKey, automations.Count);
        return automations;
    }

    public Task<FieldRecord> GetFieldAsync(string fieldId, string token, CancellationToken cancellationToken = default) =>
        GetOrLoadAsync(
            CacheKey($"field:{fieldId}"),
            () => _dg.GetByIdAsync<FieldRecord>(OcDatasets.Fields, fieldId, token, cancellationToken),
            "FIELD_NOT_FOUND",
            $"Field '{fieldId}' not found.",
            $"Alan '{fieldId}' bulunamadı.",
            cancellationToken);

    public async Task<FieldRecord?> FindFieldByKeyAsync(
        string fieldKey,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
            return null;

        var cacheKey = CacheKey($"field:key:{fieldKey.Trim()}");
        if (_cache.TryGetValue(cacheKey, out FieldRecord? cached))
            return cached;

        var filter = $"key:eq:{fieldKey.Trim()}";
        var matches = (await _dg.GetAsync<FieldRecord>(
            OcDatasets.Fields,
            $"filter={Uri.EscapeDataString(filter)}&limit=5",
            token,
            cancellationToken)).ToList();

        var field = matches.FirstOrDefault(f =>
            string.Equals(f.Key, fieldKey.Trim(), StringComparison.OrdinalIgnoreCase));

        if (field != null)
            _cache.Set(cacheKey, field, _ttl);

        return field;
    }

    public async Task<ProfileRecord?> ResolveDefaultProfileAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey($"profile:default:{workspaceId}");
        if (_cache.TryGetValue(cacheKey, out ProfileRecord? cached))
            return cached;

        var filter = $"workspaceId:eq:{workspaceId}";
        var profiles = (await _dg.GetAsync<ProfileRecord>(
            OcDatasets.Profiles,
            $"filter={Uri.EscapeDataString(filter)}&limit=50",
            token,
            cancellationToken)).ToList();

        var selected = profiles.FirstOrDefault(p => p.IsDefault == true) ?? profiles.FirstOrDefault();
        if (selected != null)
            _cache.Set(cacheKey, selected, _ttl);

        return selected;
    }

    public Task<StateRecord> GetStateAsync(string stateId, string token, CancellationToken cancellationToken = default) =>
        GetOrLoadAsync(
            CacheKey($"state:{stateId}"),
            () => _dg.GetByIdAsync<StateRecord>(OcDatasets.States, stateId, token, cancellationToken),
            "STATE_NOT_FOUND",
            $"State '{stateId}' not found.",
            $"State '{stateId}' bulunamadı.",
            cancellationToken);

    public async Task<IReadOnlyList<SlaPolicyRecord>> GetSlaPoliciesForWorkspaceAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey($"sla:policies:{workspaceId}");
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<SlaPolicyRecord>? cached) && cached != null)
            return cached;

        var filter = $"workspaceId:eq:{workspaceId}";
        var policies = (await _dg.GetAsync<SlaPolicyRecord>(
            OcDatasets.SlaPolicies,
            $"filter={Uri.EscapeDataString(filter)}&limit=100",
            token,
            cancellationToken)).ToList();
        _cache.Set(cacheKey, (IReadOnlyList<SlaPolicyRecord>)policies, _ttl);
        return policies;
    }

    public async Task<SlaPolicyRecord?> ResolveSlaPolicyAsync(
        string workspaceId,
        string typeId,
        string? priorityId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetSlaPoliciesForWorkspaceAsync(workspaceId, token, cancellationToken);

        SlaPolicyRecord? best = null;
        var bestScore = -1;

        foreach (var policy in cached)
        {
            if (policy.IsActive == false)
                continue;

            if (!string.IsNullOrEmpty(policy.TypeId)
                && !string.Equals(policy.TypeId, typeId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(policy.PriorityId)
                && !string.Equals(policy.PriorityId, priorityId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var score = 0;
            if (!string.IsNullOrEmpty(policy.TypeId))
                score += 2;
            if (!string.IsNullOrEmpty(policy.PriorityId))
                score += 1;

            if (score > bestScore)
            {
                bestScore = score;
                best = policy;
            }
            else if (score == bestScore && best != null)
            {
                var policyRank = policy.Priority ?? 0;
                var bestRank = best.Priority ?? 0;
                if (policyRank > bestRank)
                    best = policy;
            }
        }

        return best;
    }

    public Task<DashboardRecord> GetDashboardAsync(string dashboardId, string token, CancellationToken cancellationToken = default) =>
        GetOrLoadAsync(
            CacheKey($"dashboard:{dashboardId}"),
            () => _dg.GetByIdAsync<DashboardRecord>(OcDatasets.Dashboards, dashboardId, token, cancellationToken),
            "DASHBOARD_NOT_FOUND",
            $"Dashboard '{dashboardId}' not found.",
            $"Dashboard '{dashboardId}' bulunamadı.",
            cancellationToken);

    public async Task<IReadOnlyList<NotificationPolicyRecord>> GetNotificationPoliciesForWorkspaceAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey($"notification-policies:{workspaceId}");
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<NotificationPolicyRecord>? cached) && cached != null)
            return cached;

        var filter = $"workspaceId:eq:{workspaceId}";
        var policies = (await _dg.GetAsync<NotificationPolicyRecord>(
            OcDatasets.NotificationPolicies,
            $"filter={Uri.EscapeDataString(filter)}&limit=200",
            token,
            cancellationToken)).ToList();

        _cache.Set(cacheKey, policies, _ttl);
        return policies;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetCatalogListAsync(
        string dataset,
        string token,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CatalogCacheKey(dataset);
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Dictionary<string, object?>>? cached) && cached != null)
            return cached;

        var rows = (await _dg.GetAsync<Dictionary<string, object?>>(
            dataset,
            "limit=500",
            token,
            cancellationToken)).ToList();

        _cache.Set(cacheKey, (IReadOnlyList<Dictionary<string, object?>>)rows, _catalogTtl);
        _logger.LogDebug("Catalog cache set {CacheKey} ({Count} rows)", cacheKey, rows.Count);
        return rows;
    }

    public void InvalidateCatalog(string dataset)
    {
        var cacheKey = CatalogCacheKey(dataset);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Catalog cache invalidated {CacheKey}", cacheKey);
    }

    public async Task<MetadataCacheReloadResult> ReloadWorkspaceAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new OperationCoreException(
                "WORKSPACE_ID_REQUIRED",
                "workspaceId is required.",
                "workspaceId zorunludur.",
                400);
        }

        var wsId = workspaceId.Trim();
        var removed = 0;

        void Remove(string suffix)
        {
            _cache.Remove(CacheKey(suffix));
            removed++;
        }

        Remove($"workspace:{wsId}");
        Remove($"form:default:{wsId}");
        Remove($"rules:{wsId}");
        Remove($"automations:{wsId}");
        Remove($"profile:default:{wsId}");
        Remove($"sla:policies:{wsId}");
        Remove($"notification-policies:{wsId}");
        Remove($"poolfields:{wsId}");

        var filter = $"workspaceId:eq:{wsId}";
        var filterQuery = $"filter={Uri.EscapeDataString(filter)}&limit=200";

        var workspace = await _dg.GetByIdAsync<WorkspaceRecord>(
            OcDatasets.Workspaces,
            wsId,
            token,
            cancellationToken);

        if (workspace == null)
        {
            throw new OperationCoreException(
                "WORKSPACE_NOT_FOUND",
                $"Workspace '{wsId}' not found.",
                $"Workspace '{wsId}' bulunamadı.",
                404);
        }

        if (!string.IsNullOrWhiteSpace(workspace.DefaultStateFlowId))
            Remove($"flow:{workspace.DefaultStateFlowId.Trim()}");

        foreach (var form in await _dg.GetAsync<FormRecord>(OcDatasets.Forms, filterQuery, token, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(form.DataId))
                Remove($"form:{form.DataId.Trim()}");

            if (!string.IsNullOrWhiteSpace(form.DefaultStateFlowId))
                Remove($"flow:{form.DefaultStateFlowId.Trim()}");
        }

        foreach (var board in await _dg.GetAsync<BoardRecord>(OcDatasets.Boards, filterQuery, token, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(board.DataId))
                Remove($"board:{board.DataId.Trim()}");
        }

        foreach (var dashboard in await _dg.GetAsync<DashboardRecord>(OcDatasets.Dashboards, filterQuery, token, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(dashboard.DataId))
                Remove($"dashboard:{dashboard.DataId.Trim()}");
        }

        var flowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in await _dg.GetAsync<WorkItemTypeRecord>(OcDatasets.WorkItemTypes, filterQuery, token, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(type.DataId))
                Remove($"type:{type.DataId.Trim()}");

            if (!string.IsNullOrWhiteSpace(type.DefaultStateFlowId))
                flowIds.Add(type.DefaultStateFlowId.Trim());
        }

        foreach (var typeId in MetadataRelationHelper.ParseIdList(workspace.EnabledTypeIds))
        {
            Remove($"type:{typeId}");
        }

        foreach (var flow in await _dg.GetAsync<StateFlowRecord>(OcDatasets.StateFlows, filterQuery, token, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(flow.DataId))
                flowIds.Add(flow.DataId.Trim());
        }

        foreach (var flowId in flowIds)
            Remove($"flow:{flowId}");

        var fieldIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fieldId in MetadataRelationHelper.ParseIdList(workspace.EnabledFieldIds))
            fieldIds.Add(fieldId);

        var scopedFields = (await _dg.GetAsync<FieldRecord>(OcDatasets.Fields, filterQuery, token, cancellationToken)).ToList();
        foreach (var field in scopedFields)
        {
            if (!string.IsNullOrWhiteSpace(field.DataId))
                fieldIds.Add(field.DataId.Trim());
        }

        foreach (var fieldId in fieldIds)
            Remove($"field:{fieldId}");

        foreach (var field in scopedFields)
        {
            if (!string.IsNullOrWhiteSpace(field.Key))
                Remove($"field:key:{field.Key.Trim()}");
        }

        foreach (var fieldId in MetadataRelationHelper.ParseIdList(workspace.EnabledFieldIds))
        {
            try
            {
                var field = await _dg.GetByIdAsync<FieldRecord>(
                    OcDatasets.Fields,
                    fieldId,
                    token,
                    cancellationToken);
                if (!string.IsNullOrWhiteSpace(field?.Key))
                    Remove($"field:key:{field.Key.Trim()}");
            }
            catch (OperationCoreException ex) when (ex.Code == "FIELD_NOT_FOUND")
            {
                // enabledFieldIds'te kırık referans — yoksay
            }
        }

        _logger.LogInformation(
            "Metadata cache reloaded for workspace {WorkspaceId} ({KeysRemoved} keys removed)",
            wsId,
            removed);

        return new MetadataCacheReloadResult
        {
            WorkspaceId = wsId,
            KeysRemoved = removed
        };
    }

    private string CatalogCacheKey(string dataset) => CacheKey($"catalog:{dataset}");

    private async Task<T> GetOrLoadAsync<T>(
        string cacheKey,
        Func<Task<T?>> loader,
        string notFoundCode,
        string notFoundMessage,
        string? notFoundMessageTr,
        CancellationToken cancellationToken) where T : class
    {
        if (_cache.TryGetValue(cacheKey, out T? cached) && cached != null)
            return cached;

        var record = await loader();
        if (record == null)
            throw new OperationCoreException(notFoundCode, notFoundMessage, notFoundMessageTr, 404);

        _cache.Set(cacheKey, record, _ttl);
        _logger.LogDebug("Metadata cache set {CacheKey}", cacheKey);
        return record;
    }

    private string CacheKey(string suffix) =>
        $"oc:{_requestContext.DomainId ?? "unknown"}:{suffix}";
}
