using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Diagnostics;
using MngOperations.Application.Exceptions;
using MngOperations.Application.FieldBehaviors;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Permissions;
using MngOperations.Application.Rules;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

/// <summary>
/// Runtime context servisi. Okunabilirlik için <c>partial</c> dosyalara bölünmüştür:
/// bu dosya çekirdek (ctor/alanlar + profil/board/query + ortak yardımcılar); ayrıca
/// <c>.Dashboard.cs</c>, <c>.Directory.cs</c>, <c>.Form.cs</c>. Davranış aynıdır.
/// </summary>
public partial class RuntimeContextService : IRuntimeContextService
{
    private readonly IMngDataGatewayClient _dg;
    private readonly IMetadataCache _metadataCache;
    private readonly IPermissionEvaluator _permissions;
    private readonly IFieldBehaviorResolver _fieldBehaviors;
    private readonly IPersonDirectory _personDirectory;
    private readonly IGroupDirectory _groupDirectory;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<RuntimeContextService> _logger;
    private readonly OcCallStats _stats;
    private readonly bool _perfDiagnostics;

    public RuntimeContextService(
        IMngDataGatewayClient dg,
        IMetadataCache metadataCache,
        IPermissionEvaluator permissions,
        IFieldBehaviorResolver fieldBehaviors,
        IPersonDirectory personDirectory,
        IGroupDirectory groupDirectory,
        IRequestContext requestContext,
        ILogger<RuntimeContextService> logger,
        OcCallStats stats,
        IOptions<MngOperationsSettings> settings)
    {
        _dg = dg;
        _metadataCache = metadataCache;
        _permissions = permissions;
        _fieldBehaviors = fieldBehaviors;
        _personDirectory = personDirectory;
        _groupDirectory = groupDirectory;
        _requestContext = requestContext;
        _logger = logger;
        _stats = stats;
        _perfDiagnostics = settings.Value.PerfDiagnostics;
    }

    // GEÇİCİ (perf/oc-optimization): endpoint sonunda DG/Keeper çağrı kırılımı.
    private void LogPerf(string endpoint, string detail, long totalMs)
    {
        if (!_perfDiagnostics) return;
        _logger.LogInformation(
            "OC_PERF {Endpoint} {Detail} totalMs={TotalMs} dgCalls={DgCalls} dgMs={DgMs} keeperCalls={KeeperCalls} keeperMs={KeeperMs} ops=[{Ops}]",
            endpoint, detail, totalMs, _stats.DgCount, _stats.DgMs, _stats.KeeperCount, _stats.KeeperMs, _stats.OpSummary());
    }

    // Çekirdek person alanları — fieldType'a bakılmaksızın daima person.
    private static readonly string[] CorePersonFieldKeys = { "assignee", "watchers" };

    // Çekirdek person grup alanları — fieldType'a bakılmaksızın daima grup.
    private static readonly string[] CoreGroupFieldKeys = { "assignmentGroups" };

    public async Task<ProfileRuntimeContext> GetProfileAsync(
        string workItemId,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var workItem = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        return await GetProfileAsync(workItemId, workItem, cancellationToken);
    }

    /// <summary>Önceden yüklenmiş work item ile (profile-view içinde tekrar DG GetById yapmamak için).</summary>
    private async Task<ProfileRuntimeContext> GetProfileAsync(
        string workItemId,
        Dictionary<string, object?> workItem,
        CancellationToken cancellationToken = default)
    {
        var perfSw = _perfDiagnostics ? Stopwatch.StartNew() : null;
        var token = RequireToken();
        var workspaceId = WorkItemDataHelper.GetString(workItem, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing.", "workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureWorkItemView(workspace, workItem);

        // İş kaydına bağlı (yalnız workItemId gerektiren) bağımsız veri çağrılarını erken başlat;
        // aşağıdaki metadata + field-behavior çözümlemesiyle örtüşsünler (profil warm darboğazı).
        var linksFilterOut = $"sourceWorkItemId:eq:{workItemId}";
        var linksFilterIn = $"targetWorkItemId:eq:{workItemId}";
        var segmentsFilter = $"workItemId:eq:{workItemId}";

        var outgoingLinksTask = _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.Links,
            $"filter={Uri.EscapeDataString(linksFilterOut)}&limit=50",
            token,
            cancellationToken);

        var incomingLinksTask = _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.Links,
            $"filter={Uri.EscapeDataString(linksFilterIn)}&limit=50",
            token,
            cancellationToken);

