using System.Text.Json;

namespace MngOperations.Application.Utilities;

public sealed class DashboardWidgetDefinition
{
    public required string Key { get; init; }
    public required string WidgetType { get; init; }
    public string? Title { get; init; }
    public string Dataset { get; init; } = "op_work_items";
    public string? QueryKey { get; init; }
    public IReadOnlyDictionary<string, object?> Parameters { get; init; }
        = new Dictionary<string, object?>();
    public int Take { get; init; } = 50;
    public int Skip { get; init; }
    public bool ExecuteOnLoad { get; init; } = true;

    /// <summary>Chart widget'ları için: 'bar' | 'pie' | 'donut' | 'line'.</summary>
    public string? ChartType { get; init; }

    /// <summary>Chart agregasyon alanı: 'stateId' | 'priorityId' | 'typeId' | 'assignee'.</summary>
    public string? GroupBy { get; init; }

    /// <summary>summaryCard: Vuetify tema rengi (primary, success, …).</summary>
    public string? AccentColor { get; init; }

    /// <summary>summaryCard: mdi ikon adı (örn. mdi-counter).</summary>
    public string? Icon { get; init; }
}

public static class DashboardWidgetParser
{
    public static IReadOnlyList<DashboardWidgetDefinition> Parse(JsonElement? widgets)
    {
        if (widgets is not { ValueKind: JsonValueKind.Array })
            return Array.Empty<DashboardWidgetDefinition>();

        var list = new List<DashboardWidgetDefinition>();
        var index = 0;

        foreach (var item in widgets.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var parsed = ParseWidget(item, index++);
            if (parsed != null)
                list.Add(parsed);
        }

        return list;
    }

    private static DashboardWidgetDefinition? ParseWidget(JsonElement item, int index)
    {
        var key = ReadString(item, "key")
            ?? ReadString(item, "widgetKey")
            ?? ReadString(item, "id")
            ?? $"widget_{index}";

        var widgetType = ReadString(item, "type")
            ?? ReadString(item, "widgetType")
            ?? "list";

        var title = ReadString(item, "title") ?? ReadString(item, "name");

        // Chart meta: önce top-level, sonra "config" objesi.
        var chartType = ReadString(item, "chartType");
        var groupBy = ReadString(item, "groupBy");
        var accentColor = ReadString(item, "accentColor");
        var icon = ReadString(item, "icon");
        if ((chartType == null || groupBy == null || accentColor == null || icon == null)
            && item.TryGetProperty("config", out var config)
            && config.ValueKind == JsonValueKind.Object)
        {
            chartType ??= ReadString(config, "chartType");
            groupBy ??= ReadString(config, "groupBy");
            accentColor ??= ReadString(config, "accentColor");
            icon ??= ReadString(config, "icon");
        }

        string? dataset = null;
        string? queryKey = null;
        IReadOnlyDictionary<string, object?> parameters = new Dictionary<string, object?>();
        var take = ReadInt(item, "take") ?? 50;
        var skip = ReadInt(item, "skip") ?? 0;
        var executeOnLoad = ReadBool(item, "executeOnLoad", defaultValue: true);

        if (item.TryGetProperty("query", out var query) && query.ValueKind == JsonValueKind.Object)
        {
            dataset = ReadString(query, "dataset");
            queryKey = ReadString(query, "queryKey");
            parameters = ParseParameters(query);
            take = ReadInt(query, "take") ?? take;
            skip = ReadInt(query, "skip") ?? skip;
            executeOnLoad = ReadBool(query, "executeOnLoad", executeOnLoad);
        }
        else
        {
            dataset = ReadString(item, "dataset");
            queryKey = ReadString(item, "queryKey");
            parameters = ParseParameters(item);
        }

        return new DashboardWidgetDefinition
        {
            Key = key,
            WidgetType = widgetType,
            Title = title,
            Dataset = string.IsNullOrWhiteSpace(dataset) ? "op_work_items" : dataset.Trim(),
            QueryKey = queryKey,
            Parameters = parameters,
            Take = take,
            Skip = skip,
            ExecuteOnLoad = executeOnLoad,
            ChartType = chartType,
            GroupBy = groupBy,
            AccentColor = accentColor,
            Icon = icon
        };
    }

    private static IReadOnlyDictionary<string, object?> ParseParameters(JsonElement parent)
    {
        if (!parent.TryGetProperty("parameters", out var parameters)
            || parameters.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, object?>();
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in parameters.EnumerateObject())
            result[prop.Name] = DeserializeJsonValue(prop.Value);

        return result;
    }

    private static object? DeserializeJsonValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => JsonSerializer.Deserialize<object?>(element.GetRawText())
        };

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Number)
            return null;

        return prop.TryGetInt32(out var value) ? value : null;
    }

    private static bool ReadBool(JsonElement element, string name, bool defaultValue)
    {
        if (!element.TryGetProperty(name, out var prop))
            return defaultValue;

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }
}
