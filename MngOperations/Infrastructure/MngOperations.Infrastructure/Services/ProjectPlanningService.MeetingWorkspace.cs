using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    private const string MeetingTemplateName = "Toplantı tutanağı";
    private const string MeetingFieldKey = "pmMeetingRef";
    private const string MeetingStateOpenName = "Toplantı Açık";
    private const string MeetingStateProgressName = "Toplantı Sürüyor";
    private const string MeetingStateDoneName = "Toplantı Bitti";
    private const string MeetingStateWaivedName = "Toplantı Vazgeçildi";

    public async Task<MeetingWorkspaceDto> GetMeetingWorkspaceAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        var project = await LoadProjectOrThrowAsync(projectId, token, ct);
        return await ToMeetingWorkspaceDtoAsync(project, token, ct);
    }

    public async Task<MeetingWorkspaceDto> SetMeetingWorkspaceAsync(
        string projectId,
        SetMeetingWorkspaceRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        var project = await LoadProjectOrThrowAsync(projectId, token, ct);
        var workspaceId = EmptyToNull(request.WorkspaceId);
        if (workspaceId is not null)
            await _metadata.GetWorkspaceAsync(workspaceId, token, ct);

        await _dg.UpdateAsync(
            PmDatasets.Projects,
            projectId,
            new Dictionary<string, object?> { ["meetingWorkspaceId"] = workspaceId },
            token,
            ct);
        project.meetingWorkspaceId = workspaceId;
        return await ToMeetingWorkspaceDtoAsync(project, token, ct);
    }

    public async Task<MeetingWorkspaceDto> EnsureMeetingWorkspaceAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        var project = await LoadProjectOrThrowAsync(projectId, token, ct);
        var name = MeetingWorkspaceName(project.code);
        var existingId = await FindFirstIdAsync(
            OcDatasets.Workspaces,
            new Dictionary<string, object?> { ["name"] = name },
            token,
            ct);

        string workspaceId;
        if (!string.IsNullOrWhiteSpace(existingId))
        {
            workspaceId = existingId;
        }
        else
        {
            workspaceId = await CreateNamedAsync(
                OcDatasets.Workspaces,
                new Dictionary<string, object?> { ["name"] = name },
                new Dictionary<string, object?>
                {
                    ["name"] = name,
                    ["workspaceType"] = "project",
                    ["description"] = "Toplantı tutanağı şablonundan üretildi. Görevlendirmeler burada durur.",
                    ["workItemKeyPrefix"] = MeetingWorkspacePrefix(project.code),
                    ["workItemKeyFormat"] = "{prefix}-{seq:D4}",
                    ["workItemSequenceStart"] = 1
                },
                token,
                ct);
            _logger.LogInformation(
                "Created meeting workspace {WorkspaceId} ({Name}) for project {ProjectId}",
                workspaceId, name, projectId);
        }

        await EnsureMeetingSkeletonAsync(workspaceId, token, ct);
        await _dg.UpdateAsync(
            PmDatasets.Projects,
            projectId,
            new Dictionary<string, object?> { ["meetingWorkspaceId"] = workspaceId },
            token,
            ct);
        project.meetingWorkspaceId = workspaceId;
        return await ToMeetingWorkspaceDtoAsync(project, token, ct);
    }

    private async Task EnsureMeetingSkeletonAsync(string workspaceId, string token, CancellationToken ct)
    {
        var (openId, progressId, doneId, waivedId) = await EnsureMeetingStatesAsync(token, ct);
        var fieldId = await EnsureNamedAsync(
            OcDatasets.Fields,
            new Dictionary<string, object?> { ["key"] = MeetingFieldKey },
            new Dictionary<string, object?>
            {
                ["key"] = MeetingFieldKey,
                ["label"] = "Toplantı",
                ["fieldType"] = "text",
                ["scope"] = "pool",
                ["description"] = "Bu görevlendirmenin doğduğu toplantı.",
                ["isSystem"] = false,
                ["isSensitive"] = false,
                ["sortOrder"] = 40
            },
            token,
            ct);

        var flowId = await EnsureNamedAsync(
            OcDatasets.StateFlows,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = "Toplantı akışı" },
            new Dictionary<string, object?>
            {
                ["name"] = "Toplantı akışı",
                ["workspaceId"] = workspaceId,
                ["initialStateId"] = openId,
                ["isDefault"] = true,
                ["isActive"] = true,
                ["transitions"] = new object[]
                {
                    Transition("start", openId, progressId, "Sürdür", 0),
                    Transition("finish", progressId, doneId, "Bitir", 1),
                    Transition("waive", openId, waivedId, "Vazgeç", 2),
                    Transition("waive_progress", progressId, waivedId, "Vazgeç", 3),
                    Transition("reopen", doneId, openId, "Yeniden aç", 4),
                    Transition("reopen_waived", waivedId, openId, "Yeniden aç", 5)
                }
            },
            token,
            ct);

        var typeId = await EnsureNamedAsync(
            OcDatasets.WorkItemTypes,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = "Görevlendirme" },
            new Dictionary<string, object?>
            {
                ["name"] = "Görevlendirme",
                ["category"] = "task",
                ["workspaceId"] = workspaceId,
                ["defaultStateFlowId"] = flowId
            },
            token,
            ct);

        var formFields = new[] { "title", "assignee", "dueDate", "description", MeetingFieldKey };
        var formId = await EnsureNamedAsync(
            OcDatasets.Forms,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = "Görevlendirme formu" },
            new Dictionary<string, object?>
            {
                ["name"] = "Görevlendirme formu",
                ["workspaceId"] = workspaceId,
                ["defaultTypeId"] = typeId,
                ["defaultStateFlowId"] = flowId,
                ["defaultStateId"] = openId,
                ["isDefault"] = true,
                ["layout"] = new Dictionary<string, object?>
                {
                    ["sections"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["key"] = "main",
                            ["title"] = "Görevlendirme",
                            ["fields"] = formFields
                        }
                    }
                },
                ["fieldBehaviors"] = new Dictionary<string, object?>
                {
                    ["title"] = new Dictionary<string, object?> { ["visible"] = true, ["required"] = true },
                    ["assignee"] = new Dictionary<string, object?> { ["visible"] = true },
                    ["dueDate"] = new Dictionary<string, object?> { ["visible"] = true },
                    ["description"] = new Dictionary<string, object?> { ["visible"] = true },
                    [MeetingFieldKey] = new Dictionary<string, object?> { ["visible"] = true }
                }
            },
            token,
            ct);

        await EnsureNamedAsync(
            OcDatasets.Boards,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = "Görevlendirmeler" },
            new Dictionary<string, object?>
            {
                ["name"] = "Görevlendirmeler",
                ["workspaceId"] = workspaceId,
                ["viewType"] = "kanban",
                ["isDefault"] = true,
                ["defaultStateFlowId"] = flowId,
                ["defaultFormId"] = formId,
                ["defaultTypeId"] = typeId,
                ["defaultStateId"] = openId,
                ["visibleFields"] = new[] { "key", "title", "stateId", "assignee", "dueDate", MeetingFieldKey },
                ["config"] = new Dictionary<string, object?>
                {
                    ["columns"] = new object[]
                    {
                        new Dictionary<string, object?> { ["stateId"] = openId, ["title"] = "Açık", ["queryKey"] = "wi_board_column" },
                        new Dictionary<string, object?> { ["stateId"] = progressId, ["title"] = "Sürüyor", ["queryKey"] = "wi_board_column" },
                        new Dictionary<string, object?> { ["stateId"] = doneId, ["title"] = "Bitti", ["queryKey"] = "wi_board_column" },
                        new Dictionary<string, object?> { ["stateId"] = waivedId, ["title"] = "Vazgeçildi", ["queryKey"] = "wi_board_column" }
                    },
                    ["listColumns"] = new object[]
                    {
                        ListCol("key", "No", sortable: true, filterable: false),
                        ListCol("title", "Görevlendirme", sortable: true, filterable: true),
                        ListCol("stateId", "Durum", sortable: true, filterable: true),
                        ListCol("assignee", "Kişi", sortable: false, filterable: true),
                        ListCol("dueDate", "Son tarih", sortable: true, filterable: true, format: "date")
                    },
                    ["defaultSort"] = new Dictionary<string, object?>
                    {
                        ["field"] = "dueDate",
                        ["direction"] = "asc"
                    }
                }
            },
            token,
            ct);

        await EnsureNamedAsync(
            OcDatasets.Profiles,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = "Görevlendirme profili" },
            new Dictionary<string, object?>
            {
                ["name"] = "Görevlendirme profili",
                ["workspaceId"] = workspaceId,
                ["defaultTypeId"] = typeId,
                ["isDefault"] = true,
                ["fieldBehaviors"] = new Dictionary<string, object?>
                {
                    ["title"] = new Dictionary<string, object?> { ["visible"] = true, ["readonly"] = false, ["required"] = true },
                    ["assignee"] = new Dictionary<string, object?> { ["visible"] = true },
                    ["dueDate"] = new Dictionary<string, object?> { ["visible"] = true },
                    ["description"] = new Dictionary<string, object?> { ["visible"] = true },
                    [MeetingFieldKey] = new Dictionary<string, object?> { ["visible"] = true },
                    ["typeId"] = new Dictionary<string, object?> { ["visible"] = true, ["readonly"] = true }
                },
                ["actions"] = new object[]
                {
                    new Dictionary<string, object?> { ["transitionKey"] = "start", ["order"] = 0, ["label"] = "Sürdür" },
                    new Dictionary<string, object?> { ["transitionKey"] = "finish", ["order"] = 1, ["label"] = "Bitir" },
                    new Dictionary<string, object?> { ["transitionKey"] = "waive", ["order"] = 2, ["label"] = "Vazgeç" },
                    new Dictionary<string, object?> { ["transitionKey"] = "waive_progress", ["order"] = 3, ["label"] = "Vazgeç" },
                    new Dictionary<string, object?> { ["transitionKey"] = "reopen", ["order"] = 4, ["label"] = "Yeniden aç" },
                    new Dictionary<string, object?> { ["transitionKey"] = "reopen_waived", ["order"] = 5, ["label"] = "Yeniden aç" }
                },
                ["header"] = new Dictionary<string, object?> { ["showBreadcrumb"] = true, ["showKey"] = true },
                ["sidebar"] = new Dictionary<string, object?>
                {
                    ["showSla"] = false,
                    ["showWatchers"] = true
                },
                ["panels"] = new Dictionary<string, object?>
                {
                    ["timeline"] = new Dictionary<string, object?> { ["enabled"] = true },
                    ["comments"] = new Dictionary<string, object?> { ["enabled"] = true }
                },
                ["layout"] = new Dictionary<string, object?>
                {
                    ["sections"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["key"] = "summary",
                            ["title"] = "Görevlendirme",
                            ["fields"] = new[] { "title", "assignee", "dueDate", "description", MeetingFieldKey, "key" }
                        }
                    }
                }
            },
            token,
            ct);

        await _dg.UpdateAsync(
            OcDatasets.Workspaces,
            workspaceId,
            new Dictionary<string, object?>
            {
                ["defaultStateFlowId"] = flowId,
                ["enabledTypeIds"] = new[] { typeId },
                ["enabledStateIds"] = new[] { openId, progressId, doneId, waivedId },
                ["enabledFieldIds"] = new[] { fieldId }
            },
            token,
            ct);
    }

    private static Dictionary<string, object?> Transition(
        string key,
        string fromStateId,
        string toStateId,
        string label,
        int order) =>
        new()
        {
            ["transitionKey"] = key,
            ["fromStateId"] = fromStateId,
            ["toStateId"] = toStateId,
            ["label"] = label,
            ["order"] = order
        };

    private async Task<(string OpenId, string ProgressId, string DoneId, string WaivedId)> EnsureMeetingStatesAsync(
        string token,
        CancellationToken ct)
    {
        var openId = await EnsurePackStateAsync(
            [MeetingStateOpenName],
            MeetingStateOpenName,
            new Dictionary<string, object?>
            {
                ["name"] = MeetingStateOpenName,
                ["category"] = "open",
                ["isInitial"] = true,
                ["isStart"] = true,
                ["color"] = "#2196F3"
            },
            token,
            ct);
        var progressId = await EnsurePackStateAsync(
            [MeetingStateProgressName],
            MeetingStateProgressName,
            new Dictionary<string, object?>
            {
                ["name"] = MeetingStateProgressName,
                ["category"] = "in_progress",
                ["color"] = "#FB8C00"
            },
            token,
            ct);
        var doneId = await EnsurePackStateAsync(
            [MeetingStateDoneName],
            MeetingStateDoneName,
            new Dictionary<string, object?>
            {
                ["name"] = MeetingStateDoneName,
                ["category"] = "closed",
                ["isClosed"] = true,
                ["isTerminal"] = true,
                ["color"] = "#4CAF50"
            },
            token,
            ct);
        var waivedId = await EnsurePackStateAsync(
            [MeetingStateWaivedName],
            MeetingStateWaivedName,
            new Dictionary<string, object?>
            {
                ["name"] = MeetingStateWaivedName,
                ["category"] = "closed",
                ["isClosed"] = true,
                ["isTerminal"] = true,
                ["color"] = "#9E9E9E"
            },
            token,
            ct);
        return (openId, progressId, doneId, waivedId);
    }

    private async Task<MeetingWorkspaceDto> ToMeetingWorkspaceDtoAsync(
        PmProjectRow project,
        string token,
        CancellationToken ct)
    {
        var id = EmptyToNull(project.meetingWorkspaceId);
        string? name = null;
        if (id is not null)
        {
            var existing = await _dg.GetByIdAsync<WorkspaceRecord>(
                OcDatasets.Workspaces, id, token, ct, expand: false);
            name = EmptyToNull(existing?.Name);
        }

        return new MeetingWorkspaceDto
        {
            WorkspaceId = id,
            WorkspaceName = name,
            TemplateName = MeetingTemplateName,
            SuggestedName = MeetingWorkspaceName(project.code)
        };
    }

    private static string MeetingWorkspaceName(string? projectCode)
    {
        var code = (projectCode ?? string.Empty).Trim();
        return string.IsNullOrEmpty(code) ? "Toplantılar" : $"{code} Toplantılar";
    }

    private static string MeetingWorkspacePrefix(string? projectCode)
    {
        const string suffix = "-MTG";
        var room = PackWorkItemKeyPrefixMaxLength - suffix.Length;
        var basePrefix = PackWorkspacePrefix(projectCode);
        if (basePrefix.Length > room)
            basePrefix = basePrefix[..room].TrimEnd('-');
        return string.IsNullOrEmpty(basePrefix) ? "MTG" : basePrefix + suffix;
    }
}
