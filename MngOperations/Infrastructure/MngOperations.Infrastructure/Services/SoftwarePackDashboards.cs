namespace MngOperations.Infrastructure.Services;

/// <summary>
/// Yazılım paketi panoları. Durum kimlikleri kurulumda yerine yazılır.
/// </summary>
public static class SoftwarePackDashboards
{
    public static (Dictionary<string, object?> Layout, object Widgets) Build(
        string kind,
        IReadOnlyDictionary<string, string> stateIds)
    {
        return kind.Trim().ToLowerInvariant() switch
        {
            "sprint" => Sprint(),
            _ => Summary(stateIds)
        };
    }

    private static (Dictionary<string, object?> Layout, object Widgets) Summary(
        IReadOnlyDictionary<string, string> stateIds)
    {
        var widgets = new object[]
        {
            CountCard("state_new", "Yeni", "mdi-folder-open-outline", "secondary", stateIds["state.new"]),
            CountCard("state_dev", "Geliştirmede", "mdi-progress-clock", "primary", stateIds["state.dev"]),
            CountCard("state_test", "Testte", "mdi-flag-outline", "warning", stateIds["state.test"]),
            new Dictionary<string, object?>
            {
                ["key"] = "open_points",
                ["type"] = "summaryCard",
                ["title"] = "Açık puan",
                ["icon"] = "mdi-chart-line",
                ["accentColor"] = "success",
                ["dataset"] = "op_work_items",
                ["queryKey"] = "wi_open_workspace",
                ["parameters"] = new Dictionary<string, object?> { ["workspaceId"] = "{{currentWorkspace}}" },
                ["measure"] = "sum",
                ["measureField"] = "storyPoints",
                ["aside"] = "unpointed"
            },
            Chart("points_by_state", "Açık puan", "bar", "stateId", "wi_open_workspace", null, sum: true),
            Chart("new_by_priority", "Yeni — öncelik", "bar", "priorityId", "wi_by_workspace_and_state", stateIds["state.new"], sum: false),
            List("mine_list", "Bana atanan açık", "wi_assigned_open", null)
        };

        var layout = Rows(
            Row(
                Col("state_new", 6, 4, 3),
                Col("state_dev", 6, 4, 3),
                Col("state_test", 6, 4, 3),
                Col("open_points", 6, 12, 3)),
            Row(Col("points_by_state", 12, 6), Col("new_by_priority", 12, 6)),
            Row(Col("mine_list", 12, 12)));
        return (layout, widgets);
    }

    private static (Dictionary<string, object?> Layout, object Widgets) Sprint()
    {
        var widgets = new object[]
        {
            SprintCard("sprint_count", "Sprintteki iş", "mdi-counter", "primary", "all", "count"),
            SprintCard("sprint_open_points", "Açık puan", "mdi-chart-line", "success", "open", "sum"),
            SprintCard("sprint_closed_points", "Kapanan puan", "mdi-check-circle-outline", "secondary", "closed", "sum"),
            Chart("sprint_by_state", "Durum", "bar", "stateId", "wi_active_sprint", null, sum: false, closure: "all"),
            List("sprint_list", "Sprintteki işler", "wi_active_sprint", null, take: 12)
        };
        var layout = Rows(
            Row(
                Col("sprint_count", 12, 4),
                Col("sprint_open_points", 12, 4),
                Col("sprint_closed_points", 12, 4)),
            Row(Col("sprint_by_state", 12, 12)),
            Row(Col("sprint_list", 12, 12)));
        return (layout, widgets);
    }

    private static Dictionary<string, object?> CountCard(string key, string title, string icon, string accent, string stateId) =>
        new()
        {
            ["key"] = key,
            ["type"] = "summaryCard",
            ["title"] = title,
            ["icon"] = icon,
            ["accentColor"] = accent,
            ["dataset"] = "op_work_items",
            ["queryKey"] = "wi_by_workspace_and_state",
            ["parameters"] = new Dictionary<string, object?>
            {
                ["workspaceId"] = "{{currentWorkspace}}",
                ["stateId"] = stateId
            }
        };

    private static Dictionary<string, object?> SprintCard(
        string key,
        string title,
        string icon,
        string accent,
        string closure,
        string measure)
    {
        var widget = new Dictionary<string, object?>
        {
            ["key"] = key,
            ["type"] = "summaryCard",
            ["title"] = title,
            ["icon"] = icon,
            ["accentColor"] = accent,
            ["dataset"] = "op_work_items",
            ["queryKey"] = "wi_active_sprint",
            ["parameters"] = new Dictionary<string, object?>
            {
                ["workspaceId"] = "{{currentWorkspace}}",
                ["closure"] = closure
            }
        };
        if (measure == "sum")
        {
            widget["measure"] = "sum";
            widget["measureField"] = "storyPoints";
            if (closure == "open")
                widget["aside"] = "unpointed";
        }

        return widget;
    }

    private static Dictionary<string, object?> Chart(
        string key,
        string title,
        string chartType,
        string groupBy,
        string queryKey,
        string? stateId,
        bool sum,
        string? closure = null)
    {
        var parameters = new Dictionary<string, object?> { ["workspaceId"] = "{{currentWorkspace}}" };
        if (!string.IsNullOrWhiteSpace(stateId))
            parameters["stateId"] = stateId;
        if (!string.IsNullOrWhiteSpace(closure))
            parameters["closure"] = closure;
        var widget = new Dictionary<string, object?>
        {
            ["key"] = key,
            ["type"] = "chart",
            ["title"] = title,
            ["dataset"] = "op_work_items",
            ["queryKey"] = queryKey,
            ["chartType"] = chartType,
            ["groupBy"] = groupBy,
            ["parameters"] = parameters
        };
        if (sum)
        {
            widget["measure"] = "sum";
            widget["measureField"] = "storyPoints";
        }

        return widget;
    }

    private static Dictionary<string, object?> List(string key, string title, string queryKey, string? stateId, int take = 8)
    {
        var parameters = new Dictionary<string, object?>();
        if (queryKey == "wi_assigned_open")
            parameters["assignee"] = "{{currentUser}}";
        else
            parameters["workspaceId"] = "{{currentWorkspace}}";
        if (!string.IsNullOrWhiteSpace(stateId))
            parameters["stateId"] = stateId;
        return new Dictionary<string, object?>
        {
            ["key"] = key,
            ["type"] = "list",
            ["title"] = title,
            ["dataset"] = "op_work_items",
            ["queryKey"] = queryKey,
            ["take"] = take,
            ["parameters"] = parameters
        };
    }

    private static Dictionary<string, object?> Col(string widgetId, int span, int spanMd, int? spanLg = null)
    {
        var col = new Dictionary<string, object?>
        {
            ["widgetId"] = widgetId,
            ["span"] = span,
            ["spanMd"] = spanMd
        };
        if (spanLg is int lg)
            col["spanLg"] = lg;
        return col;
    }

    private static Dictionary<string, object?> Row(params Dictionary<string, object?>[] cols) =>
        new() { ["cols"] = cols };

    private static Dictionary<string, object?> Rows(params Dictionary<string, object?>[] rows) =>
        new()
        {
            ["type"] = "rows",
            ["rows"] = rows
        };
}
