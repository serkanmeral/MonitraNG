using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Exceptions;
using MngOperations.Application.FieldBehaviors;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Permissions;
using MngOperations.Application.Rules;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public class RuntimeContextService : IRuntimeContextService
{
    private readonly IMngDataGatewayClient _dg;
    private readonly IMetadataCache _metadataCache;
    private readonly IPermissionEvaluator _permissions;
    private readonly IFieldBehaviorResolver _fieldBehaviors;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<RuntimeContextService> _logger;

    public RuntimeContextService(
        IMngDataGatewayClient dg,
        IMetadataCache metadataCache,
        IPermissionEvaluator permissions,
        IFieldBehaviorResolver fieldBehaviors,
        IRequestContext requestContext,
        ILogger<RuntimeContextService> logger)
    {
        _dg = dg;
        _metadataCache = metadataCache;
        _permissions = permissions;
        _fieldBehaviors = fieldBehaviors;
        _requestContext = requestContext;
        _logger = logger;
    }

    public async Task<ProfileRuntimeContext> GetProfileAsync(
        string workItemId,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var workItem = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        var workspaceId = WorkItemDataHelper.GetString(workItem, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing.", "workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureWorkItemView(workspace, workItem);

        var stateFlowId = WorkItemDataHelper.GetString(workItem, "stateFlowId");
        var currentStateId = WorkItemDataHelper.GetString(workItem, "stateId") ?? string.Empty;
        var profile = await _metadataCache.ResolveDefaultProfileAsync(workspaceId, token, cancellationToken);

        var availableActions = new List<ProfileActionDto>();
        if (!string.IsNullOrEmpty(stateFlowId))
        {
            var stateFlow = await _metadataCache.GetStateFlowAsync(stateFlowId, token, cancellationToken);
            var order = 0;
            foreach (var transition in _permissions.GetAvailableTransitions(workspace, stateFlow, currentStateId))
            {
                var key = StateFlowCatalog.GetStringProperty(transition, "transitionKey") ?? string.Empty;
                availableActions.Add(new ProfileActionDto
                {
                    TransitionKey = key,
                    Label = StateFlowCatalog.GetStringProperty(transition, "label")
                        ?? StateFlowCatalog.GetStringProperty(transition, "name"),
                    FromStateId = StateFlowCatalog.GetStringProperty(transition, "fromStateId"),
                    ToStateId = StateFlowCatalog.GetStringProperty(transition, "toStateId") ?? string.Empty,
                    Enabled = _permissions.CanApplyTransition(workspace, transition),
                    Order = order++
                });
            }
        }

        var actions = ProfileActionBuilder.Build(availableActions, profile?.Actions);

        var canEdit = _permissions.CanEditWorkItem(workspace, workItem);
        BoardRecord? board = null;
        var boardId = WorkItemDataHelper.GetString(workItem, "boardId");
        if (!string.IsNullOrEmpty(boardId))
        {
            try
            {
                board = await _metadataCache.GetBoardAsync(boardId, token, cancellationToken);
            }
            catch (OperationCoreException ex) when (ex.Code == "BOARD_NOT_FOUND")
            {
                _logger.LogDebug("Board {BoardId} not found for profile field behaviors", boardId);
            }
        }

        var behaviorContext = new FieldBehaviorResolveContext
        {
            Screen = FieldBehaviorScreen.Profile,
            Mode = "edit",
            Workspace = workspace,
            WorkItem = workItem,
            Profile = profile,
            Board = board,
            StateId = currentStateId,
            CanEdit = canEdit,
            RuleTrigger = RuleTriggers.WorkItemUpdated
        };

        var fieldBehaviors = await _fieldBehaviors.ResolveAllAsync(behaviorContext, cancellationToken);

        var linksFilterOut = $"sourceWorkItemId:eq:{workItemId}";
        var linksFilterIn = $"targetWorkItemId:eq:{workItemId}";
        var segmentsFilter = $"workItemId:eq:{workItemId}";

        var outgoingLinksTask = _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.Links,
            $"filter={Uri.EscapeDataString(linksFilterOut)}&limit=50",
            token,
            cancellationToken);

        var incomingLinksTask = _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.Links,
            $"filter={Uri.EscapeDataString(linksFilterIn)}&limit=50",
            token,
            cancellationToken);

        var segmentsTask = _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.WorkItemTimelines,
            $"filter={Uri.EscapeDataString(segmentsFilter)}&limit=200",
            token,
            cancellationToken);

        await Task.WhenAll(outgoingLinksTask, incomingLinksTask, segmentsTask);

        var links = outgoingLinksTask.Result
            .Select(ProfileRuntimeMapper.MapOutgoingLink)
            .Concat(incomingLinksTask.Result.Select(ProfileRuntimeMapper.MapIncomingLink))
            .ToList();

        var stateSegments = segmentsTask.Result
            .Select(ProfileRuntimeMapper.MapStateSegment)
            .OrderByDescending(s => s.EnteredAt)
            .Take(ProfileRuntimeMapper.DefaultStateSegmentCount)
            .OrderBy(s => s.EnteredAt)
            .ToList();

        return new ProfileRuntimeContext
        {
            WorkspaceId = workspaceId,
            WorkItem = ProfileRuntimeBuilder.BuildSummary(workItemId, workItem),
            Permissions = new RuntimePermissionsDto
            {
                CanView = true,
                CanEdit = canEdit,
                CanComment = canEdit || _permissions.CanViewWorkItem(workspace, workItem)
            },
            Actions = actions,
            ProfileId = profile?.DataId,
            ProfileName = profile?.Name,
            Header = profile?.Header,
            Sidebar = profile?.Sidebar,
            Panels = profile?.Panels,
            Layout = profile?.Layout,
            Fields = ProfileRuntimeBuilder.BuildFields(workItem),
            FieldBehaviors = fieldBehaviors,
            Sla = SlaSnapshotHelper.MapFromWorkItem(workItem),
            Watchers = WorkItemDataHelper.GetStringList(workItem, "watchers"),
            Links = links,
            StateSegments = stateSegments
        };
    }

    public async Task<StateSegmentsPage> GetStateSegmentsAsync(
        string workItemId,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var workItem = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        var workspaceId = WorkItemDataHelper.GetString(workItem, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing.", "workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureWorkItemView(workspace, workItem);

        var filter = $"workItemId:eq:{workItemId}";
        var segments = await _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.WorkItemTimelines,
            $"filter={Uri.EscapeDataString(filter)}&limit=200",
            token,
            cancellationToken);

        var items = segments
            .Select(ProfileRuntimeMapper.MapStateSegment)
            .OrderBy(s => s.EnteredAt)
            .ToList();

        return new StateSegmentsPage
        {
            Items = items,
            Total = items.Count
        };
    }

    public async Task<TimelinePage> GetTimelineAsync(
        string workItemId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);

        var workItem = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        var workspaceId = WorkItemDataHelper.GetString(workItem, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing.", "workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureWorkItemView(workspace, workItem);

        var sourceFilter = $"sourceDataset:eq:{OcDatasets.WorkItems},sourceRecordId:eq:{workItemId}";

        var commentsTask = _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.Comments,
            $"filter={Uri.EscapeDataString(sourceFilter)}&limit=500",
            token,
            cancellationToken);

        var activitiesTask = _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.Activities,
            $"filter={Uri.EscapeDataString(sourceFilter)}&limit=500",
            token,
            cancellationToken);

        await Task.WhenAll(commentsTask, activitiesTask);

        var entries = new List<TimelineEntryDto>();

        foreach (var comment in commentsTask.Result)
        {
            entries.Add(new TimelineEntryDto
            {
                Type = "comment",
                Id = WorkItemDataHelper.GetDataId(comment),
                Actor = WorkItemDataHelper.GetString(comment, "author"),
                Text = WorkItemDataHelper.GetString(comment, "body"),
                At = WorkItemDataHelper.GetDateTime(comment, "commentDate")
            });
        }

        foreach (var activity in activitiesTask.Result)
        {
            entries.Add(new TimelineEntryDto
            {
                Type = "activity",
                Id = WorkItemDataHelper.GetDataId(activity),
                Actor = WorkItemDataHelper.GetString(activity, "actor"),
                Text = WorkItemDataHelper.GetString(activity, "message"),
                At = WorkItemDataHelper.GetDateTime(activity, "activityDate"),
                ActivityType = WorkItemDataHelper.GetString(activity, "activityType")
            });
        }

        var ordered = entries
            .OrderByDescending(e => e.At ?? DateTime.MinValue)
            .ToList();

        var page = ordered.Skip(skip).Take(take).ToList();

        return new TimelinePage
        {
            Items = page,
            Skip = skip,
            Take = take,
            Total = ordered.Count
        };
    }

    public async Task<BoardRuntimeContext> GetBoardAsync(
        string boardId,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var board = await _metadataCache.GetBoardAsync(boardId, token, cancellationToken);
        var workspaceId = board.WorkspaceId
            ?? throw new OperationCoreException("BOARD_INVALID", "workspaceId missing on board.", "Board workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureBoardView(workspace, board);

        var stateFlowId = board.DefaultStateFlowId ?? workspace.DefaultStateFlowId;
        JsonElement? transitions = null;
        string? initialStateId = null;

        if (!string.IsNullOrEmpty(stateFlowId))
        {
            var stateFlow = await _metadataCache.GetStateFlowAsync(stateFlowId, token, cancellationToken);
            transitions = stateFlow.Transitions;
            initialStateId = stateFlow.InitialStateId;
        }

        JsonElement? configColumns = null;
        if (board.Config is { ValueKind: JsonValueKind.Object } config
            && config.TryGetProperty("columns", out var configCols))
        {
            configColumns = configCols;
        }

        var boardColumns = BoardColumnBuilder.Build(
            transitions,
            initialStateId,
            workspaceId,
            boardId,
            configColumns);

        return new BoardRuntimeContext
        {
            BoardId = boardId,
            WorkspaceId = workspaceId,
            Name = board.Name,
            ViewType = board.ViewType,
            Permissions = new RuntimePermissionsDto
            {
                CanView = true,
                CanEdit = _permissions.CanEditWorkItem(workspace, new Dictionary<string, object?>()),
                CanComment = _permissions.CanEditWorkItem(workspace, new Dictionary<string, object?>())
            },
            Columns = boardColumns,
            CardFieldKeys = ParseCardFieldKeys(board.VisibleFields)
        };
    }

    public async Task<QueryExecuteResponse> ExecuteQueryAsync(
        string queryKey,
        ExecuteQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var dataset = string.IsNullOrWhiteSpace(request.Dataset) ? OcDatasets.WorkItems : request.Dataset.Trim();
        var take = Math.Clamp(request.Take, 1, 200);
        var skip = Math.Max(0, request.Skip);

        var rawParams = ParseQueryParameters(request.Parameters);
        var workspaceHint = rawParams.TryGetValue("workspaceId", out var ws) ? ws?.ToString() : null;
        var boardHint = rawParams.TryGetValue("boardId", out var b) ? b?.ToString() : null;

        return await ExecuteQueryCoreAsync(
            queryKey,
            dataset,
            rawParams,
            skip,
            take,
            token,
            new QueryResolveContext
            {
                WorkspaceId = workspaceHint,
                BoardId = boardHint,
                Username = _requestContext.Username,
                UtcNow = DateTime.UtcNow
            },
            cancellationToken);
    }

    public async Task<DashboardRuntimeContext> GetDashboardAsync(
        string dashboardId,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var dashboard = await _metadataCache.GetDashboardAsync(dashboardId, token, cancellationToken);

        if (dashboard.IsActive == false)
        {
            throw new OperationCoreException(
                "DASHBOARD_INACTIVE",
                "Dashboard is not active.",
                "Dashboard aktif değil.",
                404);
        }

        var workspaceId = dashboard.WorkspaceId;
        WorkspaceRecord? workspace = null;
        if (!string.IsNullOrEmpty(workspaceId))
        {
            workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
            _permissions.EnsureDashboardView(workspace, dashboard);
        }

        var definitions = DashboardWidgetParser.Parse(dashboard.Widgets);
        var widgetResults = new List<DashboardWidgetRuntimeDto>();
        var resolveContext = new QueryResolveContext
        {
            WorkspaceId = workspaceId,
            Username = _requestContext.Username,
            UtcNow = DateTime.UtcNow
        };

        foreach (var definition in definitions)
        {
            widgetResults.Add(await BuildDashboardWidgetAsync(definition, resolveContext, token, cancellationToken));
        }

        var canEdit = workspace != null
            && _permissions.CanEditWorkItem(workspace, new Dictionary<string, object?>());

        return new DashboardRuntimeContext
        {
            DashboardId = dashboardId,
            WorkspaceId = workspaceId,
            Name = dashboard.Name,
            Description = dashboard.Description,
            Scope = dashboard.Scope,
            Layout = dashboard.Layout,
            Permissions = new RuntimePermissionsDto
            {
                CanView = true,
                CanEdit = canEdit,
                CanComment = canEdit
            },
            Widgets = widgetResults
        };
    }

    private async Task<DashboardWidgetRuntimeDto> BuildDashboardWidgetAsync(
        DashboardWidgetDefinition definition,
        QueryResolveContext resolveContext,
        string token,
        CancellationToken cancellationToken)
    {
        if (!definition.ExecuteOnLoad
            || string.IsNullOrWhiteSpace(definition.QueryKey)
            || !IsQueryWidgetType(definition.WidgetType))
        {
            return new DashboardWidgetRuntimeDto
            {
                Key = definition.Key,
                WidgetType = definition.WidgetType,
                Title = definition.Title,
                Dataset = definition.Dataset,
                QueryKey = definition.QueryKey
            };
        }

        var rawParams = new Dictionary<string, object?>(definition.Parameters, StringComparer.OrdinalIgnoreCase);
        if (!rawParams.ContainsKey("workspaceId") && !string.IsNullOrEmpty(resolveContext.WorkspaceId))
            rawParams["workspaceId"] = resolveContext.WorkspaceId;

        var resolved = QueryParameterResolver.Resolve(rawParams, resolveContext);
        var executedAt = DateTime.UtcNow;

        try
        {
            var take = definition.WidgetType.Equals("summaryCard", StringComparison.OrdinalIgnoreCase)
                ? Math.Clamp(definition.Take, 1, 200)
                : Math.Clamp(definition.Take, 1, 50);

            var result = await ExecuteQueryCoreAsync(
                definition.QueryKey!,
                definition.Dataset,
                rawParams,
                definition.Skip,
                take,
                token,
                resolveContext,
                cancellationToken);

            return new DashboardWidgetRuntimeDto
            {
                Key = definition.Key,
                WidgetType = definition.WidgetType,
                Title = definition.Title,
                Dataset = result.Dataset,
                QueryKey = result.QueryKey,
                ResolvedParameters = resolved,
                Execution = new DashboardWidgetExecutionDto
                {
                    Success = true,
                    Total = result.Total,
                    Skip = result.Skip,
                    Take = result.Take,
                    Items = result.Items,
                    ExecutedAt = executedAt
                }
            };
        }
        catch (OperationCoreException ex)
        {
            _logger.LogWarning(
                ex,
                "Dashboard widget {WidgetKey} query {QueryKey} failed: {Code}",
                definition.Key,
                definition.QueryKey,
                ex.Code);

            return new DashboardWidgetRuntimeDto
            {
                Key = definition.Key,
                WidgetType = definition.WidgetType,
                Title = definition.Title,
                Dataset = definition.Dataset,
                QueryKey = definition.QueryKey,
                ResolvedParameters = resolved,
                Execution = new DashboardWidgetExecutionDto
                {
                    Success = false,
                    ErrorCode = ex.Code,
                    ErrorMessage = ex.MessageTr ?? ex.Message,
                    ExecutedAt = executedAt
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard widget {WidgetKey} query {QueryKey} failed", definition.Key, definition.QueryKey);

            return new DashboardWidgetRuntimeDto
            {
                Key = definition.Key,
                WidgetType = definition.WidgetType,
                Title = definition.Title,
                Dataset = definition.Dataset,
                QueryKey = definition.QueryKey,
                ResolvedParameters = resolved,
                Execution = new DashboardWidgetExecutionDto
                {
                    Success = false,
                    ErrorCode = "WIDGET_QUERY_FAILED",
                    ErrorMessage = ex.Message,
                    ExecutedAt = executedAt
                }
            };
        }
    }

    private static bool IsQueryWidgetType(string widgetType) =>
        widgetType.Equals("summaryCard", StringComparison.OrdinalIgnoreCase)
        || widgetType.Equals("list", StringComparison.OrdinalIgnoreCase)
        || widgetType.Equals("chart", StringComparison.OrdinalIgnoreCase);

    private async Task<QueryExecuteResponse> ExecuteQueryCoreAsync(
        string queryKey,
        string dataset,
        IReadOnlyDictionary<string, object?> rawParams,
        int skip,
        int take,
        string token,
        QueryResolveContext resolveContext,
        CancellationToken cancellationToken)
    {
        if (!OcQueries.IsAllowed(dataset, queryKey))
        {
            throw new OperationCoreException(
                "QUERY_NOT_ALLOWED",
                $"Query '{queryKey}' is not allowed on dataset '{dataset}'.",
                $"'{queryKey}' sorgusu '{dataset}' üzerinde çalıştırılamaz.",
                400);
        }

        var takeClamped = Math.Clamp(take, 1, 200);
        var skipClamped = Math.Max(0, skip);
        var mergedParams = new Dictionary<string, object?>(rawParams, StringComparer.OrdinalIgnoreCase);

        var resolved = QueryParameterResolver.Resolve(mergedParams, resolveContext);
        var workspaceId = resolved.TryGetValue("workspaceId", out var ws) ? ws?.ToString() : resolveContext.WorkspaceId;
        var boardId = resolved.TryGetValue("boardId", out var b) ? b?.ToString() : resolveContext.BoardId;

        if (!string.IsNullOrEmpty(workspaceId))
        {
            var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
            _permissions.EnsureWorkspace(workspace, WorkspaceAction.View);

            if (!string.IsNullOrEmpty(boardId))
            {
                var board = await _metadataCache.GetBoardAsync(boardId, token, cancellationToken);
                _permissions.EnsureBoardView(workspace, board);
            }
        }

        var rows = await _dg.ExecuteQueryAsync(dataset, queryKey, resolved, token, cancellationToken);
        var cards = rows.Select(MapWorkItemCard).ToList();
        var page = cards.Skip(skipClamped).Take(takeClamped).ToList();

        return new QueryExecuteResponse
        {
            Dataset = dataset,
            QueryKey = queryKey,
            Items = page,
            Skip = skipClamped,
            Take = takeClamped,
            Total = cards.Count
        };
    }

    private static Dictionary<string, object?> ParseQueryParameters(Dictionary<string, JsonElement>? parameters)
    {
        var rawParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (parameters == null)
            return rawParams;

        foreach (var (key, value) in parameters)
        {
            rawParams[key] = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.TryGetInt64(out var l) ? l : value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => JsonSerializer.Deserialize<object?>(value.GetRawText())
            };
        }

        return rawParams;
    }

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
        var workspaceId = WorkItemDataHelper.GetString(workItem, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing.", "workspaceId yok.", 500);

        return await BuildFormContextAsync("edit", workspaceId, workItemId, workItem, formId: null, cancellationToken);
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
            Fields = FormRuntimeBuilder.BuildFields(
                mode,
                workItem,
                form?.DefaultValues,
                form?.Layout,
                fieldCatalog),
            FieldBehaviors = fieldBehaviors
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
            foreach (var typeId in enabledIds)
            {
                var type = await _metadataCache.GetWorkItemTypeAsync(typeId, token, cancellationToken);
                types.Add(MapTypeOption(type));
            }

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

    private static WorkItemTypeOptionDto MapTypeOption(WorkItemTypeRecord type) =>
        new()
        {
            Id = type.DataId ?? string.Empty,
            Name = type.Name ?? type.DataId ?? string.Empty,
            Category = type.Category
        };

    private static WorkItemCardDto MapWorkItemCard(IReadOnlyDictionary<string, object?> row) =>
        new()
        {
            Id = WorkItemDataHelper.GetDataId(row),
            Key = WorkItemDataHelper.GetString(row, "key") ?? string.Empty,
            Title = WorkItemDataHelper.GetString(row, "title") ?? string.Empty,
            StateId = WorkItemDataHelper.GetString(row, "stateId"),
            Assignee = WorkItemDataHelper.GetString(row, "assignee"),
            PriorityId = WorkItemDataHelper.GetString(row, "priorityId"),
            TypeId = WorkItemDataHelper.GetString(row, "typeId")
        };

    private static IReadOnlyList<string> ParseCardFieldKeys(JsonElement? visibleFields)
    {
        if (visibleFields is not { ValueKind: JsonValueKind.Array })
            return new[] { "title", "assignee", "priorityId", "key" };

        var keys = new List<string>();
        foreach (var item in visibleFields.Value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    keys.Add(s);
            }
            else if (item.ValueKind == JsonValueKind.Object
                     && item.TryGetProperty("name", out var nameProp)
                     && nameProp.ValueKind == JsonValueKind.String)
            {
                var s = nameProp.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    keys.Add(s);
            }
        }

        return keys.Count > 0 ? keys : new[] { "title", "assignee", "priorityId", "key" };
    }

    private async Task<Dictionary<string, object?>> LoadWorkItemAsync(
        string workItemId,
        string token,
        CancellationToken cancellationToken)
    {
        var item = await _dg.GetByIdAsync<Dictionary<string, object?>>(OcDatasets.WorkItems, workItemId, token, cancellationToken);
        if (item == null)
        {
            throw new OperationCoreException(
                "WORK_ITEM_NOT_FOUND",
                $"Work item '{workItemId}' not found.",
                $"İş kaydı '{workItemId}' bulunamadı.",
                404);
        }

        return item;
    }

    private string RequireToken()
    {
        if (string.IsNullOrEmpty(_requestContext.BearerToken))
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
