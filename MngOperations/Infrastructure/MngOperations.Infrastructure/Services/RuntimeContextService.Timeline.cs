using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

/// <summary>
/// Aktivite alan değişiklik satırlarının (op_activities.changes[]) read-time çözümü.
/// Yazımda ham id/scalar saklanır; burada katalog/dizin/relation çözülüp görünen metne çevrilir
/// (UI ham veri işlemez; profile-view felsefesiyle aynı resolver yardımcıları kullanılır).
/// </summary>
public partial class RuntimeContextService
{
    private static readonly IReadOnlyDictionary<string, List<TimelineChangeDto>> EmptyTimelineChanges =
        new Dictionary<string, List<TimelineChangeDto>>(StringComparer.Ordinal);

    // typeId çekirdek relation listesinde yok (CoreRelationDatasets) → ad çözümü için dataset eşlemesi.
    private const string TypeIdDataset = OcDatasets.WorkItemTypes;

    // Katalog dataset'leri (state/priority/type/board) DG query ucundan ($in) gelmeyebilir →
    // profil-view ile aynı şekilde cache'li GetCatalogListAsync üzerinden çözülür.
    private static readonly HashSet<string> ChangeCatalogDatasets = new(StringComparer.Ordinal)
    {
        OcDatasets.States,
        OcDatasets.Priorities,
        OcDatasets.WorkItemTypes,
        OcDatasets.Boards,
        OcDatasets.Tags,
    };

    private enum ChangeFieldKind { Scalar, Person, Group, Dataset }

    private sealed record ChangeFieldMeta(string Key, string Label, string? FieldType, ChangeFieldKind Kind, string? Dataset);

    private readonly struct ParsedChange
    {
        public ParsedChange(string field, IReadOnlyList<string> from, IReadOnlyList<string> to)
        {
            Field = field;
            From = from;
            To = to;
        }

        public string Field { get; }
        public IReadOnlyList<string> From { get; }
        public IReadOnlyList<string> To { get; }
    }

    /// <summary>
    /// Aktivite kayıtlarındaki changes[]'i parse edip alan etiketi + eski/yeni görünen metinlere çözer.
    /// changes içeren aktivite yoksa metadata (form/pool/dizin) hiç yüklenmez (yorum yenilemede ek maliyet yok).
    /// </summary>
    private async Task<IReadOnlyDictionary<string, List<TimelineChangeDto>>> ResolveActivityChangesAsync(
        string workItemId,
        Dictionary<string, object?> workItem,
        string workspaceId,
        IReadOnlyList<Dictionary<string, object?>> activityList,
        string token,
        CancellationToken cancellationToken,
        Task<FormRuntimeContext>? sharedFormTask = null,
        Task<IReadOnlyList<Dictionary<string, object?>>>? sharedPoolFieldsTask = null)
    {
        // 1) Ham changes parse: activityId → satırlar.
        var parsed = new Dictionary<string, List<ParsedChange>>(StringComparer.Ordinal);
        foreach (var activity in activityList)
        {
            var id = WorkItemDataHelper.GetDataId(activity);
            if (string.IsNullOrWhiteSpace(id) || !activity.TryGetValue("changes", out var raw))
                continue;
            var rows = ParseChangeRows(raw);
            if (rows.Count > 0)
                parsed[id] = rows;
        }

        if (parsed.Count == 0)
            return EmptyTimelineChanges;

        // 2) Form (etiket/tür) + pool alanlar (relationDatasetName) — yalnız changes varsa.
        IReadOnlyDictionary<string, FormFieldRuntimeDto> formFields =
            new Dictionary<string, FormFieldRuntimeDto>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<Dictionary<string, object?>> poolFields = Array.Empty<Dictionary<string, object?>>();
        try
        {
            if (sharedFormTask != null && sharedPoolFieldsTask != null)
            {
                await Task.WhenAll(sharedFormTask, sharedPoolFieldsTask);
                formFields = sharedFormTask.Result.Fields ?? formFields;
                poolFields = sharedPoolFieldsTask.Result;
            }
            else
            {
                var formTask = GetFormEditAsync(workItemId, workItem, cancellationToken);
                var poolTask = LoadProfilePoolFieldsAsync(workspaceId, token, cancellationToken);
                await Task.WhenAll(formTask, poolTask);
                formFields = formTask.Result.Fields ?? formFields;
                poolFields = poolTask.Result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Timeline changes metadata load failed; ham id/scalar gösterilecek.");
        }

        var poolByKey = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pf in poolFields)
        {
            var k = WorkItemDataHelper.GetString(pf, "key");
            if (!string.IsNullOrWhiteSpace(k) && !poolByKey.ContainsKey(k))
                poolByKey[k] = pf;
        }

        // 3) Alan başına kind/dataset/etiket; çözüm için id'leri kovalara topla.
        var fieldMeta = new Dictionary<string, ChangeFieldMeta>(StringComparer.OrdinalIgnoreCase);
        var personIds = new HashSet<string>(StringComparer.Ordinal);
        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        var datasetIds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var rows in parsed.Values)
        {
            foreach (var row in rows)
            {
                if (fieldMeta.ContainsKey(row.Field))
                {
                    AccumulateIds(fieldMeta[row.Field], row, personIds, groupIds, datasetIds);
                    continue;
                }

                var meta = ResolveFieldMeta(row.Field, formFields, poolByKey);
                fieldMeta[row.Field] = meta;
                AccumulateIds(meta, row, personIds, groupIds, datasetIds);
            }
        }

