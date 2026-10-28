using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.WorkspacePacks;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.WorkspacePacks;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed class WorkspacePackInstallService : IWorkspacePackInstallService
{
    private const string TaskCategory = "task";

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _requestContext;
    private readonly IMetadataCache _cache;
    private readonly ILogger<WorkspacePackInstallService> _logger;

    public WorkspacePackInstallService(
        IMngDataGatewayClient dg,
        IRequestContext requestContext,
        IMetadataCache cache,
        ILogger<WorkspacePackInstallService> logger)
    {
        _dg = dg;
        _requestContext = requestContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<WorkspacePackInstallDto?> GetAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var workspace = await LoadWorkspaceAsync(workspaceId, token, cancellationToken);
        var install = ReadInstall(workspace);
        if (install is null) return null;
        return ToDto(RequireId(workspaceId), install, "current", [], [], []);
    }

    public async Task<WorkspacePackInstallDto> InstallAsync(
        string workspaceId,
        InstallWorkspacePackRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureManager();
        var token = RequireToken();
        var wsId = RequireId(workspaceId);
        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            throw new OperationCoreException(
                "PACK_CODE_REQUIRED",
                "Pack code is required.",
                "Paket kodu zorunludur.",
                400);
        }

        var pack = WorkspacePackCatalog.Find(code);
        if (pack is null || !pack.CanApply)
        {
            throw new OperationCoreException(
                "PACK_NOT_FOUND",
                $"Workspace pack '{code}' was not found.",
                $"Paket bulunamadı: {code}.",
                404);
        }

        if (pack.LibraryFolders.Count > 0)
        {
            throw new OperationCoreException(
                "PACK_LIBRARY_NOT_READY",
                "This pack declares library folders, which this install step does not create.",
                "Bu paketin kütüphane klasörleri bu kurulum adımında açılmaz.",
                400);
        }

        var workspace = await LoadWorkspaceAsync(wsId, token, cancellationToken);
        var existing = ReadInstall(workspace);
        var catalogVersion = WorkspacePackCatalog.NormalizeVersion(pack.Version);
        var replaceUntouched = false;
        string action;

        if (existing is null)
        {
            action = "installed";
        }
        else if (!string.Equals(existing.Code, pack.Code, StringComparison.OrdinalIgnoreCase))
        {
            throw new OperationCoreException(
                "PACK_ALREADY_INSTALLED",
                $"Workspace already has pack '{existing.Code}'.",
                $"Bu workspace'te zaten '{existing.Code}' paketi kurulu. İkinci kurulum kaydı açılmaz.",
                409);
        }
        else
        {
            var compare = CompareVersions(catalogVersion, existing.Version);
            if (compare == 0)
            {
                action = "skipped";
            }
            else if (compare > 0)
            {
                if (!request.Upgrade)
                {
                    throw new OperationCoreException(
                        "PACK_UPGRADE_REQUIRED",
                        $"Installed version is {existing.Version}. Catalog version {catalogVersion} needs an explicit upgrade.",
                        $"Kurulu sürüm {existing.Version}. Katalog sürümü {catalogVersion} yalnızca açık yükseltmeyle uygulanır.",
                        409);
                }

                replaceUntouched = true;
                action = "upgraded";
            }
            else
            {
                throw new OperationCoreException(
                    "PACK_VERSION_OLDER",
                    $"Catalog version {catalogVersion} is older than installed {existing.Version}.",
                    $"Katalog sürümü {catalogVersion}, kurulu sürüm {existing.Version} değerinden eski.",
                    409);
            }
        }

        var workspaceKey = ReadString(workspace, "key") ?? wsId;
        var boardNames = NormalizeBoardNames(request.BoardNames);
        var created = new List<string>();
        var skipped = new List<string>();
        var left = new List<string>();
        var pieces = new Dictionary<string, string>(existing?.Pieces ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);

        var stateIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in pack.States)
        {
            var (id, pieceAction) = await EnsureStateAsync(state, token, cancellationToken);
            stateIds[state.Key] = id;
            pieces[state.Key] = id;
            Note(state.Key, pieceAction, created, skipped, left);
        }

        var idToKey = stateIds.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

        foreach (var flow in pack.Flows)
        {
            var (id, pieceAction) = await EnsureScopedAsync(
                OcDatasets.StateFlows,
                wsId,
                workspaceKey,
                flow.Key,
                pack.Code,
                FlowStamp(flow),
                () => FlowPayload(wsId, pack.Code, flow, stateIds),
                row => FlowStampFromRow(row, idToKey),
                replaceUntouched,
                token,
                cancellationToken);
            pieces[flow.Key] = id;
            Note(flow.Key, pieceAction, created, skipped, left);
        }

        var defaultFlow = pack.Flows.First(f => f.IsDefault);
        var flowId = pieces[defaultFlow.Key];

        var typeIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in pack.Types)
        {
            var (id, pieceAction) = await EnsureScopedAsync(
                OcDatasets.WorkItemTypes,
                wsId,
                workspaceKey,
                type.Key,
                pack.Code,
                TypeStamp(type),
                () => TypePayload(wsId, pack.Code, type, flowId),
                TypeStampFromRow,
                replaceUntouched,
                token,
                cancellationToken);
            typeIds[type.Key] = id;
            pieces[type.Key] = id;
            Note(type.Key, pieceAction, created, skipped, left);
        }

        foreach (var form in pack.Forms)
        {
            var (id, pieceAction) = await EnsureScopedAsync(
                OcDatasets.Forms,
                wsId,
                workspaceKey,
                form.Key,
                pack.Code,
                FormStamp(form),
                () => FormPayload(wsId, pack.Code, form, flowId, typeIds),
                row => FormStampFromRow(row, idToKey, Reverse(typeIds), pieces, defaultFlow.Key),
                replaceUntouched,
                token,
                cancellationToken);
            pieces[form.Key] = id;
            Note(form.Key, pieceAction, created, skipped, left);
        }

        foreach (var profile in pack.Profiles)
        {
            var (id, pieceAction) = await EnsureScopedAsync(
                OcDatasets.Profiles,
                wsId,
                workspaceKey,
                profile.Key,
                pack.Code,
                ProfileStamp(profile),
                () => ProfilePayload(wsId, pack.Code, profile, flowId, typeIds),
                row => ProfileStampFromRow(row, Reverse(typeIds), pieces, defaultFlow.Key),
                replaceUntouched,
                token,
                cancellationToken);
            pieces[profile.Key] = id;
            Note(profile.Key, pieceAction, created, skipped, left);
        }

        foreach (var board in pack.Boards)
        {
            var displayName = BoardDisplayName(board, boardNames);
            var (id, pieceAction) = await EnsureScopedAsync(
                OcDatasets.Boards,
                wsId,
                workspaceKey,
                board.Key,
                pack.Code,
                BoardStamp(board),
                () => BoardPayload(wsId, pack, board, displayName, flowId, typeIds, pieces, stateIds),
                row => BoardStampFromRow(row, idToKey, Reverse(typeIds), pieces, defaultFlow.Key),
                replaceUntouched,
                token,
                cancellationToken);
            pieces[board.Key] = id;
            Note(board.Key, pieceAction, created, skipped, left);
        }

        var poolFieldIds = new List<string>();
        foreach (var field in pack.PoolFields)
        {
            var fieldId = await EnsurePoolFieldAsync(wsId, field, token, cancellationToken);
            poolFieldIds.Add(fieldId);
            pieces[$"field.{field.Key}"] = fieldId;
        }

        if (existing is not null && string.Equals(action, "skipped", StringComparison.Ordinal) && created.Count > 0)
            action = "completed";

        var install = new Dictionary<string, object?>
        {
            ["code"] = pack.Code,
            ["version"] = catalogVersion,
            ["installedAt"] = DateTime.UtcNow.ToString("O"),
            ["pieces"] = pieces
        };

        var workspaceUpdate = new Dictionary<string, object?>
        {
            ["packInstall"] = install,
            ["enabledTypeIds"] = Union(ReadIds(workspace, "enabledTypeIds"), typeIds.Values),
            ["enabledStateIds"] = Union(ReadIds(workspace, "enabledStateIds"), stateIds.Values),
            ["enabledFieldIds"] = Union(ReadIds(workspace, "enabledFieldIds"), poolFieldIds)
        };
        if (string.Equals(pack.Code, "software", StringComparison.OrdinalIgnoreCase))
            workspaceUpdate["settings"] = EnsureSoftwareViews(workspace);
        if (string.IsNullOrWhiteSpace(ReadString(workspace, "defaultStateFlowId")))
            workspaceUpdate["defaultStateFlowId"] = flowId;

        await _dg.UpdateAsync(OcDatasets.Workspaces, wsId, workspaceUpdate, token, cancellationToken);

        _cache.InvalidateCatalog(OcDatasets.States);
        _cache.InvalidateCatalog(OcDatasets.WorkItemTypes);
        _cache.InvalidateCatalog(OcDatasets.Fields);
        try
        {
            await _cache.ReloadWorkspaceAsync(wsId, token, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Workspace pack installed but metadata cache reload failed for {WorkspaceId}", wsId);
        }

        _logger.LogInformation(
            "Workspace pack {Code} {Version} {Action} on {WorkspaceId}. Created {Created}, skipped {Skipped}, left {Left}",
            pack.Code, catalogVersion, action, wsId, created.Count, skipped.Count, left.Count);

        return ToDto(wsId, new InstallSnapshot(pack.Code, catalogVersion, pieces), action, created, skipped, left);
    }

    private async Task<(string Id, PieceAction Action)> EnsureStateAsync(
        WorkspacePackState state,
        string token,
        CancellationToken cancellationToken)
    {
        var page = await _dg.QueryPageAsync(
            OcDatasets.States,
            new Dictionary<string, object?> { ["name"] = state.Name },
            "limit=5&expand=false",
            token,
            cancellationToken);
        var existing = page.Items.FirstOrDefault();
        var existingId = existing is null ? null : ReadId(existing);
        var color = NormalizeThemeColor(state.Color);
        if (!string.IsNullOrWhiteSpace(existingId))
        {
            if (color is not null && string.IsNullOrWhiteSpace(ReadString(existing!, "color")))
            {
                await _dg.UpdateAsync(
                    OcDatasets.States,
                    existingId,
                    new Dictionary<string, object?> { ["color"] = color },
                    token,
                    cancellationToken);
                return (existingId, PieceAction.Updated);
            }

            return (existingId, PieceAction.Skipped);
        }

        var payload = new Dictionary<string, object?>
        {
            ["name"] = state.Name,
            ["category"] = state.Category,
            ["isInitial"] = state.IsInitial,
            ["isStart"] = state.IsInitial,
            ["isClosed"] = string.Equals(state.Category, "closed", StringComparison.OrdinalIgnoreCase)
        };
        if (color is not null)
            payload["color"] = color;
        var createdId = await CreateAsync(OcDatasets.States, payload, token, cancellationToken);
        return (createdId, PieceAction.Created);
    }

    private async Task<(string Id, PieceAction Action)> EnsureScopedAsync(
        string dataset,
        string workspaceId,
        string workspaceKey,
        string packKey,
        string packCode,
        string stamp,
        Func<Dictionary<string, object?>> payloadFactory,
        Func<Dictionary<string, object?>, string?> stampFromRow,
        bool replaceUntouched,
        string token,
        CancellationToken cancellationToken)
    {
        var rows = await LoadWorkspaceRowsAsync(dataset, workspaceId, token, cancellationToken);
        var current = rows.FirstOrDefault(row =>
            string.Equals(ReadString(row, "packKey"), packKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ReadString(row, "packCode"), packCode, StringComparison.OrdinalIgnoreCase));

        if (current is not null)
        {
            var id = ReadId(current) ?? throw Storage("Kayıt kimliği okunamadı.");
            if (!replaceUntouched)
                return (id, PieceAction.Skipped);

            var currentStamp = ReadString(current, "packStamp");
            var rebuilt = stampFromRow(current);
            if (!string.IsNullOrWhiteSpace(currentStamp)
                && string.Equals(currentStamp, rebuilt, StringComparison.Ordinal))
            {
                var update = payloadFactory();
                update["packStamp"] = stamp;
                await _dg.UpdateAsync(dataset, id, update, token, cancellationToken);
                return (id, PieceAction.Updated);
            }

            return (id, PieceAction.Left);
        }

        var payload = payloadFactory();
        payload["packStamp"] = stamp;
        try
        {
            var id = await CreateAsync(dataset, payload, token, cancellationToken);
            return (id, PieceAction.Created);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
        {
            rows = await LoadWorkspaceRowsAsync(dataset, workspaceId, token, cancellationToken);
            current = rows.FirstOrDefault(row =>
                string.Equals(ReadString(row, "packKey"), packKey, StringComparison.OrdinalIgnoreCase));
            var adopted = current is null ? null : ReadId(current);
            if (!string.IsNullOrWhiteSpace(adopted))
                return (adopted, PieceAction.Skipped);

            var name = ReadString(payload, "name");
            var named = rows.FirstOrDefault(row => string.Equals(ReadString(row, "name"), name, StringComparison.OrdinalIgnoreCase));
            if (named is not null)
            {
                throw new OperationCoreException(
                    "PACK_NAME_CONFLICT",
                    $"'{name}' already exists in this workspace and is not part of pack '{packCode}'.",
                    $"'{name}' bu workspace'te var ve '{packCode}' paketine ait değil. Kurulum bu kaydı değiştirmez.",
                    409);
            }

            if (ex.Message.Contains("VALIDATION_UNIQUE_CONSTRAINT", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(name)
                && name.IndexOf(workspaceKey, StringComparison.OrdinalIgnoreCase) < 0)
            {
                payload["name"] = $"{name} · {workspaceKey}";
                var id = await CreateAsync(dataset, payload, token, cancellationToken);
                return (id, PieceAction.Created);
            }

            throw Storage(ex.Message);
        }
    }

    private static Dictionary<string, object?> FlowPayload(
        string workspaceId,
        string packCode,
        WorkspacePackFlow flow,
        IReadOnlyDictionary<string, string> stateIds)
    {
        var transitions = new List<Dictionary<string, object?>>();
        for (var i = 0; i < flow.Transitions.Count; i++)
        {
            var transition = flow.Transitions[i];
            transitions.Add(new Dictionary<string, object?>
            {
                ["transitionKey"] = transition.Key,
                ["fromStateId"] = stateIds[transition.FromStateKey],
                ["toStateId"] = stateIds[transition.ToStateKey],
                ["label"] = transition.Label,
                ["order"] = i,
                ["requiredFields"] = transition.RequiredFields
                    .Where(field => !string.IsNullOrWhiteSpace(field))
                    .Select(field => field.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                ["requireComment"] = transition.RequireComment,
                ["permissions"] = new Dictionary<string, object?> { ["groups"] = Array.Empty<string>() }
            });
        }

        return new Dictionary<string, object?>
        {
            ["name"] = flow.Name,
            ["workspaceId"] = workspaceId,
            ["initialStateId"] = stateIds[flow.InitialStateKey],
            ["isDefault"] = flow.IsDefault,
            ["isActive"] = true,
            ["transitions"] = transitions,
            ["packCode"] = packCode,
            ["packKey"] = flow.Key
        };
    }

    private static Dictionary<string, object?> TypePayload(
        string workspaceId,
        string packCode,
        WorkspacePackType type,
        string flowId) => new()
    {
        ["name"] = type.Name,
        ["category"] = TaskCategory,
        ["workspaceId"] = workspaceId,
        ["defaultStateFlowId"] = flowId,
        ["requiresParent"] = type.RequiresParent,
        ["childTypeKeys"] = type.ChildTypeKeys,
        ["packCode"] = packCode,
        ["packKey"] = type.Key,
        ["packFlowKey"] = type.FlowKey
    };

    private static Dictionary<string, object?> FormPayload(
        string workspaceId,
        string packCode,
        WorkspacePackForm form,
        string flowId,
        IReadOnlyDictionary<string, string> typeIds)
    {
        var behaviors = new Dictionary<string, object?>();
        foreach (var field in form.Fields)
        {
            var required = field is "title" or "typeId";
            behaviors[field] = new Dictionary<string, object?> { ["visible"] = true, ["required"] = required };
        }

        return new Dictionary<string, object?>
        {
            ["name"] = form.Name,
            ["workspaceId"] = workspaceId,
            ["defaultTypeId"] = typeIds[form.TypeKey],
            ["defaultStateFlowId"] = flowId,
            ["isDefault"] = form.IsDefault,
            ["layout"] = new Dictionary<string, object?>
            {
                ["sections"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["key"] = "main",
                        ["title"] = "Temel bilgiler",
                        ["fields"] = form.Fields
                    }
                }
            },
            ["fieldBehaviors"] = behaviors,
            ["packCode"] = packCode,
            ["packKey"] = form.Key
        };
    }

    private static Dictionary<string, object?> ProfilePayload(
        string workspaceId,
        string packCode,
        WorkspacePackProfile profile,
        string flowId,
        IReadOnlyDictionary<string, string> typeIds)
    {
        var fieldKeys = ProfileFieldKeys(profile);
        var behaviors = new Dictionary<string, object?>();
        foreach (var field in fieldKeys)
        {
            behaviors[field] = new Dictionary<string, object?>
            {
                ["visible"] = true,
                ["required"] = field == "title",
                ["readonly"] = field is "typeId" or "key"
            };
        }

        var actions = new List<Dictionary<string, object?>>();
        for (var i = 0; i < profile.ActionKeys.Count; i++)
        {
            actions.Add(new Dictionary<string, object?>
            {
                ["transitionKey"] = profile.ActionKeys[i],
                ["order"] = i
            });
        }

        return new Dictionary<string, object?>
        {
            ["name"] = profile.Name,
            ["workspaceId"] = workspaceId,
            ["defaultTypeId"] = typeIds[profile.TypeKey],
            ["defaultStateFlowId"] = flowId,
            ["isDefault"] = profile.IsDefault,
            ["fieldBehaviors"] = behaviors,
            ["actions"] = actions,
            ["header"] = new Dictionary<string, object?> { ["showBreadcrumb"] = true, ["showKey"] = true },
            ["layout"] = new Dictionary<string, object?>
            {
                ["sections"] = ProfileSections(profile)
            },
            ["packCode"] = packCode,
            ["packKey"] = profile.Key
        };
    }

    /// <summary>
    /// Yazılım paketinde sprint, takvim ve Gantt açık başlar. Kullanıcı kapattıysa dokunulmaz.
    /// </summary>
    private static Dictionary<string, object?> EnsureSoftwareViews(Dictionary<string, object?> workspace)
    {
        JsonObject settings = new();
        if (workspace.TryGetValue("settings", out var raw))
        {
            if (raw is JsonElement element && element.ValueKind == JsonValueKind.Object)
                settings = JsonNode.Parse(element.GetRawText()) as JsonObject ?? new JsonObject();
            else if (raw is string text && text.TrimStart().StartsWith('{'))
                settings = JsonNode.Parse(text) as JsonObject ?? new JsonObject();
        }

        var views = settings["views"] as JsonObject ?? new JsonObject();
        settings["views"] = views;
        foreach (var key in new[] { "sprints", "calendar", "gantt" })
        {
            if (views[key] is null)
                views[key] = true;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(settings.ToJsonString())
            ?? new Dictionary<string, object?>();
    }

    private static Dictionary<string, string> NormalizeBoardNames(IReadOnlyDictionary<string, string>? names)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (names is null) return normalized;
        foreach (var pair in names)
        {
            var key = pair.Key?.Trim();
            if (!string.IsNullOrWhiteSpace(key))
                normalized[key] = pair.Value ?? string.Empty;
        }

        return normalized;
    }

    /// <summary>
    /// A custom name is stored on the row. The stamp stays on the catalog name, so an upgrade leaves a renamed board in place.
    /// </summary>
    private static string BoardDisplayName(WorkspacePackBoard board, IReadOnlyDictionary<string, string> names)
    {
        if (!names.TryGetValue(board.Key, out var custom))
            return board.Name.Trim();
        var value = custom.Trim();
        if (value.Length == 0)
            return board.Name.Trim();
        if (value.Length > 80)
        {
            throw new OperationCoreException(
                "PACK_BOARD_NAME",
                "Board name must be 80 characters or fewer.",
                "Pano adı en fazla 80 karakter olabilir.",
                400);
        }

        return value;
    }

    private static Dictionary<string, object?> BoardPayload(
        string workspaceId,
        WorkspacePackDefinition pack,
        WorkspacePackBoard board,
        string displayName,
        string flowId,
        IReadOnlyDictionary<string, string> typeIds,
        IReadOnlyDictionary<string, string> pieces,
        IReadOnlyDictionary<string, string> stateIds)
    {
        var flow = pack.Flows.First(item => string.Equals(item.Key, board.FlowKey, StringComparison.OrdinalIgnoreCase));
        var profile = pack.Profiles.First(item => item.IsDefault);
        var columns = new List<Dictionary<string, object?>>();
        foreach (var stateKey in board.ColumnStateKeys)
        {
            var outgoing = flow.Transitions
                .Where(item => string.Equals(item.FromStateKey, stateKey, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Key)
                .ToList();
            var state = pack.States.First(item => string.Equals(item.Key, stateKey, StringComparison.OrdinalIgnoreCase));
            columns.Add(new Dictionary<string, object?>
            {
                ["stateId"] = stateIds[stateKey],
                ["title"] = state.Name,
                ["queryKey"] = "wi_board_column",
                ["defaultTransitionKey"] = outgoing.Count == 1 ? outgoing[0] : null
            });
        }

        return new Dictionary<string, object?>
        {
            ["name"] = displayName,
            ["workspaceId"] = workspaceId,
            ["viewType"] = "kanban",
            ["supportedViewTypes"] = new[] { "kanban", "list" },
            ["defaultViewType"] = "kanban",
            ["isDefault"] = board.IsDefault,
            ["defaultStateFlowId"] = flowId,
            ["defaultFormId"] = pieces[board.FormKey],
            ["defaultProfileId"] = pieces[profile.Key],
            ["defaultTypeId"] = typeIds[board.TypeKey],
            ["defaultStateId"] = stateIds[flow.InitialStateKey],
            ["visibleFields"] = new[] { "key", "title", "stateId", "assignee", "priorityId", "storyPoints" },
            ["config"] = new Dictionary<string, object?>
            {
                ["columns"] = columns,
                ["listColumns"] = new object[]
                {
                    ListCol("key", "No"),
                    ListCol("title", "Başlık"),
                    ListCol("stateId", "Durum"),
                    ListCol("sprintId", "Sprint", filterable: false),
                    ListCol("assignee", "Atanan"),
                    ListCol("priorityId", "Öncelik"),
                    ListCol("startDate", "Başlangıç", filterable: false, sortable: false, format: "date"),
                    ListCol("dueDate", "Vade", filterable: false, format: "date"),
                    ListCol("storyPoints", "İş puanı")
                }
            },
            ["packCode"] = pack.Code,
            ["packKey"] = board.Key
        };
    }

    private static Dictionary<string, object?> ListCol(
        string key,
        string label,
        bool? filterable = null,
        bool? sortable = null,
        string? format = null)
    {
        var column = new Dictionary<string, object?>
        {
            ["key"] = key,
            ["label"] = label,
            ["sortable"] = sortable ?? true,
            ["filterable"] = filterable ?? key != "assignee"
        };
        if (!string.IsNullOrWhiteSpace(format))
            column["format"] = format;
        return column;
    }

    private async Task<string> EnsurePoolFieldAsync(
        string workspaceId,
        WorkspacePackPoolField field,
        string token,
        CancellationToken cancellationToken)
    {
        var key = field.Key.Trim();
        var label = field.Label.Trim();
        var fieldType = string.IsNullOrWhiteSpace(field.FieldType) ? "text" : field.FieldType.Trim();
        var page = await _dg.QueryPageAsync(
            OcDatasets.Fields,
            new Dictionary<string, object?> { ["key"] = key, ["workspaceId"] = workspaceId },
            "limit=20&expand=false",
            token,
            cancellationToken);
        var current = page.Items.FirstOrDefault(row =>
            string.Equals(ReadString(row, "key"), key, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ReadString(row, "workspaceId"), workspaceId, StringComparison.Ordinal));
        if (current is not null)
        {
            var id = ReadId(current) ?? throw Storage("Alan kimliği okunamadı.");
            if (!string.Equals(ReadString(current, "label"), label, StringComparison.Ordinal)
                || !string.Equals(ReadString(current, "fieldType"), fieldType, StringComparison.OrdinalIgnoreCase))
            {
                await _dg.UpdateAsync(
                    OcDatasets.Fields,
                    id,
                    new Dictionary<string, object?> { ["label"] = label, ["fieldType"] = fieldType },
                    token,
                    cancellationToken);
            }

            return id;
        }

        return await CreateAsync(
            OcDatasets.Fields,
            new Dictionary<string, object?>
            {
                ["key"] = key,
                ["label"] = label,
                ["fieldType"] = fieldType,
                ["scope"] = "pool",
                ["workspaceId"] = workspaceId,
                ["isSystem"] = false,
                ["isSensitive"] = false
            },
            token,
            cancellationToken);
    }

    private async Task<List<Dictionary<string, object?>>> LoadWorkspaceRowsAsync(
        string dataset,
        string workspaceId,
        string token,
        CancellationToken cancellationToken)
    {
        var page = await _dg.QueryPageAsync(
            dataset,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId },
            "limit=200&expand=false",
            token,
            cancellationToken);
        return page.Items.ToList();
    }

    private async Task<Dictionary<string, object?>> LoadWorkspaceAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken)
    {
        var row = await _dg.GetByIdAsync<Dictionary<string, object?>>(
            OcDatasets.Workspaces,
            RequireId(workspaceId),
            token,
            cancellationToken,
            expand: false);
        if (row is null || string.IsNullOrWhiteSpace(ReadId(row)))
        {
            throw new OperationCoreException(
                "WORKSPACE_NOT_FOUND",
                "Workspace was not found.",
                "Workspace bulunamadı.",
                404);
        }

        return row;
    }

    private async Task<string> CreateAsync(
        string dataset,
        Dictionary<string, object?> payload,
        string token,
        CancellationToken cancellationToken)
    {
        var created = await _dg.CreateAsync(dataset, payload, token, cancellationToken);
        var id = ReadId(created);
        if (!string.IsNullOrWhiteSpace(id))
            return id;
        throw Storage($"{dataset} kaydı oluşturulamadı.");
    }

    private void EnsureManager()
    {
        if (_requestContext.IsAdmin || _requestContext.IsManager)
            return;

        throw new OperationCoreException(
            "PACK_FORBIDDEN",
            "Only domain managers can install a workspace pack.",
            "Paket kurulumu için manager yetkisi gerekli.",
            403);
    }

    private string RequireToken()
    {
        var token = _requestContext.BearerToken?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new OperationCoreException(
                "TOKEN_REQUIRED",
                "Bearer token is required.",
                "Oturum gerekli.",
                401);
        }

        return token;
    }

    private static void Note(string key, PieceAction action, List<string> created, List<string> skipped, List<string> left)
    {
        switch (action)
        {
            case PieceAction.Created:
            case PieceAction.Updated:
                created.Add(key);
                break;
            case PieceAction.Left:
                left.Add(key);
                break;
            default:
                skipped.Add(key);
                break;
        }
    }

    private static WorkspacePackInstallDto ToDto(
        string workspaceId,
        InstallSnapshot install,
        string action,
        List<string> created,
        List<string> skipped,
        List<string> left) => new()
    {
        WorkspaceId = workspaceId,
        Code = install.Code,
        Version = install.Version,
        Action = action,
        Created = created,
        Skipped = skipped,
        Left = left,
        Pieces = new Dictionary<string, string>(install.Pieces, StringComparer.OrdinalIgnoreCase)
    };

    private static InstallSnapshot? ReadInstall(Dictionary<string, object?> workspace)
    {
        if (!TryGet(workspace, "packInstall", out var value) || value is null)
            return null;
        if (value is not JsonElement el || el.ValueKind != JsonValueKind.Object)
            return null;
        var code = ElementString(el, "code");
        if (string.IsNullOrWhiteSpace(code))
            return null;
        var pieces = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (el.TryGetProperty("pieces", out var raw) && raw.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in raw.EnumerateObject())
            {
                var id = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                if (!string.IsNullOrWhiteSpace(id))
                    pieces[property.Name] = id;
            }
        }

        return new InstallSnapshot(code, WorkspacePackCatalog.NormalizeVersion(ElementString(el, "version")), pieces);
    }

    private static string FlowStamp(WorkspacePackFlow flow)
    {
        var lines = new List<string> { "flow", flow.Name.Trim(), flow.InitialStateKey.Trim() };
        lines.AddRange(flow.Transitions.Select(t => $"{t.Key}|{t.FromStateKey}|{t.ToStateKey}|{t.Label}"));
        return Hash(lines);
    }

    private static string? FlowStampFromRow(Dictionary<string, object?> row, IReadOnlyDictionary<string, string> idToKey)
    {
        var name = ReadString(row, "name");
        var initialId = ReadString(row, "initialStateId");
        if (name is null || initialId is null || !idToKey.TryGetValue(initialId, out var initialKey))
            return null;
        if (!TryGet(row, "transitions", out var raw) || raw is not JsonElement transitions || transitions.ValueKind != JsonValueKind.Array)
            return null;
        var lines = new List<string> { "flow", name, initialKey };
        foreach (var item in transitions.EnumerateArray())
        {
            var key = ElementString(item, "transitionKey");
            var fromId = ElementString(item, "fromStateId");
            var toId = ElementString(item, "toStateId");
            var label = ElementString(item, "label") ?? string.Empty;
            if (key is null || fromId is null || toId is null
                || !idToKey.TryGetValue(fromId, out var fromKey)
                || !idToKey.TryGetValue(toId, out var toKey))
                return null;
            lines.Add($"{key}|{fromKey}|{toKey}|{label}");
        }

        return Hash(lines);
    }

    private static string TypeStamp(WorkspacePackType type)
    {
        var children = string.Join(",", type.ChildTypeKeys);
        return Hash(["type", type.Name.Trim(), type.FlowKey.Trim(), type.RequiresParent ? "1" : "0", children]);
    }

    private static string? TypeStampFromRow(Dictionary<string, object?> row)
    {
        var name = ReadString(row, "name");
        if (name is null) return null;
        var requires = ReadBool(row, "requiresParent") ? "1" : "0";
        var children = string.Join(",", ReadStrings(row, "childTypeKeys"));
        var flowKey = ReadString(row, "packFlowKey");
        if (flowKey is null) return null;
        return Hash(["type", name, flowKey, requires, children]);
    }

    private static string FormStamp(WorkspacePackForm form) =>
        Hash(["form", form.Name.Trim(), form.FlowKey.Trim(), form.TypeKey.Trim(), string.Join(",", form.Fields)]);

    private static string? FormStampFromRow(
        Dictionary<string, object?> row,
        IReadOnlyDictionary<string, string> stateIdsUnused,
        IReadOnlyDictionary<string, string> typeIdToKey,
        IReadOnlyDictionary<string, string> pieces,
        string flowKey)
    {
        _ = stateIdsUnused;
        _ = pieces;
        var name = ReadString(row, "name");
        var typeId = ReadString(row, "defaultTypeId");
        if (name is null || typeId is null || !typeIdToKey.TryGetValue(typeId, out var typeKey))
            return null;
        var fields = ReadLayoutFields(row);
        if (fields is null) return null;
        return Hash(["form", name, flowKey, typeKey, string.Join(",", fields)]);
    }

    private static string BoardStamp(WorkspacePackBoard board) =>
        Hash(["board", board.Name.Trim(), board.FlowKey.Trim(), board.FormKey.Trim(), board.TypeKey.Trim(), string.Join(",", board.ColumnStateKeys)]);

    private static string? BoardStampFromRow(
        Dictionary<string, object?> row,
        IReadOnlyDictionary<string, string> stateIdToKey,
        IReadOnlyDictionary<string, string> typeIdToKey,
        IReadOnlyDictionary<string, string> pieces,
        string flowKey)
    {
        var name = ReadString(row, "name");
        var typeId = ReadString(row, "defaultTypeId");
        var formId = ReadString(row, "defaultFormId");
        if (name is null || typeId is null || formId is null || !typeIdToKey.TryGetValue(typeId, out var typeKey))
            return null;
        var formKey = pieces.FirstOrDefault(pair => string.Equals(pair.Value, formId, StringComparison.Ordinal)).Key;
        if (string.IsNullOrWhiteSpace(formKey)) return null;
        if (!TryGet(row, "config", out var configRaw) || configRaw is not JsonElement config || config.ValueKind != JsonValueKind.Object)
            return null;
        if (!config.TryGetProperty("columns", out var columns) || columns.ValueKind != JsonValueKind.Array)
            return null;
        var keys = new List<string>();
        foreach (var column in columns.EnumerateArray())
        {
            var stateId = ElementString(column, "stateId");
            if (stateId is null || !stateIdToKey.TryGetValue(stateId, out var stateKey))
                return null;
            keys.Add(stateKey);
        }

        return Hash(["board", name, flowKey, formKey, typeKey, string.Join(",", keys)]);
    }

    private static List<string> ProfileFieldKeys(WorkspacePackProfile profile)
    {
        if (profile.Sections.Count == 0)
            return profile.Fields;
        return profile.Sections.SelectMany(section => section.Fields).ToList();
    }

    private static List<Dictionary<string, object?>> ProfileSections(WorkspacePackProfile profile)
    {
        if (profile.Sections.Count == 0)
        {
            return
            [
                new Dictionary<string, object?>
                {
                    ["key"] = "summary",
                    ["title"] = "Özet",
                    ["fields"] = profile.Fields
                }
            ];
        }

        return profile.Sections.Select(section => new Dictionary<string, object?>
        {
            ["key"] = section.Key.Trim(),
            ["title"] = section.Title.Trim(),
            ["cols"] = 12,
            ["fields"] = section.Fields
        }).ToList();
    }

    private static string ProfileStamp(WorkspacePackProfile profile) =>
        Hash(["profile", profile.Name.Trim(), profile.FlowKey.Trim(), profile.TypeKey.Trim(), string.Join(",", ProfileFieldKeys(profile)), string.Join(",", profile.ActionKeys)]);

    private static string? ProfileStampFromRow(
        Dictionary<string, object?> row,
        IReadOnlyDictionary<string, string> typeIdToKey,
        IReadOnlyDictionary<string, string> pieces,
        string flowKey)
    {
        _ = pieces;
        var name = ReadString(row, "name");
        var typeId = ReadString(row, "defaultTypeId");
        if (name is null || typeId is null || !typeIdToKey.TryGetValue(typeId, out var typeKey))
            return null;
        var fields = ReadLayoutFields(row);
        if (fields is null) return null;
        if (!TryGet(row, "actions", out var actionsRaw) || actionsRaw is not JsonElement actions || actions.ValueKind != JsonValueKind.Array)
            return null;
        var actionKeys = new List<string>();
        foreach (var action in actions.EnumerateArray())
        {
            var key = ElementString(action, "transitionKey");
            if (key is null) return null;
            actionKeys.Add(key);
        }

        return Hash(["profile", name, flowKey, typeKey, string.Join(",", fields), string.Join(",", actionKeys)]);
    }

    private static List<string>? ReadLayoutFields(Dictionary<string, object?> row)
    {
        if (!TryGet(row, "layout", out var layoutRaw) || layoutRaw is not JsonElement layout || layout.ValueKind != JsonValueKind.Object)
            return null;
        if (!layout.TryGetProperty("sections", out var sections) || sections.ValueKind != JsonValueKind.Array)
            return null;
        var fields = new List<string>();
        foreach (var section in sections.EnumerateArray())
        {
            if (section.ValueKind != JsonValueKind.Object || !section.TryGetProperty("fields", out var sectionFields) || sectionFields.ValueKind != JsonValueKind.Array)
                continue;
            fields.AddRange(sectionFields.EnumerateArray().Select(item =>
                item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty));
        }

        return fields.Count == 0 ? null : fields;
    }

    private static Dictionary<string, string> Reverse(IReadOnlyDictionary<string, string> map) =>
        map.GroupBy(pair => pair.Value, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);

    private static string Hash(IEnumerable<string> lines)
    {
        var text = string.Join("\n", lines);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    private static int CompareVersions(string left, string right)
    {
        var a = Parts(left);
        var b = Parts(right);
        var count = Math.Max(a.Length, b.Length);
        for (var i = 0; i < count; i++)
        {
            var av = i < a.Length ? a[i] : 0;
            var bv = i < b.Length ? b[i] : 0;
            if (av != bv) return av.CompareTo(bv);
        }

        return 0;
    }

    private static int[] Parts(string version) =>
        version.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var number) ? number : 0)
            .ToArray();

    private static List<string> Union(IEnumerable<string> current, IEnumerable<string> added)
    {
        var set = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in current.Concat(added))
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) continue;
            set.Add(id);
        }

        return set;
    }

    private static List<string> ReadIds(Dictionary<string, object?> row, string key)
    {
        var ids = new List<string>();
        if (!TryGet(row, key, out var value) || value is not JsonElement el || el.ValueKind != JsonValueKind.Array)
            return ids;
        foreach (var item in el.EnumerateArray())
        {
            var id = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Object when item.TryGetProperty("__dataId", out var dataId) => dataId.GetString(),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id);
        }

        return ids;
    }

    private static List<string> ReadStrings(Dictionary<string, object?> row, string key)
    {
        if (!TryGet(row, key, out var value) || value is not JsonElement el || el.ValueKind != JsonValueKind.Array)
            return [];
        return el.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => item.Length > 0)
            .ToList();
    }

    private static readonly HashSet<string> ThemeColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "primary", "secondary", "success", "info", "warning", "error"
    };

    private static string? NormalizeThemeColor(string? color)
    {
        var value = (color ?? string.Empty).Trim().ToLowerInvariant();
        return ThemeColors.Contains(value) ? value : null;
    }

    private static bool ReadBool(Dictionary<string, object?> row, string key)
    {
        if (!TryGet(row, key, out var value) || value is null) return false;
        return value switch
        {
            bool flag => flag,
            JsonElement el when el.ValueKind == JsonValueKind.True => true,
            JsonElement el when el.ValueKind == JsonValueKind.False => false,
            _ => false
        };
    }

    private static string? ReadId(IReadOnlyDictionary<string, object?> row) =>
        ReadString(row, "__dataId") ?? ReadString(row, "dataId");

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!TryGet(row, key, out var value) || value is null) return null;
        return value switch
        {
            string text => Empty(text),
            JsonElement el when el.ValueKind == JsonValueKind.String => Empty(el.GetString()),
            JsonElement el when el.ValueKind == JsonValueKind.Object && el.TryGetProperty("__dataId", out var dataId) => Empty(dataId.GetString()),
            JsonElement el when el.ValueKind == JsonValueKind.Object && el.TryGetProperty("dataId", out var alt) => Empty(alt.GetString()),
            _ => null
        };
    }

    private static string? ElementString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => Empty(value.GetString()),
            JsonValueKind.Object when value.TryGetProperty("__dataId", out var dataId) => Empty(dataId.GetString()),
            _ => null
        };
    }

    private static bool TryGet(IReadOnlyDictionary<string, object?> row, string key, out object? value)
    {
        if (row.TryGetValue(key, out value)) return true;
        var match = row.Keys.FirstOrDefault(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            value = null;
            return false;
        }

        value = row[match];
        return true;
    }

    private static string RequireId(string id)
    {
        var value = id.Trim();
        if (value.Length == 0)
        {
            throw new OperationCoreException("WORKSPACE_REQUIRED", "Workspace id is required.", "Workspace zorunludur.", 400);
        }

        return value;
    }

    private static string? Empty(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static OperationCoreException Storage(string message) =>
        new("PACK_STORAGE", message, "Paket kaydı yazılamadı.", 502);

    private sealed record InstallSnapshot(string Code, string Version, Dictionary<string, string> Pieces);

    private enum PieceAction
    {
        Created,
        Skipped,
        Updated,
        Left
    }
}
