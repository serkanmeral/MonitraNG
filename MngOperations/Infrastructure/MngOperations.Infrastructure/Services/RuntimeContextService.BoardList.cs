using System.Text.Json;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Utilities;

namespace MngOperations.Infrastructure.Services;

public partial class RuntimeContextService
{
    /// <summary>listColumns[].label boşsa op_fields.label ile doldurur (UI sütun başlıkları).</summary>
    private static IReadOnlyList<BoardListColumnDto> EnrichListColumnLabels(
        IReadOnlyList<BoardListColumnDto> columns,
        IReadOnlyList<Dictionary<string, object?>> poolFields)
    {
        if (columns.Count == 0 || poolFields.Count == 0)
            return columns;

        var labelByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pf in poolFields)
        {
            var key = WorkItemDataHelper.GetString(pf, "key");
            var label = WorkItemDataHelper.GetString(pf, "label");
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(label))
                labelByKey[key] = label.Trim();
        }

        if (labelByKey.Count == 0)
            return columns;

        var result = new List<BoardListColumnDto>(columns.Count);
        foreach (var col in columns)
        {
            if (!string.IsNullOrWhiteSpace(col.Label))
            {
                result.Add(col);
                continue;
            }

            if (labelByKey.TryGetValue(col.Key, out var lbl))
            {
                result.Add(new BoardListColumnDto
                {
                    Key = col.Key,
                    Sortable = col.Sortable,
                    Filterable = col.Filterable,
                    Format = col.Format,
                    Computed = col.Computed,
                    Expr = col.Expr,
                    Label = lbl
                });
            }
            else
            {
                result.Add(col);
            }
        }

        return result;
    }

    /// <summary>Board liste satırları — görünen relation pool sütunları için id→etiket çözümü (toplu).</summary>
    private async Task<IReadOnlyList<WorkItemCardDto>> EnrichBoardListCardsAsync(
        IReadOnlyList<WorkItemCardDto> cards,
        IReadOnlyList<BoardListColumnDto> listColumns,
        IReadOnlyList<Dictionary<string, object?>> poolFields,
        string token,
        CancellationToken cancellationToken)
    {
        if (cards.Count == 0 || listColumns.Count == 0 || poolFields.Count == 0)
            return cards;

        var poolByKey = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pf in poolFields)
        {
            var k = WorkItemDataHelper.GetString(pf, "key");
            if (!string.IsNullOrWhiteSpace(k) && !poolByKey.ContainsKey(k))
                poolByKey[k] = pf;
        }

        var columnMeta = new List<(string Key, string Dataset, string LabelField)>();
        foreach (var col in listColumns)
        {
            if (col.Computed || string.IsNullOrWhiteSpace(col.Key))
                continue;
            if (!poolByKey.TryGetValue(col.Key, out var pf))
                continue;

            var ft = (WorkItemDataHelper.GetString(pf, "fieldType") ?? string.Empty).Trim().ToLowerInvariant();
            var dataset = WorkItemDataHelper.GetString(pf, "relationDatasetName");
            if (ft != "relation" || string.IsNullOrWhiteSpace(dataset))
                continue;

            var labelField = LookupFieldOptionsHelper.ResolveLabelField(pf);
            columnMeta.Add((col.Key, dataset, labelField));
        }

        if (columnMeta.Count == 0)
            return cards;

        var datasetIds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (key, dataset, labelField) in columnMeta)
        {
            var resolveKey = LookupFieldOptionsHelper.ComposeResolveKey(dataset, labelField);
            if (!datasetIds.TryGetValue(resolveKey, out var set))
                datasetIds[resolveKey] = set = new HashSet<string>(StringComparer.Ordinal);

            foreach (var card in cards)
            {
                if (card.Fields is not { ValueKind: JsonValueKind.Object } fields
                    || !fields.TryGetProperty(key, out var val))
                {
                    continue;
                }

                var ids = new List<string>();
                CollectRefIdsFromValue(val, ids);
                foreach (var id in ids)
                    set.Add(id);
            }
        }

        var nameMapTasks = datasetIds.ToDictionary(
            kv => kv.Key,
            kv =>
            {
                var (dataset, labelField) = LookupFieldOptionsHelper.ParseResolveKey(kv.Key);
                return ResolveRelationNamesAsync(dataset, kv.Value, labelField, token, cancellationToken);
            });
        await Task.WhenAll(nameMapTasks.Values);

        var enriched = new List<WorkItemCardDto>(cards.Count);
        foreach (var card in cards)
        {
            var displays = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (card.Fields is { ValueKind: JsonValueKind.Object } fields)
            {
                foreach (var (key, dataset, labelField) in columnMeta)
                {
                    if (!fields.TryGetProperty(key, out var val))
                        continue;

                    var ids = new List<string>();
                    CollectRefIdsFromValue(val, ids);
                    if (ids.Count == 0)
                        continue;

                    var resolveKey = LookupFieldOptionsHelper.ComposeResolveKey(dataset, labelField);
                    var map = nameMapTasks.TryGetValue(resolveKey, out var task) ? task.Result : null;
                    var names = ids
                        .Select(id =>
                            map != null && map.TryGetValue(id, out var nm) && !string.IsNullOrWhiteSpace(nm)
                                ? nm
                                : id)
                        .ToList();
                    if (names.Count > 0)
                        displays[key] = string.Join(", ", names);
                }
            }

            enriched.Add(new WorkItemCardDto
            {
                Id = card.Id,
                Key = card.Key,
                Title = card.Title,
                StateId = card.StateId,
                Assignee = card.Assignee,
                PriorityId = card.PriorityId,
                TypeId = card.TypeId,
                CreatedAt = card.CreatedAt,
                CreatedBy = card.CreatedBy,
                UpdatedAt = card.UpdatedAt,
                LastStateChangeAt = card.LastStateChangeAt,
                ClosedAt = card.ClosedAt,
                Sla = card.Sla,
                Fields = card.Fields,
                FieldDisplays = displays.Count > 0 ? displays : null
            });
        }

        return enriched;
    }
}
