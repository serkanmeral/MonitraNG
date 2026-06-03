using System.Text.Json;

namespace MngOperations.Application.Contracts.Runtime;

public sealed class DashboardRuntimeContext
{
    public required string DashboardId { get; init; }
    public string? WorkspaceId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Scope { get; init; }
    public JsonElement? Layout { get; init; }
    public RuntimePermissionsDto Permissions { get; init; } = new();
    public IReadOnlyList<DashboardWidgetRuntimeDto> Widgets { get; init; } = Array.Empty<DashboardWidgetRuntimeDto>();

    /// <summary>Workspace state/priority/type kataloğu (id → ad/renk/ikon). List/summary widget'ları ham id yerine ad gösterir.</summary>
    public BoardCatalogsDto Catalogs { get; init; } = new();

    /// <summary>Tüm widget item'larındaki person alanları (assignee + person pool) id → görünen ad. Board context ile aynı desen.</summary>
    public IReadOnlyDictionary<string, PersonDisplayDto> People { get; init; }
        = new Dictionary<string, PersonDisplayDto>();

    /// <summary>Tüm widget item'larındaki person <b>grup</b> alanları id → grup adı.</summary>
    public IReadOnlyDictionary<string, PersonDisplayDto> Groups { get; init; }
        = new Dictionary<string, PersonDisplayDto>();
}

public sealed class DashboardWidgetRuntimeDto
{
    public required string Key { get; init; }
    public required string WidgetType { get; init; }
    public string? Title { get; init; }
    public string? Dataset { get; init; }
    public string? QueryKey { get; init; }

    /// <summary>Chart widget'ları için: 'bar' | 'pie' | 'donut' | 'line'. Diğer tiplerde null.</summary>
    public string? ChartType { get; init; }

    /// <summary>Chart agregasyon alanı: 'stateId' | 'priorityId' | 'typeId' | 'assignee'. Diğer tiplerde null.</summary>
    public string? GroupBy { get; init; }

    /// <summary>summaryCard görünüm — DG widget config.</summary>
    public string? AccentColor { get; init; }

    public string? Icon { get; init; }

    public IReadOnlyDictionary<string, object?>? ResolvedParameters { get; init; }
    public DashboardWidgetExecutionDto? Execution { get; init; }
}

public sealed class DashboardWidgetExecutionDto
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int Total { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; }
    public IReadOnlyList<WorkItemCardDto> Items { get; init; } = Array.Empty<WorkItemCardDto>();

    /// <summary>Chart widget'ları için server-side agregasyon (tam sonuç kümesi groupBy'a göre gruplanır). Diğer tiplerde boş.</summary>
    public IReadOnlyList<DashboardAggregationBucketDto> Aggregation { get; init; }
        = Array.Empty<DashboardAggregationBucketDto>();

    public DateTime ExecutedAt { get; init; }
}

/// <summary>Chart agregasyon kovası — Key = ham id/değer (catalog/person ile UI'da çözülür), Count = kayıt sayısı.</summary>
public sealed class DashboardAggregationBucketDto
{
    public string? Key { get; init; }
    public int Count { get; init; }
}