        // 4) Çözümler (paralel).
        var people = personIds.Count > 0
            ? await _personDirectory.GetPeopleAsync(personIds, token, cancellationToken)
            : new Dictionary<string, PersonDisplayDto>();
        var groups = groupIds.Count > 0
            ? await _groupDirectory.GetGroupsAsync(groupIds, token, cancellationToken)
            : new Dictionary<string, PersonDisplayDto>();

        var nameMapTasks = datasetIds.ToDictionary(
            kv => kv.Key,
            kv => ResolveChangeDatasetNamesAsync(kv.Key, kv.Value, token, cancellationToken));
        if (nameMapTasks.Count > 0)
            await Task.WhenAll(nameMapTasks.Values);
        var datasetMaps = nameMapTasks.ToDictionary(kv => kv.Key, kv => kv.Value.Result, StringComparer.Ordinal);

        // 5) Görünen metinleri kur.
        var result = new Dictionary<string, List<TimelineChangeDto>>(StringComparer.Ordinal);
        foreach (var (activityId, rows) in parsed)
        {
            var list = new List<TimelineChangeDto>(rows.Count);
            foreach (var row in rows)
            {
                var meta = fieldMeta[row.Field];
                list.Add(new TimelineChangeDto
                {
                    Field = row.Field,
                    Label = meta.Label,
                    FieldType = meta.FieldType,
                    FromDisplay = BuildDisplay(meta, row.From, people, groups, datasetMaps),
                    ToDisplay = BuildDisplay(meta, row.To, people, groups, datasetMaps),
                });
            }
            result[activityId] = list;
        }

