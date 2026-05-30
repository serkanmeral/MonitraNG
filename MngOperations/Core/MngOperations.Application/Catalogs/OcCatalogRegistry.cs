using MngOperations.Domain.Constants;

namespace MngOperations.Application.Catalogs;

/// <summary>
/// Bir katalog satırının silinebilmesi için referans kontrolü.
/// <c>{Field}:eq:{id}</c> filtresi: skaler alanda eşitlik, dizi alanında (ör. enabledStateIds) üyelik.
/// </summary>
public sealed class CatalogUsageCheck
{
    public required string Dataset { get; init; }
    public required string Field { get; init; }

    /// <summary>Kullanıcıya hangi bağlamda kullanıldığını anlatmak için kısa anahtar.</summary>
    public required string UsageKey { get; init; }
}

/// <summary>
/// Generic katalog endpoint'i için izin verilen kaynak tanımı.
/// </summary>
public sealed class CatalogDefinition
{
    public required string Source { get; init; }
    public required string Dataset { get; init; }
    public IReadOnlyList<CatalogUsageCheck> UsageChecks { get; init; } = Array.Empty<CatalogUsageCheck>();
}

/// <summary>
/// MO üzerinden CRUD + cache yönetilen global kataloglar (states/priorities/types/fields).
/// </summary>
public static class OcCatalogRegistry
{
    private static readonly Dictionary<string, CatalogDefinition> BySource =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["states"] = new CatalogDefinition
            {
                Source = "states",
                Dataset = OcDatasets.States,
                UsageChecks = new[]
                {
                    new CatalogUsageCheck { Dataset = OcDatasets.WorkItems, Field = "stateId", UsageKey = "workItems" },
                    new CatalogUsageCheck { Dataset = OcDatasets.Workspaces, Field = "enabledStateIds", UsageKey = "workspaces" },
                },
            },
            ["priorities"] = new CatalogDefinition
            {
                Source = "priorities",
                Dataset = OcDatasets.Priorities,
                UsageChecks = new[]
                {
                    new CatalogUsageCheck { Dataset = OcDatasets.WorkItems, Field = "priorityId", UsageKey = "workItems" },
                    new CatalogUsageCheck { Dataset = OcDatasets.Workspaces, Field = "enabledPriorityIds", UsageKey = "workspaces" },
                },
            },
            ["types"] = new CatalogDefinition
            {
                Source = "types",
                Dataset = OcDatasets.WorkItemTypes,
                UsageChecks = new[]
                {
                    new CatalogUsageCheck { Dataset = OcDatasets.WorkItems, Field = "typeId", UsageKey = "workItems" },
                    new CatalogUsageCheck { Dataset = OcDatasets.Workspaces, Field = "enabledTypeIds", UsageKey = "workspaces" },
                },
            },
            ["fields"] = new CatalogDefinition
            {
                Source = "fields",
                Dataset = OcDatasets.Fields,
                // Pool alan değerleri work item extraFields.{key} altında tutulur; Faz 1'de yalnızca
                // workspace etkinleştirmesi (enabledFieldIds) üzerinden guard uygulanır.
                UsageChecks = new[]
                {
                    new CatalogUsageCheck { Dataset = OcDatasets.Workspaces, Field = "enabledFieldIds", UsageKey = "workspaces" },
                },
            },
        };

    public static IReadOnlyCollection<string> Sources => BySource.Keys;

    public static bool TryResolve(string? source, out CatalogDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(source) && BySource.TryGetValue(source.Trim(), out var def))
        {
            definition = def;
            return true;
        }

        definition = null!;
        return false;
    }
}
