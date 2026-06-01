using MngOperations.Application.Contracts.Runtime;
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
            Username = _requestContext.Username,
            UtcNow = DateTime.UtcNow
        };

        foreach (var definition in definitions)
        {
            widgetResults.Add(await BuildDashboardWidgetAsync(definition, resolveContext, token, cancellationToken));
        }

        // Tüm widget item'larındaki id'leri tek seferde ada çöz (board context deseni); list/summary widget'ları
        // ham id yerine ad/renk gösterir. Katalog = workspace kapsamı (board scope yok → boş scope).
        var allItems = widgetResults
            .Where(w => w.Execution?.Items != null)
            .SelectMany(w => w.Execution!.Items)
            .ToList();

        var people = await ResolvePeopleForCardsAsync(allItems, token, cancellationToken);
        var groups = await ResolveGroupsForCardsAsync(allItems, token, cancellationToken);
        var catalogs = workspace != null
            ? await BuildBoardCatalogsAsync(workspace, workspaceId!, Array.Empty<string>(), token, cancellationToken)
            : new BoardCatalogsDto();

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
        CancellationToken cancellationToken)
    {
        var isChart = definition.WidgetType.Equals("chart", StringComparison.OrdinalIgnoreCase);

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
                QueryKey = definition.QueryKey,
                ChartType = isChart ? definition.ChartType : null,
                GroupBy = isChart ? definition.GroupBy : null
            };
        }

        var rawParams = new Dictionary<string, object?>(definition.Parameters, StringComparer.OrdinalIgnoreCase);
        if (!rawParams.ContainsKey("workspaceId") && !string.IsNullOrEmpty(resolveContext.WorkspaceId))
            rawParams["workspaceId"] = resolveContext.WorkspaceId;

        var resolved = QueryParameterResolver.Resolve(rawParams, resolveContext);
        var executedAt = DateTime.UtcNow;

        try
        {
            // Chart: tam sonuç kümesini groupBy'a göre server-side gruplar (doğru sayım); item listesi döndürmez.
            if (isChart)
            {
                var cards = await ExecuteQueryCardsAsync(
                    definition.QueryKey!,
                    definition.Dataset,
                    rawParams,
                    token,
                    resolveContext,
                    cancellationToken);

                var buckets = AggregateCards(cards, definition.GroupBy);

                return new DashboardWidgetRuntimeDto
                {
                    Key = definition.Key,
                    WidgetType = definition.WidgetType,
                    Title = definition.Title,
                    Dataset = definition.Dataset,
                    QueryKey = definition.QueryKey,
                    ChartType = definition.ChartType,
                    GroupBy = definition.GroupBy,
                    ResolvedParameters = resolved,
                    Execution = new DashboardWidgetExecutionDto
                    {
                        Success = true,
                        Total = cards.Count,
                        Aggregation = buckets,
                        ExecutedAt = executedAt
                    }
                };
            }

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
                ChartType = isChart ? definition.ChartType : null,
                GroupBy = isChart ? definition.GroupBy : null,
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
                ChartType = isChart ? definition.ChartType : null,
                GroupBy = isChart ? definition.GroupBy : null,
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

    /// <summary>
    /// Chart agregasyonu — tam kart kümesini <paramref name="groupBy"/> alanına göre gruplar (sayım).
    /// Desteklenen alanlar: stateId/priorityId/typeId/assignee (varsayılan stateId). Karşılaşma sırası korunur;
    /// boş değerler tek "null" kovasında toplanır. Etiket/renk çözümü UI'da catalog/person ile yapılır.
    /// </summary>
    private static IReadOnlyList<DashboardAggregationBucketDto> AggregateCards(
        IReadOnlyList<WorkItemCardDto> cards,
        string? groupBy)
    {
        var field = string.IsNullOrWhiteSpace(groupBy) ? "stateid" : groupBy.Trim().ToLowerInvariant();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
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
                order.Add(key);
            }
            counts[key]++;
        }

        return order
            .Select(k => new DashboardAggregationBucketDto
            {
                Key = k == noneKey ? null : k,
                Count = counts[k]
            })
            .ToList();
    }
}