        return result;
    }

    private static ChangeFieldMeta ResolveFieldMeta(
        string key,
        IReadOnlyDictionary<string, FormFieldRuntimeDto> formFields,
        IReadOnlyDictionary<string, Dictionary<string, object?>> poolByKey)
    {
        formFields.TryGetValue(key, out var ff);
        var label = !string.IsNullOrWhiteSpace(ff?.Label) ? ff!.Label! : key;
        var ft = ff?.FieldType?.Trim().ToLowerInvariant();

        // person alanları (assignee/watchers/reporter/createdBy + person pool).
        if (ft is "persons" or "person" || CorePersonFieldKeys.Contains(key) || key is "reporter" or "createdBy")
            return new ChangeFieldMeta(key, label, ft, ChangeFieldKind.Person, null);

        // grup alanları (assignmentGroups + personGroups pool).
        if (ft is "persongroups" or "persongroup" or "group" || CoreGroupFieldKeys.Contains(key))
            return new ChangeFieldMeta(key, label, ft, ChangeFieldKind.Group, null);

        // dataset bazlı çözüm (çekirdek katalog/relation + pool relation).
        string? dataset = null;
        if (poolByKey.TryGetValue(key, out var pf))
            dataset = WorkItemDataHelper.GetString(pf, "relationDatasetName");
        if (string.IsNullOrWhiteSpace(dataset))
            CoreRelationDatasets.TryGetValue(key, out dataset);
        if (string.IsNullOrWhiteSpace(dataset) && string.Equals(key, "typeId", StringComparison.OrdinalIgnoreCase))
            dataset = TypeIdDataset;
        // 'tags' pool alanı → workspace etiket kataloğu (op_tags); id→ad $in query ile çözülür.
        if (string.IsNullOrWhiteSpace(dataset) && ft == "tags")
            dataset = OcDatasets.Tags;

        if (!string.IsNullOrWhiteSpace(dataset))
            return new ChangeFieldMeta(key, label, ft, ChangeFieldKind.Dataset, dataset);

        // scalar (text/number/date/bool) → ham gösterilir.
        return new ChangeFieldMeta(key, label, ft, ChangeFieldKind.Scalar, null);
    }

    private static void AccumulateIds(
        ChangeFieldMeta meta,
        ParsedChange row,
        HashSet<string> personIds,
        HashSet<string> groupIds,
        Dictionary<string, HashSet<string>> datasetIds)
    {
        switch (meta.Kind)
        {
            case ChangeFieldKind.Person:
                foreach (var id in row.From) personIds.Add(id);
                foreach (var id in row.To) personIds.Add(id);
                break;
            case ChangeFieldKind.Group:
                foreach (var id in row.From) groupIds.Add(id);
                foreach (var id in row.To) groupIds.Add(id);
                break;
            case ChangeFieldKind.Dataset:
                if (!datasetIds.TryGetValue(meta.Dataset!, out var set))
                    datasetIds[meta.Dataset!] = set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in row.From) set.Add(id);
                foreach (var id in row.To) set.Add(id);
                break;
        }
    }

    private static string? BuildDisplay(
        ChangeFieldMeta meta,
        IReadOnlyList<string> tokens,
        IReadOnlyDictionary<string, PersonDisplayDto> people,
        IReadOnlyDictionary<string, PersonDisplayDto> groups,
        IReadOnlyDictionary<string, Dictionary<string, string>> datasetMaps)
    {
        if (tokens.Count == 0)
            return null;

        var names = new List<string>(tokens.Count);
        foreach (var token in tokens)
        {
            names.Add(meta.Kind switch
            {
                ChangeFieldKind.Person => people.TryGetValue(token, out var p) && !string.IsNullOrWhiteSpace(p.Name) ? p.Name! : token,
                ChangeFieldKind.Group => groups.TryGetValue(token, out var g) && !string.IsNullOrWhiteSpace(g.Name) ? g.Name! : token,
                ChangeFieldKind.Dataset => datasetMaps.TryGetValue(meta.Dataset!, out var map) && map.TryGetValue(token, out var nm) && !string.IsNullOrWhiteSpace(nm) ? nm : token,
                _ => token,
            });
        }

        return string.Join(", ", names);
    }

    /// <summary>Katalog dataset'leri (state/priority/type/board) cache'li listeden, diğerleri $in query ile çözülür.</summary>
    private Task<Dictionary<string, string>> ResolveChangeDatasetNamesAsync(
        string dataset,
        IReadOnlyCollection<string> ids,
        string token,
        CancellationToken cancellationToken)
    {
        return ChangeCatalogDatasets.Contains(dataset)
            ? ResolveCatalogNamesAsync(dataset, ids, token, cancellationToken)
            : ResolveRelationNamesAsync(dataset, ids, token, cancellationToken);
    }

    /// <summary>Katalog dataset'inden (op_states/priorities/work_item_types/boards) id → ad (cache'li, profil-view ile aynı kanal).</summary>
    private async Task<Dictionary<string, string>> ResolveCatalogNamesAsync(
        string dataset,
        IReadOnlyCollection<string> ids,
        string token,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ids.Count == 0)
            return map;

        try
        {
            var wanted = new HashSet<string>(ids, StringComparer.Ordinal);
            var list = await _metadataCache.GetCatalogListAsync(dataset, token, cancellationToken);
            foreach (var row in list)
            {
                var id = WorkItemDataHelper.GetString(row, "__dataId");
                if (string.IsNullOrWhiteSpace(id) || !wanted.Contains(id))
                    continue;
                var name = FirstNonEmpty(
                    WorkItemDataHelper.GetString(row, "name"),
                    WorkItemDataHelper.GetString(row, "label"),
                    WorkItemDataHelper.GetString(row, "title"),
                    WorkItemDataHelper.GetString(row, "key"));
                if (!string.IsNullOrWhiteSpace(name))
                    map[id!] = name!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Catalog name resolve failed for dataset {Dataset}.", dataset);
        }

        return map;
    }

    /// <summary>op_activities.changes ham değerini ParsedChange listesine çevirir (DG JsonElement dizisi).</summary>
    private static List<ParsedChange> ParseChangeRows(object? raw)
    {
        var rows = new List<ParsedChange>();
        if (raw is not JsonElement { ValueKind: JsonValueKind.Array } arr)
            return rows;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var field = item.TryGetProperty("field", out var fEl) && fEl.ValueKind == JsonValueKind.String
                ? fEl.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(field))
                continue;

            var from = item.TryGetProperty("from", out var fromEl) ? ExtractTokens(fromEl) : new List<string>();
            var to = item.TryGetProperty("to", out var toEl) ? ExtractTokens(toEl) : new List<string>();
            rows.Add(new ParsedChange(field!, from, to));
        }

        return rows;
    }

    private static List<string> ExtractTokens(JsonElement el)
    {
        var tokens = new List<string>();
        AppendTokens(el, tokens);
        return tokens;
    }

    private static readonly string[] ChangeTokenRefProps = { "__dataId", "_id", "id" };

    private static void AppendTokens(JsonElement el, List<string> tokens)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                AddToken(tokens, el.GetString());
                break;
            case JsonValueKind.Number:
                AddToken(tokens, el.ToString());
                break;
            case JsonValueKind.True:
                AddToken(tokens, "true");
                break;
            case JsonValueKind.False:
                AddToken(tokens, "false");
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    AppendTokens(item, tokens);
                break;
            case JsonValueKind.Object:
                foreach (var n in ChangeTokenRefProps)
                {
                    if (el.TryGetProperty(n, out var idEl)
                        && idEl.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(idEl.GetString()))
                    {
                        AddToken(tokens, idEl.GetString());
                        return;
                    }
                }
                break;
        }
    }

    private static void AddToken(List<string> tokens, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;
        tokens.Add(token.Trim());
    }
}
