using System.Threading;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Domain.Constants;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Application.Permissions;
using MngOperations.Application.Rules;
using MngOperations.Application.Utilities;
using Microsoft.Extensions.Logging;

namespace MngOperations.Infrastructure.Services;

public partial class RuntimeContextService
{
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
            CurrentUserId = _requestContext.MngPersonId,
            UtcNow = DateTime.UtcNow
        };

        // Katalog çözümü widget sonuçlarına bağlı değil — widget DG sorgularıyla paralel başlat (pano warm süresi).
        var catalogsTask = workspace != null
            ? BuildBoardCatalogsAsync(workspace, workspaceId!, Array.Empty<string>(), token, cancellationToken)
            : Task.FromResult(new BoardCatalogsDto());

        // Aynı queryKey+parametreli widget'lar tek DG çalıştırması paylaşır (Faz 2b dedup).
        var queryResultCache = new Dictionary<string, Task<IReadOnlyList<WorkItemCardDto>>>(StringComparer.Ordinal);

        // Widget sorguları birbirinden bağımsız — kanban kolonları gibi sınırlı paralellik (DG spike önleme).
        const int maxParallelWidgets = 4;
        using var widgetGate = new SemaphoreSlim(maxParallelWidgets);
        var widgetTasks = definitions.Select(async definition =>
        {
            await widgetGate.WaitAsync(cancellationToken);
            try
            {
                return await BuildDashboardWidgetAsync(definition, resolveContext, token, cancellationToken, queryResultCache);
            }
            finally
            {
                widgetGate.Release();
            }
        });
        widgetResults.AddRange(await Task.WhenAll(widgetTasks));

        // Tüm widget item'larındaki id'leri tek seferde ada çöz (board context deseni); list/summary widget'ları
        // ham id yerine ad/renk gösterir. Katalog = workspace kapsamı (board scope yok → boş scope).
        var allItems = widgetResults
            .Where(w => w.Execution?.Items != null)
            .SelectMany(w => w.Execution!.Items)
            .ToList();

        var peopleTask = ResolvePeopleForCardsAsync(allItems, token, cancellationToken);
        var groupsTask = ResolveGroupsForCardsAsync(allItems, token, cancellationToken);
        await Task.WhenAll(catalogsTask, peopleTask, groupsTask);

        var catalogs = catalogsTask.Result;
        var people = peopleTask.Result;
        var groups = groupsTask.Result;

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
            Widgets = widgetResults,
            Catalogs = catalogs,
            People = people,
            Groups = groups
        };
    }

    private async Task<DashboardWidgetRuntimeDto> BuildDashboardWidgetAsync(
        DashboardWidgetDefinition definition,
        QueryResolveContext resolveContext,
        string token,
        CancellationToken cancellationToken,
        Dictionary<string, Task<IReadOnlyList<WorkItemCardDto>>>? queryResultCache = null)
    {
        var isChart = definition.WidgetType.Equals("chart", StringComparison.OrdinalIgnoreCase);

        if (!definition.ExecuteOnLoad
            || string.IsNullOrWhiteSpace(definition.QueryKey)
            || !IsQueryWidgetType(definition.WidgetType))
        {
            return ToDashboardWidgetDto(definition, isChart, resolved: null, execution: null);
        }

        var rawParams = new Dictionary<string, object?>(definition.Parameters, StringComparer.OrdinalIgnoreCase);
        if (!rawParams.ContainsKey("workspaceId") && !string.IsNullOrEmpty(resolveContext.WorkspaceId))
            rawParams["workspaceId"] = resolveContext.WorkspaceId;

        var resolved = QueryParameterResolver.Resolve(rawParams, resolveContext);
        var executedAt = DateTime.UtcNow;

        try
        {
            // Chart: tam sonuç kümesini groupBy'a göre server-side gruplar (doğru sayım); item listesi döndürmez.
            if (isChart || definition.WidgetType.Equals("summaryCard", StringComparison.OrdinalIgnoreCase))
            {
                var cards = await ExecuteQueryCardsAsync(
                    definition.QueryKey!,
                    definition.Dataset,
                    rawParams,
                    token,
                    resolveContext,
                    cancellationToken,
                    queryResultCache);

                if (isChart)
                {
                    var buckets = AggregateCards(cards, definition.GroupBy, definition.Measure);
                    return ToDashboardWidgetDto(
                        definition,
                        isChart,
                        resolved,
                        new DashboardWidgetExecutionDto
                        {
                            Success = true,
                            Total = cards.Count,
                            Aggregation = buckets,
                            ExecutedAt = executedAt
                        });
                }

                var summed = DashboardMeasure.IsSum(definition.Measure);
                var wantsAside = string.Equals(definition.Aside, DashboardMeasure.Unpointed, StringComparison.OrdinalIgnoreCase);
                var points = summed || wantsAside ? DashboardMeasure.SumPoints(cards) : default;
                return ToDashboardWidgetDto(
                    definition,
                    isChart,
                    resolved,
                    new DashboardWidgetExecutionDto
                    {
                        Success = true,
                        Total = cards.Count,
                        Value = summed ? points.Value : null,
                        UnpointedCount = wantsAside ? points.Unpointed : null,
                        ExecutedAt = executedAt
                    });
            }

            // summaryCard yalnızca total sayar — gereksiz kart payload'ı taşımayın.
            var isSummary = definition.WidgetType.Equals("summaryCard", StringComparison.OrdinalIgnoreCase);
            var take = isSummary
                ? 1
                : Math.Clamp(definition.Take <= 0 ? 50 : definition.Take, 1, 50);

            var result = await ExecuteQueryCoreAsync(
                definition.QueryKey!,
                definition.Dataset,
                rawParams,
                definition.Skip,
                take,
                token,
                resolveContext,
                cancellationToken,
                queryResultCache);

            return ToDashboardWidgetDto(
                definition,
                isChart,
                resolved,
                new DashboardWidgetExecutionDto
                {
                    Success = true,
                    Total = result.Total,
                    Skip = result.Skip,
                    Take = result.Take,
                    Items = result.Items,
                    ExecutedAt = executedAt
                },
                dataset: result.Dataset,
                queryKey: result.QueryKey);
        }
        catch (OperationCoreException ex)
        {
            _logger.LogWarning(
                ex,
                "Dashboard widget {WidgetKey} query {QueryKey} failed: {Code}",
                definition.Key,
                definition.QueryKey,
                ex.Code);

            return ToDashboardWidgetDto(
                definition,
                isChart,
                resolved,
                new DashboardWidgetExecutionDto
                {
                    Success = false,
                    ErrorCode = ex.Code,
                    ErrorMessage = ex.MessageTr ?? ex.Message,
                    ExecutedAt = executedAt
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard widget {WidgetKey} query {QueryKey} failed", definition.Key, definition.QueryKey);

            return ToDashboardWidgetDto(
                definition,
                isChart,
                resolved,
                new DashboardWidgetExecutionDto
                {
                    Success = false,
                    ErrorCode = "WIDGET_QUERY_FAILED",
                    ErrorMessage = ex.Message,
                    ExecutedAt = executedAt
                });
        }
    }

    private static DashboardWidgetRuntimeDto ToDashboardWidgetDto(
        DashboardWidgetDefinition definition,
        bool isChart,
        IReadOnlyDictionary<string, object?>? resolved,
        DashboardWidgetExecutionDto? execution,
        string? dataset = null,
        string? queryKey = null)
    {
        var isSummary = definition.WidgetType.Equals("summaryCard", StringComparison.OrdinalIgnoreCase);
        return new DashboardWidgetRuntimeDto
        {
            Key = definition.Key,
            WidgetType = definition.WidgetType,
            Title = definition.Title,
            Dataset = dataset ?? definition.Dataset,
            QueryKey = queryKey ?? definition.QueryKey,
            ChartType = isChart ? definition.ChartType : null,
            GroupBy = isChart ? definition.GroupBy : null,
            AccentColor = isSummary ? definition.AccentColor : null,
            Icon = isSummary ? definition.Icon : null,
            Measure = definition.Measure,
            ResolvedParameters = resolved,
            Execution = execution
        };
    }

    private static bool IsQueryWidgetType(string widgetType) =>
        widgetType.Equals("summaryCard", StringComparison.OrdinalIgnoreCase)
        || widgetType.Equals("list", StringComparison.OrdinalIgnoreCase)
        || widgetType.Equals("chart", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Chart agregasyonu — tam kart kümesini <paramref name="groupBy"/> alanına göre gruplar (sayım).
    /// Desteklenen alanlar: stateId/priorityId/typeId/assignee (varsayılan stateId). Karşılaşma sırası korunur;
    /// boş değerler tek "null" kovasında toplanır. Etiket/renk çözümü UI'da catalog/person ile yapılır.
    /// </summary>
    private static IReadOnlyList<DashboardAggregationBucketDto> AggregateCards(
        IReadOnlyList<WorkItemCardDto> cards,
        string? groupBy,
        string? measure)
    {
        var field = string.IsNullOrWhiteSpace(groupBy) ? "stateid" : groupBy.Trim().ToLowerInvariant();
        var sum = DashboardMeasure.IsSum(measure);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        var order = new List<string>();
        const string noneKey = "\u0000none";

        foreach (var c in cards)
        {
            var raw = field switch
            {
                "priorityid" => c.PriorityId,
                "typeid" => c.TypeId,
                "assignee" => c.Assignee,
                _ => c.StateId
            };
            var key = string.IsNullOrWhiteSpace(raw) ? noneKey : raw;
            if (!counts.ContainsKey(key))
            {
                counts[key] = 0;
                values[key] = 0;
                order.Add(key);
            }
            counts[key]++;
            if (sum)
                values[key] += DashboardMeasure.ReadStoryPoints(c) ?? 0;
        }

        return order
            .Select(k => new DashboardAggregationBucketDto
            {
                Key = k == noneKey ? null : k,
                Count = counts[k],
                Value = sum ? values[k] : null
            })
            .ToList();
    }

    /// <summary>
    /// Workspace-open and active-sprint slices. These are not DataGateway named queries.
    /// An empty active sprint stays empty.
    /// </summary>
    private async Task<IReadOnlyList<WorkItemCardDto>> LoadDashboardScopeAsync(
        string queryKey,
        IReadOnlyDictionary<string, object?> rawParams,
        string token,
        QueryResolveContext resolveContext,
        CancellationToken cancellationToken)
    {
        var merged = new Dictionary<string, object?>(rawParams, StringComparer.OrdinalIgnoreCase);
        var closure = merged.TryGetValue("closure", out var closureValue) ? closureValue?.ToString() : null;
        merged.Remove("closure");
        var resolved = QueryParameterResolver.Resolve(merged, resolveContext);
        var workspaceId = resolved.TryGetValue("workspaceId", out var ws) ? ws?.ToString() : resolveContext.WorkspaceId;
        if (string.IsNullOrWhiteSpace(workspaceId))
            return Array.Empty<WorkItemCardDto>();

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureWorkspace(workspace, WorkspaceAction.View);

        string? sprintId = null;
        if (string.Equals(queryKey, OcQueries.ActiveSprint, StringComparison.OrdinalIgnoreCase))
        {
            sprintId = await FindActiveSprintIdAsync(workspaceId, token, cancellationToken);
            if (string.IsNullOrWhiteSpace(sprintId))
                return Array.Empty<WorkItemCardDto>();
        }

        var filter = string.IsNullOrWhiteSpace(sprintId)
            ? $"filter=workspaceId:eq:{Uri.EscapeDataString(workspaceId)},closedAt:null&limit=1000&expand=false"
            : $"filter=workspaceId:eq:{Uri.EscapeDataString(workspaceId)},sprintId:eq:{Uri.EscapeDataString(sprintId)}&limit=1000&expand=false";
        var rows = await _dg.GetAsync<Dictionary<string, object?>>(OcDatasets.WorkItems, filter, token, cancellationToken);
        var cards = rows.Select(MapWorkItemCard).ToList();
        var mode = closure?.Trim().ToLowerInvariant();
        // closedAt:null is not a valid gateway filter (it needs field:op:value), so open scope is applied here.
        if (string.Equals(queryKey, OcQueries.OpenWorkspace, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(mode))
            mode = "open";
        if (mode == "open")
            cards = cards.Where(card => card.ClosedAt is null).ToList();
        else if (mode == "closed")
            cards = cards.Where(card => card.ClosedAt is not null).ToList();

        await AttachChildCountsAsync(cards, token, cancellationToken);
        return cards;
    }

    private async Task<string?> FindActiveSprintIdAsync(string workspaceId, string token, CancellationToken cancellationToken)
    {
        var filter = $"filter=workspaceId:eq:{Uri.EscapeDataString(workspaceId)},state:eq:active&limit=5&expand=false";
        var rows = await _dg.GetAsync<Dictionary<string, object?>>(OcDatasets.Sprints, filter, token, cancellationToken);
        foreach (var row in rows)
        {
            var id = WorkItemDataHelper.GetDataId(row);
            if (!string.IsNullOrWhiteSpace(id))
                return id;
        }

        return null;
    }
}
