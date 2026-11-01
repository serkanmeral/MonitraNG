namespace MngOperations.Infrastructure.Services;

/// <summary>
/// Help Desk özet panosu. Durum kimlikleri kurulumda yerine yazılır.
/// </summary>
public static class HelpDeskPackDashboards
{
    public static (Dictionary<string, object?> Layout, object Widgets) Build(
        IReadOnlyDictionary<string, string> stateIds)
    {
        var widgets = new object[]
        {
            CountCard("state_new", "Yeni", "mdi-inbox-outline", "secondary", stateIds["state.new"]),
            CountCard("state_progress", "İşlemde", "mdi-progress-clock", "primary", stateIds["state.progress"]),
            CountCard("state_waiting", "Beklemede", "mdi-clock-outline", "warning", stateIds["state.waiting"]),
            new Dictionary<string, object?>
            {
                ["key"] = "mine",
                ["type"] = "summaryCard",
                ["title"] = "Bana atanan",
                ["icon"] = "mdi-account-check-outline",
                ["accentColor"] = "info",
                ["dataset"] = "op_work_items",
                ["queryKey"] = "wi_assigned_open",
                ["parameters"] = new Dictionary<string, object?> { ["assignee"] = "{{currentUser}}" }
            },
            Chart("open_by_priority", "Açık — öncelik", "priorityId"),
            Chart("open_by_type", "Açık — tip", "typeId"),
            List("new_list", "Yeni kuyruk", "wi_by_workspace_and_state", stateIds["state.new"]),
            List("mine_list", "Bana atanan açık", "wi_assigned_open", null)
        };

        var layout = Rows(
            Row(
                Col("state_new", 6, 3),
                Col("state_progress", 6, 3),
                Col("state_waiting", 6, 3),
                Col("mine", 6, 3)),
            Row(Col("open_by_priority", 12, 6), Col("open_by_type", 12, 6)),
            Row(Col("new_list", 12, 6), Col("mine_list", 12, 6)));
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

    private static Dictionary<string, object?> Chart(string key, string title, string groupBy) =>
        new()
        {
            ["key"] = key,
            ["type"] = "chart",
            ["title"] = title,
            ["dataset"] = "op_work_items",
            ["queryKey"] = "wi_open_workspace",
            ["chartType"] = "bar",
            ["groupBy"] = groupBy,
            ["parameters"] = new Dictionary<string, object?> { ["workspaceId"] = "{{currentWorkspace}}" }
        };

    private static Dictionary<string, object?> List(string key, string title, string queryKey, string? stateId)
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
            ["take"] = 8,
            ["parameters"] = parameters
        };
    }

    private static Dictionary<string, object?> Col(string widgetId, int span, int spanMd) =>
        new()
        {
            ["widgetId"] = widgetId,
            ["span"] = span,
            ["spanMd"] = spanMd
        };

    private static Dictionary<string, object?> Row(params Dictionary<string, object?>[] cols) =>
        new() { ["cols"] = cols };

    private static Dictionary<string, object?> Rows(params Dictionary<string, object?>[] rows) =>
        new()
        {
            ["type"] = "rows",
            ["rows"] = rows
        };
}
