using System.Text.Json;

namespace MngOperations.Application.Contracts.Runtime;

public sealed class BoardRuntimeContext
{
    public required string BoardId { get; init; }
    public required string WorkspaceId { get; init; }
    public string? Name { get; init; }
    public string? ViewType { get; init; }
    public RuntimePermissionsDto Permissions { get; init; } = new();
    public IReadOnlyList<BoardColumnDto> Columns { get; init; } = Array.Empty<BoardColumnDto>();
    public IReadOnlyList<string> CardFieldKeys { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Liste tablosu sütun tanımları (sıra + per-column sortable/filterable). `CardFieldKeys` ile aynı
    /// sırayı paylaşır; liste görünümünde sunucu tarafı sort/filter yetkilerini belirler.
    /// </summary>
    public IReadOnlyList<BoardListColumnDto> ListColumns { get; init; } = Array.Empty<BoardListColumnDto>();

    /// <summary>Liste görünümü varsayılan sıralaması (kullanıcı sıralaması yoksa uygulanır).</summary>
    public BoardSortDto? DefaultSort { get; init; }

    /// <summary>Akıştaki başlangıç state id'si — liste SLA chip'inde "akıllı faz" (response/resolve) proxy'si için.</summary>
    public string? InitialStateId { get; init; }

    /// <summary>
    /// Katalog lookup map'leri (states/priorities/types) — UI client-side join yapmadan id'leri
    /// isim/renk/ikon ile gösterir. MO cache'inden beslenir (Durum 2).
    /// </summary>
    public BoardCatalogsDto Catalogs { get; init; } = new();
}

/// <summary>Liste tablosu sütun tanımı (board.config.listColumns).</summary>
public sealed class BoardListColumnDto
{
    public required string Key { get; init; }
    public bool Sortable { get; init; }
    public bool Filterable { get; init; }

    /// <summary>Hücre format ipucu: text | number | money | date | relativeTime (null = varsayılan).</summary>
    public string? Format { get; init; }

    /// <summary>Hesaplanan (computed) sütun mu? true ise DG alanı yoktur, değer UI'da `Expr` ile hesaplanır.</summary>
    public bool Computed { get; init; }

    /// <summary>Computed sütun ifadesi (expr-eval). Yalnızca `Computed=true` için anlamlıdır.</summary>
    public string? Expr { get; init; }

    /// <summary>Computed sütun başlığı (UI etiketi; null = key).</summary>
    public string? Label { get; init; }
}

/// <summary>Sıralama tanımı (alan + yön).</summary>
public sealed class BoardSortDto
{
    public required string Field { get; init; }
    /// <summary>"asc" | "desc".</summary>
    public string Direction { get; init; } = "asc";
}

/// <summary>Liste filtresi (alan + operatör + değer). Operatör DG REST DSL'i ile aynı sözlük.</summary>
public sealed class BoardListFilterDto
{
    public required string Field { get; init; }
    /// <summary>eq, ne, gt, gte, lt, lte, in, nin, contains, startsWith, endsWith.</summary>
    public string Operator { get; init; } = "eq";
    public string? Value { get; init; }
}

/// <summary>Board liste görünümü için sunucu tarafı sayfalama + sıralama + filtre + arama isteği.</summary>
public sealed class BoardListRequest
{
    public int Skip { get; init; }
    public int Take { get; init; } = 50;
    public BoardSortDto? Sort { get; init; }
    public IReadOnlyList<BoardListFilterDto>? Filters { get; init; }
    public string? Search { get; init; }
}

public sealed class BoardCatalogsDto
{
    public IReadOnlyDictionary<string, CatalogDisplayDto> States { get; init; }
        = new Dictionary<string, CatalogDisplayDto>();
    public IReadOnlyDictionary<string, CatalogDisplayDto> Priorities { get; init; }
        = new Dictionary<string, CatalogDisplayDto>();
    public IReadOnlyDictionary<string, CatalogDisplayDto> Types { get; init; }
        = new Dictionary<string, CatalogDisplayDto>();
}

public sealed class CatalogDisplayDto
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string? Color { get; init; }
    public string? Icon { get; init; }
}

public sealed class BoardColumnDto
{
    public required string StateId { get; init; }
    public string? Title { get; init; }
    public bool DropEligible { get; init; } = true;
    public string? DefaultTransitionKey { get; init; }
    public IReadOnlyList<string> AlternativeTransitionKeys { get; init; } = Array.Empty<string>();
    public required string QueryKey { get; init; }
    public IReadOnlyDictionary<string, string> ParametersTemplate { get; init; }
        = new Dictionary<string, string>();
    public int SuggestedPageSize { get; init; } = 50;
}

public sealed class ExecuteQueryRequest
{
    public string Dataset { get; init; } = "op_work_items";
    public Dictionary<string, JsonElement>? Parameters { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 50;
}

public sealed class QueryExecuteResponse
{
    public required string Dataset { get; init; }
    public required string QueryKey { get; init; }
    public IReadOnlyList<WorkItemCardDto> Items { get; init; } = Array.Empty<WorkItemCardDto>();
    public int Skip { get; init; }
    public int Take { get; init; }
    public int Total { get; init; }

    /// <summary>
    /// Kart person alanlarındaki (assignee/watchers + person tipi pool alanlar) id → görünen ad map'i.
    /// Kataloglar gibi MO cache'inden (Keeper) çözülür; UI client-side lookup yapmaz.
    /// </summary>
    public IReadOnlyDictionary<string, PersonDisplayDto> People { get; init; }
        = new Dictionary<string, PersonDisplayDto>();
}

public sealed class PersonDisplayDto
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string? Title { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class WorkItemCardDto
{
    public required string Id { get; init; }
    public required string Key { get; init; }
    public required string Title { get; init; }
    public string? StateId { get; init; }
    public string? Assignee { get; init; }
    public string? PriorityId { get; init; }
    public string? TypeId { get; init; }

    /// <summary>Oluşturulma zamanı (sistem alanı).</summary>
    public DateTime? CreatedAt { get; init; }

    /// <summary>Oluşturan kullanıcı id'si (forward-only; eski kayıtlarda boş olabilir). Ad çözümü <see cref="QueryExecuteResponse.People"/>.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Son güncelleme zamanı (varsa).</summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>Son state değişimi zamanı.</summary>
    public DateTime? LastStateChangeAt { get; init; }

    /// <summary>Kapanış zamanı (kapalı item'larda "geçen süre" anchor'ı).</summary>
    public DateTime? ClosedAt { get; init; }

    /// <summary>SLA snapshot (op_work_items.sla) — liste SLA durumu chip'i için.</summary>
    public SlaSnapshotDto? Sla { get; init; }

    /// <summary>Pool alan değerleri (extraFields) — liste tablosunda özel sütunlar için.</summary>
    public JsonElement? Fields { get; init; }
}