        // Profil yalnız son DefaultStateSegmentCount segmenti gösterir; en yeniler için DG-side sort.
        var segmentsTask = _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.WorkItemTimelines,
            $"filter={Uri.EscapeDataString(segmentsFilter)}&sort=-enteredAt&limit={ProfileRuntimeMapper.DefaultStateSegmentCount}",
            token,
            cancellationToken);

        var stateFlowId = WorkItemDataHelper.GetString(workItem, "stateFlowId");
        var currentStateId = WorkItemDataHelper.GetString(workItem, "stateId") ?? string.Empty;
        var boardId = WorkItemDataHelper.GetString(workItem, "boardId");

        var profileTask = _metadataCache.ResolveDefaultProfileAsync(workspaceId, token, cancellationToken);
        Task<StateFlowRecord>? stateFlowTask = null;
        if (!string.IsNullOrEmpty(stateFlowId))
            stateFlowTask = _metadataCache.GetStateFlowAsync(stateFlowId, token, cancellationToken);
        Task<BoardRecord?>? boardTask = null;
        if (!string.IsNullOrEmpty(boardId))
            boardTask = TryGetBoardForProfileAsync(boardId, token, cancellationToken);

        var metadataWaits = new List<Task> { profileTask };
        if (stateFlowTask != null) metadataWaits.Add(stateFlowTask);
        if (boardTask != null) metadataWaits.Add(boardTask);
        await Task.WhenAll(metadataWaits);

        var profile = profileTask.Result;

        var availableActions = new List<ProfileActionDto>();
        if (stateFlowTask != null)
        {
            var stateFlow = stateFlowTask.Result;
            var order = 0;
            foreach (var transition in _permissions.GetAvailableTransitions(workspace, stateFlow, currentStateId))
            {
                var key = StateFlowCatalog.GetStringProperty(transition, "transitionKey") ?? string.Empty;
                availableActions.Add(new ProfileActionDto
                {
                    TransitionKey = key,
                    Label = StateFlowCatalog.GetStringProperty(transition, "label")
                        ?? StateFlowCatalog.GetStringProperty(transition, "name"),
                    FromStateId = StateFlowCatalog.GetStringProperty(transition, "fromStateId"),
                    ToStateId = StateFlowCatalog.GetStringProperty(transition, "toStateId") ?? string.Empty,
                    Enabled = _permissions.CanApplyTransition(workspace, transition),
                    Order = order++,
                    RequiredFields = StateFlowCatalog.GetRequiredFields(transition)
                });
            }
        }

        var actions = ProfileActionBuilder.Build(availableActions, profile?.Actions);

        var canEdit = _permissions.CanEditWorkItem(workspace, workItem);
        BoardRecord? board = boardTask?.Result;

        var behaviorContext = new FieldBehaviorResolveContext
        {
            Screen = FieldBehaviorScreen.Profile,
            Mode = "edit",
            Workspace = workspace,
            WorkItem = workItem,
            Profile = profile,
            Board = board,
            StateId = currentStateId,
            CanEdit = canEdit,
            RuleTrigger = RuleTriggers.WorkItemUpdated
        };

        var fieldBehaviors = await _fieldBehaviors.ResolveAllAsync(behaviorContext, cancellationToken);

        await Task.WhenAll(outgoingLinksTask, incomingLinksTask, segmentsTask);

        var links = outgoingLinksTask.Result
            .Select(ProfileRuntimeMapper.MapOutgoingLink)
            .Concat(incomingLinksTask.Result.Select(ProfileRuntimeMapper.MapIncomingLink))
            .ToList();

        var stateSegments = segmentsTask.Result
            .Select(ProfileRuntimeMapper.MapStateSegment)
            .OrderByDescending(s => s.EnteredAt)
            .Take(ProfileRuntimeMapper.DefaultStateSegmentCount)
            .OrderBy(s => s.EnteredAt)
            .ToList();

        var profilePeopleIds = new HashSet<string>(StringComparer.Ordinal);
        AddPersonId(profilePeopleIds, WorkItemDataHelper.GetString(workItem, "assignee"));
        AddPersonId(profilePeopleIds, WorkItemDataHelper.GetString(workItem, "reporter"));
        AddPersonId(profilePeopleIds, WorkItemDataHelper.GetString(workItem, "createdBy"));
        foreach (var w in WorkItemDataHelper.GetStringList(workItem, "watchers"))
            AddPersonId(profilePeopleIds, w);
        var profilePeople = profilePeopleIds.Count > 0
            ? await _personDirectory.GetPeopleAsync(profilePeopleIds, token, cancellationToken)
            : new Dictionary<string, PersonDisplayDto>();

        // Grup alanları (assignmentGroups + personGroups tipi pool alanlar) — id → grup adı.
        var profileGroupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in WorkItemDataHelper.GetStringList(workItem, "assignmentGroups"))
            AddPersonId(profileGroupIds, g);
        try
        {
            var groupPoolKeys = await GetGroupPoolFieldKeysAsync(token, cancellationToken);
            foreach (var key in groupPoolKeys)
            {
                foreach (var g in WorkItemDataHelper.GetStringList(workItem, key))
                    AddPersonId(profileGroupIds, g);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Profile group pool field collect failed.");
        }
        var profileGroups = profileGroupIds.Count > 0
            ? await _groupDirectory.GetGroupsAsync(profileGroupIds, token, cancellationToken)
            : new Dictionary<string, PersonDisplayDto>();

        JsonElement? attachments = null;
        if (workItem.TryGetValue("attachments", out var attVal)
            && attVal is JsonElement { ValueKind: JsonValueKind.Array } attEl)
        {
            attachments = attEl;
        }

        var result = new ProfileRuntimeContext
        {
            WorkspaceId = workspaceId,
            WorkItem = ProfileRuntimeBuilder.BuildSummary(workItemId, workItem),
            Permissions = new RuntimePermissionsDto
            {
                CanView = true,
                CanEdit = canEdit,
                CanComment = canEdit || _permissions.CanViewWorkItem(workspace, workItem)
            },
            Actions = actions,
            ProfileId = profile?.DataId,
            ProfileName = profile?.Name,
            Header = profile?.Header,
            Sidebar = profile?.Sidebar,
            Panels = profile?.Panels,
            Layout = profile?.Layout,
            Fields = ProfileRuntimeBuilder.BuildFields(workItem),
            FieldBehaviors = fieldBehaviors,
            Sla = SlaSnapshotHelper.MapFromWorkItem(workItem),
            Watchers = WorkItemDataHelper.GetStringList(workItem, "watchers"),
            Links = links,
            StateSegments = stateSegments,
            People = profilePeople,
            Groups = profileGroups,
            Attachments = attachments
        };

        if (perfSw != null)
        {
            perfSw.Stop();
            LogPerf("profile", $"workItem={workItemId} fields={result.Fields.Count}", perfSw.ElapsedMilliseconds);
        }

        return result;
    }

    private async Task<BoardRecord?> TryGetBoardForProfileAsync(
        string boardId,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _metadataCache.GetBoardAsync(boardId, token, cancellationToken);
        }
        catch (OperationCoreException ex) when (ex.Code == "BOARD_NOT_FOUND")
        {
            _logger.LogDebug("Board {BoardId} not found for profile field behaviors", boardId);
            return null;
        }
    }

    public async Task<StateSegmentsPage> GetStateSegmentsAsync(
        string workItemId,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var workItem = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        var workspaceId = WorkItemDataHelper.GetString(workItem, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing.", "workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureWorkItemView(workspace, workItem);

        var filter = $"workItemId:eq:{workItemId}";
        var segments = await _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.WorkItemTimelines,
            $"filter={Uri.EscapeDataString(filter)}&limit=200",
            token,
            cancellationToken);

        var items = segments
            .Select(ProfileRuntimeMapper.MapStateSegment)
            .OrderBy(s => s.EnteredAt)
            .ToList();

        return new StateSegmentsPage
        {
            Items = items,
            Total = items.Count
        };
    }

    public async Task<TimelinePage> GetTimelineAsync(
        string workItemId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var workItem = await LoadWorkItemAsync(workItemId, token, cancellationToken);
        return await GetTimelineAsync(workItemId, workItem, skip, take, cancellationToken);
    }

    /// <summary>Önceden yüklenmiş work item ile (profile-view içinde tekrar DG GetById yapmamak için).</summary>
    private async Task<TimelinePage> GetTimelineAsync(
        string workItemId,
        Dictionary<string, object?> workItem,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var token = RequireToken();
        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);

        var workspaceId = WorkItemDataHelper.GetString(workItem, "workspaceId")
            ?? throw new OperationCoreException("WORK_ITEM_INVALID", "workspaceId missing.", "workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureWorkItemView(workspace, workItem);

        var sourceFilter = $"sourceDataset:eq:{OcDatasets.WorkItems},sourceRecordId:eq:{workItemId}";

        var commentsTask = _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.Comments,
            $"filter={Uri.EscapeDataString(sourceFilter)}&limit=500",
            token,
            cancellationToken);

        var activitiesTask = _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.Activities,
            $"filter={Uri.EscapeDataString(sourceFilter)}&limit=500",
            token,
            cancellationToken);

        await Task.WhenAll(commentsTask, activitiesTask);

        var commentList = commentsTask.Result.ToList();
        var activityList = activitiesTask.Result.ToList();

        // Aktivite alan değişiklik satırlarını (changes[]) read-time çöz (changes yoksa metadata yüklenmez).
        var changesTask = ResolveActivityChangesAsync(workItemId, workItem, workspaceId, activityList, token, cancellationToken);

        // Yazar/actor person id'lerini topla ve People diziniyle ada çöz (BL-KB toplu uç + Redis cache).
        // author/actor alanı DG okumada düz id veya tam @users nesnesine genişlemiş gelebilir → her ikisini
        // de GetPersonRefId ile id'ye indirgeriz. Eski kayıtlar username taşır → dizinde bulunmaz, ham döner.
        var actorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in commentList)
        {
            var id = WorkItemDataHelper.GetPersonRefId(c, "author");
            if (!string.IsNullOrWhiteSpace(id)) actorIds.Add(id!);
        }
        foreach (var a in activityList)
        {
            var id = WorkItemDataHelper.GetPersonRefId(a, "actor");
            if (!string.IsNullOrWhiteSpace(id)) actorIds.Add(id!);
        }
        var actorPeople = actorIds.Count > 0
            ? await _personDirectory.GetPeopleAsync(actorIds, token, cancellationToken)
            : new Dictionary<string, PersonDisplayDto>();

        // Önce id ile People dizininden (title/cache) çöz; olmazsa genişletilmiş nesnedeki ad/soyad'a,
        // en son ham id'ye düş.
        string? ResolveActor(IReadOnlyDictionary<string, object?> data, string key)
        {
            var id = WorkItemDataHelper.GetPersonRefId(data, key);
            if (!string.IsNullOrWhiteSpace(id)
                && actorPeople.TryGetValue(id!, out var person)
                && !string.IsNullOrWhiteSpace(person.Name))
            {
                return person.Name;
            }

            return WorkItemDataHelper.GetPersonRefName(data, key) ?? id;
        }

        var entries = new List<TimelineEntryDto>();

        foreach (var comment in commentList)
        {
            JsonElement? commentAttachments = null;
            if (comment.TryGetValue("attachments", out var attVal)
                && attVal is JsonElement { ValueKind: JsonValueKind.Array } attEl)
            {
                commentAttachments = attEl;
            }

            entries.Add(new TimelineEntryDto
            {
                Type = "comment",
                Id = WorkItemDataHelper.GetDataId(comment),
                Actor = ResolveActor(comment, "author"),
                ActorId = WorkItemDataHelper.GetPersonRefId(comment, "author"),
                Text = WorkItemDataHelper.GetString(comment, "body"),
                At = WorkItemDataHelper.GetDateTime(comment, "commentDate"),
                EditedAt = WorkItemDataHelper.GetDateTime(comment, "editedDate"),
                ParentId = WorkItemDataHelper.GetString(comment, "parentCommentId"),
                Attachments = commentAttachments
            });
        }

        var changesMap = await changesTask;

        foreach (var activity in activityList)
        {
            var activityId = WorkItemDataHelper.GetDataId(activity);
            entries.Add(new TimelineEntryDto
            {
                Type = "activity",
                Id = activityId,
                Actor = ResolveActor(activity, "actor"),
                ActorId = WorkItemDataHelper.GetPersonRefId(activity, "actor"),
                Text = WorkItemDataHelper.GetString(activity, "message"),
                At = WorkItemDataHelper.GetDateTime(activity, "activityDate"),
                ActivityType = WorkItemDataHelper.GetString(activity, "activityType"),
                Changes = activityId != null && changesMap.TryGetValue(activityId, out var ch) && ch.Count > 0 ? ch : null
            });
        }

        var ordered = entries
            .OrderByDescending(e => e.At ?? DateTime.MinValue)
            .ToList();

        var page = ordered.Skip(skip).Take(take).ToList();

        return new TimelinePage
        {
            Items = page,
            Skip = skip,
            Take = take,
            Total = ordered.Count
        };
    }

    public async Task<BoardRuntimeContext> GetBoardAsync(
        string boardId,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var board = await _metadataCache.GetBoardAsync(boardId, token, cancellationToken);
        var workspaceId = board.WorkspaceId
            ?? throw new OperationCoreException("BOARD_INVALID", "workspaceId missing on board.", "Board workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureBoardView(workspace, board);

        var stateFlowId = board.DefaultStateFlowId ?? workspace.DefaultStateFlowId;
        JsonElement? transitions = null;
        string? initialStateId = null;

        if (!string.IsNullOrEmpty(stateFlowId))
        {
            var stateFlow = await _metadataCache.GetStateFlowAsync(stateFlowId, token, cancellationToken);
            transitions = stateFlow.Transitions;
            initialStateId = stateFlow.InitialStateId;
        }

        JsonElement? configColumns = null;
        JsonElement? configObject = null;
        if (board.Config is { ValueKind: JsonValueKind.Object } config)
        {
            configObject = config;
            if (config.TryGetProperty("columns", out var configCols))
                configColumns = configCols;
        }

        var boardColumns = BoardColumnBuilder.Build(
            transitions,
            initialStateId,
            workspaceId,
            boardId,
            configColumns);

        var boardScopeStateIds = boardColumns
            .Select(c => c.StateId)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var listColumns = ParseListColumns(configObject);
        // Computed sütunların DG karşılığı yoktur; alan seçiminden (cardFieldKeys) hariç tutulur.
        var cardFieldKeys = listColumns.Count > 0
            ? listColumns.Where(c => !c.Computed).Select(c => c.Key).ToList()
            : ParseCardFieldKeys(board.VisibleFields);

        return new BoardRuntimeContext
        {
            BoardId = boardId,
            WorkspaceId = workspaceId,
            Name = board.Name,
            ViewType = board.ViewType,
            Permissions = new RuntimePermissionsDto
            {
                CanView = true,
                CanEdit = _permissions.CanEditWorkItem(workspace, new Dictionary<string, object?>()),
                CanComment = _permissions.CanEditWorkItem(workspace, new Dictionary<string, object?>())
            },
            Columns = boardColumns,
            CardFieldKeys = cardFieldKeys,
            ListColumns = listColumns,
            DefaultSort = ParseDefaultSort(configObject),
            InitialStateId = initialStateId,
            Catalogs = await BuildBoardCatalogsAsync(workspace, workspaceId, boardScopeStateIds, token, cancellationToken)
        };
    }

    /// <summary>board.config.listColumns[] → liste sütun meta (sıra + sortable/filterable).</summary>
    private static IReadOnlyList<BoardListColumnDto> ParseListColumns(JsonElement? configObject)
    {
        if (configObject is not { ValueKind: JsonValueKind.Object } config
            || !config.TryGetProperty("listColumns", out var listCols)
            || listCols.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<BoardListColumnDto>();
        }

        var result = new List<BoardListColumnDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var col in listCols.EnumerateArray())
        {
            if (col.ValueKind != JsonValueKind.Object)
                continue;

            var key = StateFlowCatalog.GetStringProperty(col, "key");
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
                continue;

            var format = StateFlowCatalog.GetStringProperty(col, "format");
            var computed = ReadBoolProperty(col, "computed");
            var expr = StateFlowCatalog.GetStringProperty(col, "expr");
            var label = StateFlowCatalog.GetStringProperty(col, "label");
            result.Add(new BoardListColumnDto
            {
                Key = key,
                // Computed sütunlar sunucu tarafı sort/filter yapamaz (DG alanı yok) → zorla kapat.
                Sortable = !computed && ReadBoolProperty(col, "sortable"),
                Filterable = !computed && ReadBoolProperty(col, "filterable"),
                Format = string.IsNullOrWhiteSpace(format) ? null : format.Trim(),
                Computed = computed,
                Expr = computed && !string.IsNullOrWhiteSpace(expr) ? expr.Trim() : null,
                Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim()
            });
        }

        return result;
    }

    /// <summary>board.config.defaultSort → { field, direction }.</summary>
    private static BoardSortDto? ParseDefaultSort(JsonElement? configObject)
    {
        if (configObject is not { ValueKind: JsonValueKind.Object } config
            || !config.TryGetProperty("defaultSort", out var sort)
            || sort.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var field = StateFlowCatalog.GetStringProperty(sort, "field");
        if (string.IsNullOrWhiteSpace(field))
            return null;

        var direction = StateFlowCatalog.GetStringProperty(sort, "direction")?.Trim().ToLowerInvariant();
        return new BoardSortDto
        {
            Field = field,
            Direction = direction == "desc" ? "desc" : "asc"
        };
    }

    private static bool ReadBoolProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Board liste/filtre katalogları workspace kapsamına indirgenir:
    /// state = board akış kapsamı ∪ workspace.enabledStateIds; priority/type = workspace.enabled*Ids.
    /// İlgili kapsam boşsa (workspace kısıtlamamışsa) tüm kataloğa düşer; type için workspaceId yedeği var.
    /// </summary>
    private async Task<BoardCatalogsDto> BuildBoardCatalogsAsync(
        WorkspaceRecord workspace,
        string workspaceId,
        IReadOnlyList<string> scopeStateIds,
        string token,
        CancellationToken cancellationToken)
    {
        var states = await _metadataCache.GetCatalogListAsync(OcDatasets.States, token, cancellationToken);
        var priorities = await _metadataCache.GetCatalogListAsync(OcDatasets.Priorities, token, cancellationToken);
        var types = await _metadataCache.GetCatalogListAsync(OcDatasets.WorkItemTypes, token, cancellationToken);

        // States: board akış kapsamı + workspace.enabledStateIds; boşsa tüm katalog.
        var stateScope = new HashSet<string>(scopeStateIds, StringComparer.Ordinal);
        foreach (var id in ResolveEnabledIds(workspace.EnabledStateIds, workspace.Settings, "enabledStateIds"))
            stateScope.Add(id);
        var scopedStates = stateScope.Count > 0
            ? states.Where(r => stateScope.Contains(WorkItemDataHelper.GetString(r, "__dataId") ?? string.Empty)).ToList()
            : (IReadOnlyList<Dictionary<string, object?>>)states;

        // Priorities: workspace.enabledPriorityIds; boşsa tüm katalog.
        var priorityScope = new HashSet<string>(
            ResolveEnabledIds(workspace.EnabledPriorityIds, workspace.Settings, "enabledPriorityIds"),
            StringComparer.Ordinal);
        var scopedPriorities = priorityScope.Count > 0
            ? priorities.Where(r => priorityScope.Contains(WorkItemDataHelper.GetString(r, "__dataId") ?? string.Empty)).ToList()
            : (IReadOnlyList<Dictionary<string, object?>>)priorities;

        // Types: workspace.enabledTypeIds; boşsa workspaceId'ye ait tipler; o da boşsa tüm katalog.
        var typeScope = new HashSet<string>(
            ResolveEnabledIds(workspace.EnabledTypeIds, workspace.Settings, "enabledTypeIds"),
            StringComparer.Ordinal);
        IReadOnlyList<Dictionary<string, object?>> scopedTypes;
        if (typeScope.Count > 0)
        {
            scopedTypes = types.Where(r => typeScope.Contains(WorkItemDataHelper.GetString(r, "__dataId") ?? string.Empty)).ToList();
        }
        else
        {
            var wsTypes = types
                .Where(r => string.Equals(WorkItemDataHelper.GetString(r, "workspaceId"), workspaceId, StringComparison.Ordinal))
                .ToList();
            scopedTypes = wsTypes.Count > 0 ? wsTypes : types;
        }

        return new BoardCatalogsDto
        {
            States = ToCatalogDisplayMap(scopedStates),
            Priorities = ToCatalogDisplayMap(scopedPriorities),
            Types = ToCatalogDisplayMap(scopedTypes),
        };
    }

    /// <summary>enabled*Ids alanı (öncelik) → boşsa workspace.settings yedeği.</summary>
    private static IReadOnlyList<string> ResolveEnabledIds(JsonElement? field, JsonElement? settings, string settingsKey)
    {
        var ids = MetadataRelationHelper.ParseIdList(field);
        if (ids.Count > 0)
            return ids;

        if (settings is { ValueKind: JsonValueKind.Object } s
            && s.TryGetProperty(settingsKey, out var prop))
        {
            return MetadataRelationHelper.ParseIdList(prop);
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyDictionary<string, CatalogDisplayDto> ToCatalogDisplayMap(
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var map = new Dictionary<string, CatalogDisplayDto>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var id = WorkItemDataHelper.GetString(row, "__dataId");
            if (string.IsNullOrWhiteSpace(id) || map.ContainsKey(id))
                continue;

            map[id] = new CatalogDisplayDto
            {
                Id = id,
                Name = WorkItemDataHelper.GetString(row, "name"),
                Color = WorkItemDataHelper.GetString(row, "color"),
                Icon = WorkItemDataHelper.GetString(row, "icon"),
            };
        }

        return map;
    }

    public async Task<QueryExecuteResponse> ExecuteQueryAsync(
        string queryKey,
        ExecuteQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var dataset = string.IsNullOrWhiteSpace(request.Dataset) ? OcDatasets.WorkItems : request.Dataset.Trim();
        var take = Math.Clamp(request.Take, 1, 200);
        var skip = Math.Max(0, request.Skip);

        var rawParams = ParseQueryParameters(request.Parameters);
        var workspaceHint = rawParams.TryGetValue("workspaceId", out var ws) ? ws?.ToString() : null;
        var boardHint = rawParams.TryGetValue("boardId", out var b) ? b?.ToString() : null;

        return await ExecuteQueryCoreAsync(
            queryKey,
            dataset,
            rawParams,
            skip,
            take,
            token,
            new QueryResolveContext
            {
                WorkspaceId = workspaceHint,
                BoardId = boardHint,
                CurrentUserId = _requestContext.MngPersonId,
                UtcNow = DateTime.UtcNow
            },
            cancellationToken);
    }

    /// <summary>
    /// Board liste görünümü — tek sunucu tarafı sorgu (DG POST /query): scope state'leri ($in),
    /// per-column filtre (yalnızca filterable alanlar), sıralama (sortable doğrulamalı, yoksa defaultSort),
    /// arama ve sayfalama. Toplam DG'den (X-Total-Count) gelir; person/katalog zenginleştirme MO-side kalır.
    /// </summary>
    public async Task<QueryExecuteResponse> GetBoardListAsync(
        string boardId,
        BoardListRequest request,
        CancellationToken cancellationToken = default)
    {
        var perfSw = _perfDiagnostics ? Stopwatch.StartNew() : null;
        var token = RequireToken();
        var board = await _metadataCache.GetBoardAsync(boardId, token, cancellationToken);
        var workspaceId = board.WorkspaceId
            ?? throw new OperationCoreException("BOARD_INVALID", "workspaceId missing on board.", "Board workspaceId yok.", 500);

        var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
        _permissions.EnsureBoardView(workspace, board);

        var stateFlowId = board.DefaultStateFlowId ?? workspace.DefaultStateFlowId;
        JsonElement? transitions = null;
        string? initialStateId = null;
        if (!string.IsNullOrEmpty(stateFlowId))
        {
            var stateFlow = await _metadataCache.GetStateFlowAsync(stateFlowId, token, cancellationToken);
            transitions = stateFlow.Transitions;
            initialStateId = stateFlow.InitialStateId;
        }

        JsonElement? configColumns = null;
        JsonElement? configObject = null;
        if (board.Config is { ValueKind: JsonValueKind.Object } config)
        {
            configObject = config;
            if (config.TryGetProperty("columns", out var cc))
                configColumns = cc;
        }

        var boardColumns = BoardColumnBuilder.Build(transitions, initialStateId, workspaceId, boardId, configColumns);
        var scopeStateIds = boardColumns
            .Select(c => c.StateId)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var listColumns = ParseListColumns(configObject);
        var sortableKeys = new HashSet<string>(listColumns.Where(c => c.Sortable).Select(c => c.Key), StringComparer.Ordinal);
        var filterableKeys = new HashSet<string>(listColumns.Where(c => c.Filterable).Select(c => c.Key), StringComparer.Ordinal);
        var defaultSort = ParseDefaultSort(configObject);

        var take = Math.Clamp(request.Take, 1, 200);
        var skip = Math.Max(0, request.Skip);

        // --- native Mongo match ($and: scope + kullanıcı koşulları AND'lenir) ---
        // $and kullanımı; aynı alana birden çok koşul (gelişmiş arama) ezilmeden birleşir,
        // kullanıcı stateId filtresi board akış kapsamıyla kesişir (kapsam dışına çıkamaz).
        var and = new List<object?>
        {
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId },
            new Dictionary<string, object?> { ["boardId"] = boardId }
        };
        if (scopeStateIds.Count == 1)
            and.Add(new Dictionary<string, object?> { ["stateId"] = scopeStateIds[0] });
        else if (scopeStateIds.Count > 1)
            and.Add(new Dictionary<string, object?> { ["stateId"] = new Dictionary<string, object?> { ["$in"] = scopeStateIds.Cast<object?>().ToList() } });

        if (request.Filters != null)
        {
            foreach (var f in request.Filters)
            {
                if (f == null || string.IsNullOrWhiteSpace(f.Field) || string.IsNullOrWhiteSpace(f.Value))
                    continue;
                if (!filterableKeys.Contains(f.Field))
                    continue;

                var path = MapBoardFieldToDgPath(f.Field);
                // workspaceId/boardId sabit kapsam — kullanıcı filtresi olamaz (stateId $and ile kesişerek güvenli).
                if (path is "workspaceId" or "boardId")
                    continue;

                var condition = BuildMatchCondition(f.Operator, f.Value!);
                if (condition != null)
                    and.Add(new Dictionary<string, object?> { [path] = condition });
            }
        }

        var match = new Dictionary<string, object?>(StringComparer.Ordinal) { ["$and"] = and };

        // --- sort ---
        var sort = request.Sort;
        if (sort != null && (string.IsNullOrWhiteSpace(sort.Field) || !sortableKeys.Contains(sort.Field)))
            sort = null;
        sort ??= defaultSort;

        string sortExpr;
        if (sort != null && !string.IsNullOrWhiteSpace(sort.Field))
        {
            var (path, invert) = MapSortField(sort.Field);
            var desc = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
            if (invert) desc = !desc;
            sortExpr = desc ? $"-{path}" : path;
        }
        else
        {
            sortExpr = "-lastStateChangeAt";
        }

        var queryParts = new List<string>
        {
            $"sort={Uri.EscapeDataString(sortExpr)}",
            $"skip={skip}",
            $"limit={take}",
            "expand=false"
        };
        if (!string.IsNullOrWhiteSpace(request.Search))
            queryParts.Add($"search={Uri.EscapeDataString(request.Search.Trim())}");

        var page = await _dg.QueryPageAsync(OcDatasets.WorkItems, match, string.Join("&", queryParts), token, cancellationToken);

        var cards = page.Items.Select(MapWorkItemCard).ToList();
        var people = await ResolvePeopleForCardsAsync(cards, token, cancellationToken);
        var groups = await ResolveGroupsForCardsAsync(cards, token, cancellationToken);

        if (perfSw != null)
        {
            perfSw.Stop();
            LogPerf("board_list", $"board={boardId} rows={cards.Count} total={page.Total}", perfSw.ElapsedMilliseconds);
        }

        return new QueryExecuteResponse
        {
            Dataset = OcDatasets.WorkItems,
            QueryKey = "board_list",
            Items = cards,
            Skip = skip,
            Take = take,
            Total = (int)Math.Min(page.Total, int.MaxValue),
            People = people,
            Groups = groups
        };
    }

    // Çekirdek (top-level) kart alanları; bunun dışı pool alanı kabul edilir (extraFields.<key>).
    private static readonly HashSet<string> CoreCardFieldKeys = new(StringComparer.Ordinal)
    {
        "key", "title", "stateId", "assignee", "priorityId", "typeId",
        "createdAt", "createdBy", "updatedAt", "lastStateChangeAt", "description",
        "boardId", "workspaceId", "stateFlowId", "watchers", "order",
        "firstClosedAt", "closedAt", "currentStateDurationMs"
    };

    private static string MapBoardFieldToDgPath(string fieldKey)
        => CoreCardFieldKeys.Contains(fieldKey) ? fieldKey : $"extraFields.{fieldKey}";

    /// <summary>
    /// Sıralama için sanal sistem sütunlarını gerçek alan path'ine çevirir.
    /// <c>age</c> = createdAt (yön ters: büyük yaş = eski = artan createdAt);
    /// <c>sla</c> = resolve hedef tarihi.
    /// </summary>
    private static (string Path, bool Invert) MapSortField(string fieldKey) => fieldKey switch
    {
        "age" => ("createdAt", true),
        "sla" => ("sla.resolveDueAt", false),
        _ => (MapBoardFieldToDgPath(fieldKey), false)
    };

    /// <summary>Liste filtresi → native Mongo koşulu (DG REST DSL operatörleriyle aynı sözlük).</summary>
    private static object? BuildMatchCondition(string? op, string value)
    {
        switch ((op ?? "eq").Trim().ToLowerInvariant())
        {
            case "eq": return CoerceScalar(value);
            case "ne": return new Dictionary<string, object?> { ["$ne"] = CoerceScalar(value) };
            case "gt": return new Dictionary<string, object?> { ["$gt"] = CoerceScalar(value) };
            case "gte": return new Dictionary<string, object?> { ["$gte"] = CoerceScalar(value) };
            case "lt": return new Dictionary<string, object?> { ["$lt"] = CoerceScalar(value) };
            case "lte": return new Dictionary<string, object?> { ["$lte"] = CoerceScalar(value) };
            case "in": return new Dictionary<string, object?> { ["$in"] = SplitFilterValues(value) };
            case "nin": return new Dictionary<string, object?> { ["$nin"] = SplitFilterValues(value) };
            case "contains":
                return new Dictionary<string, object?> { ["$regex"] = Regex.Escape(value), ["$options"] = "i" };
            case "startswith":
                return new Dictionary<string, object?> { ["$regex"] = "^" + Regex.Escape(value), ["$options"] = "i" };
            case "endswith":
                return new Dictionary<string, object?> { ["$regex"] = Regex.Escape(value) + "$", ["$options"] = "i" };
            default: return null;
        }
    }

    private static List<object?> SplitFilterValues(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => CoerceScalar(v))
            .ToList();

    private static object? CoerceScalar(string value)
    {
        if (long.TryParse(value, out var l)) return l;
        if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
        if (bool.TryParse(value, out var b)) return b;
        return value;
    }

    private async Task<QueryExecuteResponse> ExecuteQueryCoreAsync(
        string queryKey,
        string dataset,
        IReadOnlyDictionary<string, object?> rawParams,
        int skip,
        int take,
        string token,
        QueryResolveContext resolveContext,
        CancellationToken cancellationToken)
    {
        var takeClamped = Math.Clamp(take, 1, 200);
        var skipClamped = Math.Max(0, skip);

        var cards = await ExecuteQueryCardsAsync(queryKey, dataset, rawParams, token, resolveContext, cancellationToken);
        var page = cards.Skip(skipClamped).Take(takeClamped).ToList();

        var people = await ResolvePeopleForCardsAsync(page, token, cancellationToken);
        var groups = await ResolveGroupsForCardsAsync(page, token, cancellationToken);

        return new QueryExecuteResponse
        {
            Dataset = dataset,
            QueryKey = queryKey,
            Items = page,
            Skip = skipClamped,
            Take = takeClamped,
            Total = cards.Count,
            People = people,
            Groups = groups
        };
    }

    /// <summary>
    /// Named query'yi çalıştırıp <b>tam</b> kart kümesini döner (sayfalama/zenginleştirme yapmaz).
    /// İzin doğrulaması + DG sorgusu + kart eşlemesi paylaşılır; <see cref="ExecuteQueryCoreAsync"/> (sayfalı)
    /// ve dashboard chart agregasyonu (tam küme) aynı çekirdeği kullanır.
    /// </summary>
    private async Task<IReadOnlyList<WorkItemCardDto>> ExecuteQueryCardsAsync(
        string queryKey,
        string dataset,
        IReadOnlyDictionary<string, object?> rawParams,
        string token,
        QueryResolveContext resolveContext,
        CancellationToken cancellationToken)
    {
        if (!OcQueries.IsAllowed(dataset, queryKey))
        {
            throw new OperationCoreException(
                "QUERY_NOT_ALLOWED",
                $"Query '{queryKey}' is not allowed on dataset '{dataset}'.",
                $"'{queryKey}' sorgusu '{dataset}' üzerinde çalıştırılamaz.",
                400);
        }

        var mergedParams = new Dictionary<string, object?>(rawParams, StringComparer.OrdinalIgnoreCase);
        var resolved = QueryParameterResolver.Resolve(mergedParams, resolveContext);
        var workspaceId = resolved.TryGetValue("workspaceId", out var ws) ? ws?.ToString() : resolveContext.WorkspaceId;
        var boardId = resolved.TryGetValue("boardId", out var b) ? b?.ToString() : resolveContext.BoardId;

        if (!string.IsNullOrEmpty(workspaceId))
        {
            var workspace = await _metadataCache.GetWorkspaceAsync(workspaceId, token, cancellationToken);
            _permissions.EnsureWorkspace(workspace, WorkspaceAction.View);

            if (!string.IsNullOrEmpty(boardId))
            {
                var board = await _metadataCache.GetBoardAsync(boardId, token, cancellationToken);
                _permissions.EnsureBoardView(workspace, board);
            }
        }

        var rows = await _dg.ExecuteQueryAsync(dataset, queryKey, resolved, token, cancellationToken);
        return rows.Select(MapWorkItemCard).ToList();
    }

    /// <summary>
    /// Sayfadaki kartların person alanlarından (assignee/watchers + person tipi pool alanlar) id'leri toplar
    /// ve Keeper cache'inden id → görünen ad map'ini döner.
    /// </summary>
    private static Dictionary<string, object?> ParseQueryParameters(Dictionary<string, JsonElement>? parameters)
    {
        var rawParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (parameters == null)
            return rawParams;

        foreach (var (key, value) in parameters)
        {
            rawParams[key] = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.TryGetInt64(out var l) ? l : value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => JsonSerializer.Deserialize<object?>(value.GetRawText())
            };
        }

        return rawParams;
    }

    private static WorkItemCardDto MapWorkItemCard(IReadOnlyDictionary<string, object?> row) =>
        new()
        {
            Id = WorkItemDataHelper.GetDataId(row),
            Key = WorkItemDataHelper.GetString(row, "key") ?? string.Empty,
            Title = WorkItemDataHelper.GetString(row, "title") ?? string.Empty,
            StateId = WorkItemDataHelper.GetString(row, "stateId"),
            Assignee = WorkItemDataHelper.GetString(row, "assignee"),
            PriorityId = WorkItemDataHelper.GetString(row, "priorityId"),
            TypeId = WorkItemDataHelper.GetString(row, "typeId"),
            CreatedAt = WorkItemDataHelper.GetDateTime(row, "createdAt"),
            CreatedBy = WorkItemDataHelper.GetString(row, "createdBy"),
            UpdatedAt = WorkItemDataHelper.GetDateTime(row, "updatedAt"),
            LastStateChangeAt = WorkItemDataHelper.GetDateTime(row, "lastStateChangeAt"),
            ClosedAt = WorkItemDataHelper.GetDateTime(row, "closedAt"),
            Sla = SlaSnapshotHelper.MapFromWorkItem(row),
            Fields = GetExtraFieldsElement(row)
        };

    private static JsonElement? GetExtraFieldsElement(IReadOnlyDictionary<string, object?> row)
    {
        if (row.TryGetValue("extraFields", out var value)
            && value is JsonElement { ValueKind: JsonValueKind.Object } el)
        {
            return el;
        }

        return null;
    }

    private static IReadOnlyList<string> ParseCardFieldKeys(JsonElement? visibleFields)
    {
        if (visibleFields is not { ValueKind: JsonValueKind.Array })
            return new[] { "title", "assignee", "priorityId", "key" };

        var keys = new List<string>();
        foreach (var item in visibleFields.Value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    keys.Add(s);
            }
            else if (item.ValueKind == JsonValueKind.Object
                     && item.TryGetProperty("name", out var nameProp)
                     && nameProp.ValueKind == JsonValueKind.String)
            {
                var s = nameProp.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    keys.Add(s);
            }
        }

        return keys.Count > 0 ? keys : new[] { "title", "assignee", "priorityId", "key" };
    }

    private async Task<Dictionary<string, object?>> LoadWorkItemAsync(
        string workItemId,
        string token,
        CancellationToken cancellationToken)
    {
        // expand=false: çekirdek relation alanları (labels→op_tags vb.) MO'da çözülür; DG'nin
        // eski op_labels hedefine expand edip op_tags id'lerini düşürmesini engeller (readonly profilde "—").
        var item = await _dg.GetByIdAsync<Dictionary<string, object?>>(OcDatasets.WorkItems, workItemId, token, cancellationToken, expand: false);
        if (item == null)
        {
            throw new OperationCoreException(
                "WORK_ITEM_NOT_FOUND",
                $"Work item '{workItemId}' not found.",
                $"İş kaydı '{workItemId}' bulunamadı.",
                404);
        }

        return item;
    }

    private string RequireToken()
    {
        if (string.IsNullOrEmpty(_requestContext.BearerToken))
        {
            throw new OperationCoreException(
                "UNAUTHORIZED",
                "Bearer token is required.",
                "Bearer token gerekli.",
                401);
        }

        return _requestContext.BearerToken;
    }
}
