using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

/// <summary>
/// Profil ekranı için tek toplu "profile-view" uç implementasyonu. Mevcut GetProfile/GetFormEdit/
/// GetTimeline + katalog/politika/pool-alan çözümlerini paralel çağırıp tek pakette döner;
/// alan görünen değerleri (relation/person/grup/katalog) MO'da çözülür → readonly form lookup yapmaz.
/// </summary>
public partial class RuntimeContextService
{
    // Çekirdek relation alan key → dataset (UI OC_CORE_RELATION_DATASET ile birebir).
    private static readonly IReadOnlyDictionary<string, string> CoreRelationDatasets =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["priorityId"] = OcDatasets.Priorities,
            ["boardId"] = OcDatasets.Boards,
            ["stateId"] = OcDatasets.States,
            ["stateFlowId"] = OcDatasets.StateFlows,
            ["labels"] = OcDatasets.Tags, // çekirdek "Etiketler" alanı workspace etiket kataloğunu (op_tags) kullanır
            ["parentItemId"] = OcDatasets.WorkItems,
        };

    public async Task<ProfileViewContext> GetProfileViewAsync(
        string workItemId,
        bool includeTimeline = false,
        CancellationToken cancellationToken = default)
    {
        var perfSw = _perfDiagnostics ? Stopwatch.StartNew() : null;
        var token = RequireToken();

        var workItem = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        var workspaceId = WorkItemDataHelper.GetString(workItem, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing.", "workspaceId yok.", 500);
        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureWorkItemView(workspace, workItem);

        var currentStateId = WorkItemDataHelper.GetString(workItem, "stateId") ?? string.Empty;
        var scopeStateIds = string.IsNullOrEmpty(currentStateId)
            ? Array.Empty<string>()
            : new[] { currentStateId };

        var profileTask = GetProfileAsync(workItemId, workItem, cancellationToken);
        var formTask = GetFormEditAsync(workItemId, workItem, cancellationToken);
        var poolFieldsTask = _metadataCache.GetWorkspacePoolFieldsAsync(workspaceId, token, cancellationToken);
        Task<TimelinePage>? timelineTask = null;
        if (includeTimeline)
        {
            const int profileViewTimelineTake = 35;
            timelineTask = GetTimelineAsync(
                workItemId, workItem, 0, profileViewTimelineTake, cancellationToken, formTask, poolFieldsTask);
        }

        var catalogsTask = BuildBoardCatalogsAsync(workspace, workspaceId, scopeStateIds, token, cancellationToken);
        var policyTask = ResolveProfilePolicyAsync(workItem, workspaceId, token, cancellationToken);
        var boardId = WorkItemDataHelper.GetPersonRefId(workItem, "boardId");
        var boardNamesTask = ResolveProfileViewBoardNamesAsync(boardId, token, cancellationToken);

        var parallelTasks = new List<Task>
        {
            profileTask,
            formTask,
            poolFieldsTask,
            catalogsTask,
            policyTask,
            boardNamesTask
        };
        if (timelineTask != null)
            parallelTasks.Add(timelineTask);
        await Task.WhenAll(parallelTasks);

        var profile = profileTask.Result;
        var form = formTask.Result;
        var catalogs = catalogsTask.Result;
        var poolFields = poolFieldsTask.Result;
        var boards = boardNamesTask.Result;

        var profileLayoutKeys = FormLayoutHelper.ExtractOrderedFieldKeys(profile.Layout);
        var displayForm = profileLayoutKeys.Count > 0
            ? await BuildProfileDisplayFormAsync(workItemId, workItem, profile, workspace, cancellationToken)
            : form;

        var fieldDisplays = await BuildProfileFieldDisplaysAsync(
            workItem, displayForm.Fields, catalogs, boards, profile.People, profile.Groups, poolFields, token, cancellationToken);

        var result = new ProfileViewContext
        {
            Profile = profile,
            Form = form,
            DisplayForm = displayForm,
            Catalogs = catalogs,
            Boards = boards,
            PoolFields = poolFields,
            FieldDisplays = fieldDisplays,
            Policy = policyTask.Result,
            Timeline = timelineTask?.Result ?? new TimelinePage(),
        };

        if (perfSw != null)
        {
            perfSw.Stop();
            LogPerf("profile-view", $"workItem={workItemId} displays={fieldDisplays.Count}", perfSw.ElapsedMilliseconds);
        }

        return result;
    }

    private async Task<Dictionary<string, string>> ResolveProfileViewBoardNamesAsync(
        string? boardId,
        string token,
        CancellationToken cancellationToken)
    {
        var boards = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(boardId))
            return boards;

        try
        {
            var board = await _metadataCache.GetBoardAsync(boardId, token, cancellationToken);
            if (!string.IsNullOrEmpty(board.DataId) && !string.IsNullOrWhiteSpace(board.Name))
                boards[board.DataId!] = board.Name!;
        }
        catch (OperationCoreException ex) when (ex.Code == "BOARD_NOT_FOUND")
        {
            _logger.LogDebug("Board {BoardId} not found for profile-view field displays", boardId);
        }

        return boards;
    }

    /// <summary>op_fields → global pool + workspace (metadata cache).</summary>
    private Task<IReadOnlyList<Dictionary<string, object?>>> LoadProfilePoolFieldsAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken) =>
        _metadataCache.GetWorkspacePoolFieldsAsync(workspaceId, token, cancellationToken);

    /// <summary>OcPolicyPanel scopeMatches/matchedPolicy mantığını MO'ya taşır: op_rules/op_sla_policies
    /// cache'li (workspace bazlı) listelerden okunur — warm'da ek DG çağrısı yapılmaz.</summary>
    private async Task<ResolvedPolicyDto> ResolveProfilePolicyAsync(
        IReadOnlyDictionary<string, object?> workItem,
        string workspaceId,
        string token,
        CancellationToken cancellationToken)
    {
        var typeId = WorkItemDataHelper.GetPersonRefId(workItem, "typeId");
        var priorityId = WorkItemDataHelper.GetPersonRefId(workItem, "priorityId");
        var boardId = WorkItemDataHelper.GetPersonRefId(workItem, "boardId");
        var stateId = WorkItemDataHelper.GetPersonRefId(workItem, "stateId");
        var slaPolicyId = SlaSnapshotHelper.MapFromWorkItem(workItem)?.SlaPolicyId;

        IReadOnlyList<RuleRecord> ruleRows;
        IReadOnlyList<SlaPolicyRecord> policyRows;
        try
        {
            var rulesTask = _metadataCache.GetRulesForWorkspaceAsync(workspaceId, token, cancellationToken);
            var policiesTask = _metadataCache.GetSlaPoliciesForWorkspaceAsync(workspaceId, token, cancellationToken);
            await Task.WhenAll(rulesTask, policiesTask);
            ruleRows = rulesTask.Result;
            policyRows = policiesTask.Result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Profile policy resolve failed.");
            return new ResolvedPolicyDto();
        }

        static bool ScopeMatches(string? value, string? target) =>
            string.IsNullOrEmpty(value) || string.Equals(value, target, StringComparison.Ordinal);

        // Kurallar: aktif + name; scope (board/type/state) eşleşmesi; priority asc, name asc.
        var applicableRules = ruleRows
            .Where(r => r.IsActive != false)
            .Where(r => !string.IsNullOrWhiteSpace(r.DataId) && !string.IsNullOrWhiteSpace(r.Name))
            .Where(r => ScopeMatches(r.BoardId, boardId)
                && ScopeMatches(r.TypeId, typeId)
                && ScopeMatches(r.StateId, stateId))
            .OrderBy(r => r.Priority ?? int.MaxValue)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new ResolvedRuleDto
            {
                Id = r.DataId!,
                Name = r.Name,
                Trigger = r.Trigger,
                RuleType = r.RuleType?.Trim().ToLowerInvariant(),
                Description = r.Description,
            })
            .ToList();

        // Politikalar: name geçerli olanlar.
        var validPolicies = policyRows
            .Where(p => !string.IsNullOrWhiteSpace(p.DataId) && !string.IsNullOrWhiteSpace(p.Name))
            .ToList();

        ResolvedSlaPolicyDto? matched = null;

        // 1) snapshot id'si varsa doğrudan onu göster (derived=false).
        if (!string.IsNullOrEmpty(slaPolicyId))
        {
            var direct = validPolicies.FirstOrDefault(p =>
                string.Equals(p.DataId, slaPolicyId, StringComparison.Ordinal));
            if (direct != null)
                matched = MapSlaPolicy(direct, derived: false);
        }

        // 2) yoksa type/priority kapsamıyla en yüksek öncelikli (derived=true).
        if (matched == null)
        {
            var candidate = validPolicies
                .Where(p => p.IsActive != false)
                .Where(p => ScopeMatches(p.TypeId, typeId) && ScopeMatches(p.PriorityId, priorityId))
                .OrderByDescending(p => p.Priority ?? 0)
                .FirstOrDefault();
            if (candidate != null)
                matched = MapSlaPolicy(candidate, derived: true);
        }

        return new ResolvedPolicyDto
        {
            MatchedSlaPolicy = matched,
            ApplicableRules = applicableRules,
        };
    }

    private static ResolvedSlaPolicyDto MapSlaPolicy(SlaPolicyRecord p, bool derived) =>
        new()
        {
            Id = p.DataId ?? string.Empty,
            Name = p.Name,
            ResponseTargetMinutes = p.ResponseTargetMinutes,
            ResolveTargetMinutes = p.ResolveTargetMinutes,
            Derived = derived,
        };

    /// <summary>Form alanlarının çözülmüş görünen metinleri (relation/person/grup/katalog). Scalarlar UI'da ham gösterilir.</summary>
    private async Task<IReadOnlyDictionary<string, string>> BuildProfileFieldDisplaysAsync(
        IReadOnlyDictionary<string, object?> workItem,
        IReadOnlyDictionary<string, FormFieldRuntimeDto> formFields,
        BoardCatalogsDto catalogs,
        IReadOnlyDictionary<string, string> boards,
        IReadOnlyDictionary<string, PersonDisplayDto> people,
        IReadOnlyDictionary<string, PersonDisplayDto> groups,
        IReadOnlyList<Dictionary<string, object?>> poolFields,
        string token,
        CancellationToken cancellationToken)
    {
        var displays = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var poolByKey = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pf in poolFields)
        {
            var k = WorkItemDataHelper.GetString(pf, "key");
            if (!string.IsNullOrWhiteSpace(k) && !poolByKey.ContainsKey(k))
                poolByKey[k] = pf;
        }

        // Profil People map'i yalnız çekirdek person alanlarını içerir; person POOL alanları için
        // eksik id'leri dizinden (cache'li) topluca çöz → readonly metin ham id göstermesin.
        var personFieldIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, field) in formFields)
        {
            var ft = (field.FieldType ?? string.Empty).Trim().ToLowerInvariant();
            if (ft is "persons" or "person" || CorePersonFieldKeys.Contains(key) || key is "reporter" or "createdBy")
            {
                foreach (var id in CollectRefIds(workItem, key))
                    personFieldIds.Add(id);
            }
        }
        var resolvedPeople = people;
        var missingPeople = personFieldIds.Where(id => !people.ContainsKey(id)).ToHashSet(StringComparer.Ordinal);
        if (missingPeople.Count > 0)
        {
            try
            {
                var extra = await _personDirectory.GetPeopleAsync(missingPeople, token, cancellationToken);
                var merged = new Dictionary<string, PersonDisplayDto>(people, StringComparer.Ordinal);
                foreach (var kv in extra)
                    merged[kv.Key] = kv.Value;
                resolvedPeople = merged;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Profile-view person pool field resolve failed.");
            }
        }

        // relation alanlarını dataset + labelField bazında topla → tek seferde çöz.
        var relationFields = new List<(string Key, string Dataset, string LabelField, IReadOnlyList<string> Ids)>();
        var datasetIds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var (key, field) in formFields)
        {
            var ft = (field.FieldType ?? string.Empty).Trim().ToLowerInvariant();

            // 1) çekirdek katalog alanları (state/priority/type).
            if (string.Equals(key, "stateId", StringComparison.OrdinalIgnoreCase))
            {
                SetCatalogDisplay(displays, key, catalogs.States, workItem);
                continue;
            }
            if (string.Equals(key, "priorityId", StringComparison.OrdinalIgnoreCase))
            {
                SetCatalogDisplay(displays, key, catalogs.Priorities, workItem);
                continue;
            }
            if (string.Equals(key, "typeId", StringComparison.OrdinalIgnoreCase))
            {
                SetCatalogDisplay(displays, key, catalogs.Types, workItem);
                continue;
            }
            if (string.Equals(key, "boardId", StringComparison.OrdinalIgnoreCase))
            {
                var bid = WorkItemDataHelper.GetPersonRefId(workItem, key);
                if (!string.IsNullOrEmpty(bid))
                    displays[key] = boards.TryGetValue(bid, out var bn) ? bn : bid;
                continue;
            }

            // 2) person alanları (assignee/reporter/createdBy/watchers + person pool).
            if (ft is "persons" or "person" || CorePersonFieldKeys.Contains(key) || key is "reporter" or "createdBy")
            {
                var names = ResolveRefNames(CollectRefIds(workItem, key), resolvedPeople);
                if (names.Count > 0)
                    displays[key] = string.Join(", ", names);
                continue;
            }

            // 3) grup alanları (assignmentGroups + personGroups pool).
            if (ft is "persongroups" or "persongroup" or "group" || CoreGroupFieldKeys.Contains(key))
            {
                var names = ResolveRefNames(CollectRefIds(workItem, key), groups);
                if (names.Count > 0)
                    displays[key] = string.Join(", ", names);
                continue;
            }

            // 4) relation alanları (pool relationDatasetName veya çekirdek relation key).
            string? dataset = null;
            if (poolByKey.TryGetValue(key, out var pf2))
                dataset = WorkItemDataHelper.GetString(pf2, "relationDatasetName");
            if (string.IsNullOrWhiteSpace(dataset) && (ft == "relation" || CoreRelationDatasets.ContainsKey(key)))
                CoreRelationDatasets.TryGetValue(key, out dataset);
            // 'tags' pool alanı → workspace etiket kataloğu (op_tags); id→ad çözülür.
            if (string.IsNullOrWhiteSpace(dataset) && ft == "tags")
                dataset = OcDatasets.Tags;

            if (!string.IsNullOrWhiteSpace(dataset))
            {
                var ids = CollectRefIds(workItem, key);
                if (ids.Count > 0)
                {
                    poolByKey.TryGetValue(key, out var poolField);
                    var labelField = LookupFieldOptionsHelper.ResolveLabelField(poolField);
                    relationFields.Add((key, dataset!, labelField, ids));
                    var resolveKey = LookupFieldOptionsHelper.ComposeResolveKey(dataset!, labelField);
                    if (!datasetIds.TryGetValue(resolveKey, out var set))
                        datasetIds[resolveKey] = set = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var id in ids)
                        set.Add(id);
                }
                continue;
            }

            // diğer (text/number/date/bool) → display üretme; UI ham değeri gösterir.
        }

        // dataset+labelField bazında id → ad çöz (paralel).
        var nameMapTasks = datasetIds.ToDictionary(
            kv => kv.Key,
            kv =>
            {
                var (dataset, labelField) = LookupFieldOptionsHelper.ParseResolveKey(kv.Key);
                return ResolveRelationNamesAsync(dataset, kv.Value, labelField, token, cancellationToken);
            });
        await Task.WhenAll(nameMapTasks.Values);

        foreach (var (key, dataset, labelField, ids) in relationFields)
        {
            var resolveKey = LookupFieldOptionsHelper.ComposeResolveKey(dataset, labelField);
            var map = nameMapTasks.TryGetValue(resolveKey, out var task) ? task.Result : null;
            var names = ids
                .Select(id => map != null && map.TryGetValue(id, out var nm) && !string.IsNullOrWhiteSpace(nm) ? nm : id)
                .ToList();
            if (names.Count > 0)
                displays[key] = string.Join(", ", names);
        }

        return displays;
    }

    private static void SetCatalogDisplay(
        IDictionary<string, string> displays,
        string key,
        IReadOnlyDictionary<string, CatalogDisplayDto> catalog,
        IReadOnlyDictionary<string, object?> workItem)
    {
        var id = WorkItemDataHelper.GetPersonRefId(workItem, key);
        if (string.IsNullOrEmpty(id))
            return;
        displays[key] = catalog.TryGetValue(id, out var c) && !string.IsNullOrWhiteSpace(c.Name) ? c.Name! : id;
    }

    private static List<string> ResolveRefNames(
        IReadOnlyList<string> ids,
        IReadOnlyDictionary<string, PersonDisplayDto> directory)
    {
        var names = new List<string>(ids.Count);
        foreach (var id in ids)
            names.Add(directory.TryGetValue(id, out var p) && !string.IsNullOrWhiteSpace(p.Name) ? p.Name! : id);
        return names;
    }

    private Task<Dictionary<string, string>> ResolveRelationNamesAsync(
        string dataset,
        IReadOnlyCollection<string> ids,
        string labelField,
        string token,
        CancellationToken cancellationToken) =>
        ResolveRelationNamesViaQueryAsync(dataset, ids, labelField, token, cancellationToken);

    private async Task<Dictionary<string, string>> ResolveRelationNamesViaQueryAsync(
        string dataset,
        IReadOnlyCollection<string> ids,
        string labelField,
        string token,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            var resolved = await _metadataCache.ResolveRelationDisplayNamesAsync(
                dataset, labelField, ids, token, cancellationToken);
            return new Dictionary<string, string>(resolved, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Relation name resolve failed for dataset {Dataset}.", dataset);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>Alan değerinden id listesi (string / dizi / genişletilmiş relation nesnesi). Sıra + tekilleştirme korunur.</summary>
    private static IReadOnlyList<string> CollectRefIds(IReadOnlyDictionary<string, object?> workItem, string key)
    {
        var ids = new List<string>();
        CollectRefIdsFromValue(WorkItemDataHelper.GetFieldValue(workItem, key), ids);
        return ids;
    }

    private static void CollectRefIdsFromValue(object? value, List<string> ids)
    {
        switch (value)
        {
            case null:
                return;
            case JsonElement el:
                CollectRefIdsFromElement(el, ids);
                return;
            case string s:
                AddRefId(ids, s);
                return;
            case System.Collections.IEnumerable enumerable:
                foreach (var item in enumerable)
                    CollectRefIdsFromValue(item, ids);
                return;
            default:
                AddRefId(ids, value.ToString());
                return;
        }
    }

    private static readonly string[] RefIdProps = { "__dataId", "_id", "id" };

    private static void CollectRefIdsFromElement(JsonElement el, List<string> ids)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                AddRefId(ids, el.GetString());
                break;
            case JsonValueKind.Number:
                AddRefId(ids, el.ToString());
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    CollectRefIdsFromElement(item, ids);
                break;
            case JsonValueKind.Object:
                foreach (var n in RefIdProps)
                {
                    if (el.TryGetProperty(n, out var idEl)
                        && idEl.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(idEl.GetString()))
                    {
                        AddRefId(ids, idEl.GetString());
                        break;
                    }
                }
                break;
        }
    }

    private static void AddRefId(List<string> ids, string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        var trimmed = id.Trim();
        if (!ids.Contains(trimmed))
            ids.Add(trimmed);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        return null;
    }
}
