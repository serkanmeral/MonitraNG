using System.Net;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Application.Packs;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    private const string PackStateOpenName = "PM Açık";
    private const string PackStateProgressName = "PM Devam";
    private const string PackStateDoneName = "PM Bitti";
    private static readonly string[] PackStateOpenAliases = ["PM Açık", "PM Open"];
    private static readonly string[] PackStateProgressAliases = ["PM Devam", "PM In Progress"];
    private static readonly string[] PackStateDoneAliases = ["PM Bitti", "PM Done"];
    private static readonly string[] PackBoardVisibleFields =
    [
        "key", "title", "stateId", "assignee", "priorityId", "lastStateChangeAt"
    ];

    private static readonly object[] PackBoardListColumns =
    [
        ListCol("key", "No", sortable: true, filterable: false),
        ListCol("title", "Başlık", sortable: true, filterable: true),
        ListCol("stateId", "Durum", sortable: true, filterable: true),
        ListCol("assignee", "Atanan", sortable: false, filterable: true),
        ListCol("priorityId", "Öncelik", sortable: true, filterable: true),
        ListCol("lastStateChangeAt", "Son değişim", sortable: true, filterable: true, format: "date")
    ];

    private static Dictionary<string, object?> ListCol(
        string key,
        string label,
        bool sortable,
        bool filterable,
        string? format = null)
    {
        var row = new Dictionary<string, object?>
        {
            ["key"] = key,
            ["label"] = label,
            ["sortable"] = sortable,
            ["filterable"] = filterable
        };
        if (!string.IsNullOrWhiteSpace(format))
            row["format"] = format;
        return row;
    }

    private sealed class PackWorkspaceEnsureResult
    {
        public bool Created { get; init; }
        public string? WorkspaceId { get; init; }
        public string Action { get; init; } = "skip";
        public string? WorkspaceName { get; init; }
    }

    private static string PackWorkspaceName(string? projectCode)
    {
        var code = (projectCode ?? string.Empty).Trim();
        return string.IsNullOrEmpty(code) ? "PM Project" : $"PM {code}";
    }

    internal const int PackWorkItemKeyPrefixMaxLength = 64;

    private static string PackWorkspacePrefix(string? projectCode)
    {
        var raw = (projectCode ?? string.Empty).Trim().ToUpperInvariant();
        var chars = new List<char>(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsLetterOrDigit(c)) chars.Add(c);
            else if (c is '-' or '_') chars.Add('-');
        }

        var collapsed = new string(chars.ToArray());
        while (collapsed.Contains("--", StringComparison.Ordinal))
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        collapsed = collapsed.Trim('-');
        if (collapsed.Length == 0) return "PM";
        if (collapsed.Length <= PackWorkItemKeyPrefixMaxLength) return collapsed;
        return collapsed[..PackWorkItemKeyPrefixMaxLength].TrimEnd('-');
    }

    private async Task<PackWorkspaceEnsureResult> PreviewPackWorkspaceAsync(
        PmProjectRow project,
        string token,
        CancellationToken ct)
    {
        var name = PackWorkspaceName(project.code);
        if (!string.IsNullOrWhiteSpace(project.workspaceId))
        {
            var existing = await _dg.GetByIdAsync<WorkspaceRecord>(
                OcDatasets.Workspaces, project.workspaceId, token, ct, expand: false);
            if (!string.IsNullOrWhiteSpace(existing?.DataId))
            {
                return new PackWorkspaceEnsureResult
                {
                    Created = false,
                    WorkspaceId = existing.DataId,
                    Action = "skip",
                    WorkspaceName = string.IsNullOrWhiteSpace(existing.Name) ? name : existing.Name
                };
            }
        }

        var namedId = await FindFirstIdAsync(
            OcDatasets.Workspaces,
            new Dictionary<string, object?> { ["name"] = name },
            token,
            ct);
        if (!string.IsNullOrWhiteSpace(namedId))
        {
            return new PackWorkspaceEnsureResult
            {
                Created = false,
                WorkspaceId = namedId,
                Action = "skip",
                WorkspaceName = name
            };
        }

        return new PackWorkspaceEnsureResult
        {
            Created = true,
            WorkspaceId = null,
            Action = "create",
            WorkspaceName = name
        };
    }

    private async Task<PackWorkspaceEnsureResult> EnsurePackWorkspaceAsync(
        string projectId,
        JobPackDefinition pack,
        string token,
        CancellationToken ct)
    {
        var project = await LoadProjectOrThrowAsync(projectId, token, ct);
        var name = PackWorkspaceName(project.code);

        if (!string.IsNullOrWhiteSpace(project.workspaceId))
        {
            var linked = await _dg.GetByIdAsync<WorkspaceRecord>(
                OcDatasets.Workspaces, project.workspaceId, token, ct, expand: false);
            if (!string.IsNullOrWhiteSpace(linked?.DataId))
            {
                return new PackWorkspaceEnsureResult
                {
                    Created = false,
                    WorkspaceId = linked.DataId,
                    Action = "skip",
                    WorkspaceName = string.IsNullOrWhiteSpace(linked.Name) ? name : linked.Name
                };
            }
        }

        var existingId = await FindFirstIdAsync(
            OcDatasets.Workspaces,
            new Dictionary<string, object?> { ["name"] = name },
            token,
            ct);
        var created = false;
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
                    ["description"] = $"Pack workspace for {project.code} ({pack.Code}).",
                    ["workItemKeyPrefix"] = PackWorkspacePrefix(project.code),
                    ["workItemKeyFormat"] = "{prefix}-{seq:D4}",
                    ["workItemSequenceStart"] = 1
                },
                token,
                ct);
            created = true;
            _logger.LogInformation(
                "Created pack workspace {WorkspaceId} ({Name}) for project {ProjectId} pack {PackCode}",
                workspaceId, name, projectId, pack.Code);
        }

        var (openId, progressId, doneId) = await EnsurePackStatesAsync(token, ct);
        var flowId = await EnsureNamedAsync(
            OcDatasets.StateFlows,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = $"{project.code} Flow" },
            new Dictionary<string, object?>
            {
                ["name"] = $"{project.code} Flow",
                ["workspaceId"] = workspaceId,
                ["initialStateId"] = openId,
                ["isDefault"] = true,
                ["isActive"] = true,
                ["transitions"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["transitionKey"] = "start_progress",
                        ["fromStateId"] = openId,
                        ["toStateId"] = progressId,
                        ["label"] = "Başlat",
                        ["order"] = 0
                    },
                    new Dictionary<string, object?>
                    {
                        ["transitionKey"] = "resolve",
                        ["fromStateId"] = progressId,
                        ["toStateId"] = doneId,
                        ["label"] = "Kapat",
                        ["order"] = 1
                    },
                    new Dictionary<string, object?>
                    {
                        ["transitionKey"] = "reopen",
                        ["fromStateId"] = doneId,
                        ["toStateId"] = openId,
                        ["label"] = "Yeniden aç",
                        ["order"] = 2
                    }
                }
            },
            token,
            ct);

        var typeId = await EnsureNamedAsync(
            OcDatasets.WorkItemTypes,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = $"{project.code} Task" },
            new Dictionary<string, object?>
            {
                ["name"] = $"{project.code} Task",
                ["category"] = "task",
                ["workspaceId"] = workspaceId,
                ["defaultStateFlowId"] = flowId
            },
            token,
            ct);

        var formId = await EnsureNamedAsync(
            OcDatasets.Forms,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = $"{project.code} Create Form" },
            new Dictionary<string, object?>
            {
                ["name"] = $"{project.code} Create Form",
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
                            ["title"] = "Temel bilgiler",
                            ["fields"] = new[] { "title", "description", "typeId", "assignee", "priorityId", "boardId" }
                        }
                    }
                },
                ["fieldBehaviors"] = new Dictionary<string, object?>
                {
                    ["title"] = new Dictionary<string, object?> { ["visible"] = true, ["required"] = true },
                    ["description"] = new Dictionary<string, object?> { ["visible"] = true },
                    ["typeId"] = new Dictionary<string, object?> { ["visible"] = true, ["required"] = true },
                    ["assignee"] = new Dictionary<string, object?> { ["visible"] = true },
                    ["priorityId"] = new Dictionary<string, object?> { ["visible"] = true },
                    ["boardId"] = new Dictionary<string, object?> { ["visible"] = true }
                }
            },
            token,
            ct);

        await EnsureNamedAsync(
            OcDatasets.Boards,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = $"{project.code} Board" },
            new Dictionary<string, object?>
            {
                ["name"] = $"{project.code} Board",
                ["workspaceId"] = workspaceId,
                ["viewType"] = "list",
                ["isDefault"] = true,
                ["defaultStateFlowId"] = flowId,
                ["defaultFormId"] = formId,
                ["defaultTypeId"] = typeId,
                ["defaultStateId"] = openId,
                ["visibleFields"] = PackBoardVisibleFields,
                ["config"] = new Dictionary<string, object?>
                {
                    ["columns"] = new object[]
                    {
                        new Dictionary<string, object?> { ["stateId"] = openId, ["title"] = "Açık", ["queryKey"] = "wi_board_column" },
                        new Dictionary<string, object?> { ["stateId"] = progressId, ["title"] = "Devam", ["queryKey"] = "wi_board_column" },
                        new Dictionary<string, object?> { ["stateId"] = doneId, ["title"] = "Bitti", ["queryKey"] = "wi_board_column" }
                    },
                    ["listColumns"] = PackBoardListColumns,
                    ["defaultSort"] = new Dictionary<string, object?>
                    {
                        ["field"] = "lastStateChangeAt",
                        ["direction"] = "desc"
                    }
                }
            },
            token,
            ct);

        await EnsureNamedAsync(
            OcDatasets.Profiles,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = $"{project.code} Profile" },
            new Dictionary<string, object?>
            {
                ["name"] = $"{project.code} Profile",
                ["workspaceId"] = workspaceId,
                ["defaultTypeId"] = typeId,
                ["isDefault"] = true,
                ["fieldBehaviors"] = new Dictionary<string, object?>
                {
                    ["title"] = new Dictionary<string, object?> { ["visible"] = true, ["readonly"] = false, ["required"] = true },
                    ["description"] = new Dictionary<string, object?> { ["visible"] = true, ["readonly"] = false },
                    ["assignee"] = new Dictionary<string, object?> { ["visible"] = true },
                    ["priorityId"] = new Dictionary<string, object?> { ["visible"] = true },
                    ["typeId"] = new Dictionary<string, object?> { ["visible"] = true, ["readonly"] = true },
                    ["boardId"] = new Dictionary<string, object?> { ["visible"] = true, ["readonly"] = true }
                },
                ["actions"] = new object[]
                {
                    new Dictionary<string, object?> { ["transitionKey"] = "start_progress", ["order"] = 0, ["label"] = "Başlat" },
                    new Dictionary<string, object?> { ["transitionKey"] = "resolve", ["order"] = 1, ["label"] = "Kapat" },
                    new Dictionary<string, object?> { ["transitionKey"] = "reopen", ["order"] = 2, ["label"] = "Yeniden aç" }
                },
                ["header"] = new Dictionary<string, object?> { ["showBreadcrumb"] = true, ["showKey"] = true },
                    ["sidebar"] = new Dictionary<string, object?>
                    {
                        ["showSla"] = pack.Workspace?.SlaPolicies is { Count: > 0 },
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
                            ["title"] = "Ozet",
                            ["fields"] = new[] { "title", "description", "assignee", "priorityId", "typeId", "boardId", "key" }
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
                ["enabledStateIds"] = new[] { openId, progressId, doneId }
            },
            token,
            ct);

        if (!string.Equals(project.workspaceId, workspaceId, StringComparison.Ordinal))
        {
            await _dg.UpdateAsync(
                PmDatasets.Projects,
                projectId,
                new Dictionary<string, object?> { ["workspaceId"] = workspaceId },
                token,
                ct);
        }

        return new PackWorkspaceEnsureResult
        {
            Created = created,
            WorkspaceId = workspaceId,
            Action = created ? "create" : "skip",
            WorkspaceName = name
        };
    }

    private async Task<(string OpenId, string ProgressId, string DoneId)> EnsurePackStatesAsync(
        string token,
        CancellationToken ct)
    {
        var openId = await EnsurePackStateAsync(
            PackStateOpenAliases,
            PackStateOpenName,
            new Dictionary<string, object?>
            {
                ["name"] = PackStateOpenName,
                ["category"] = "open",
                ["isInitial"] = true,
                ["isStart"] = true,
                ["color"] = "#4CAF50"
            },
            token,
            ct);

        var progressId = await EnsurePackStateAsync(
            PackStateProgressAliases,
            PackStateProgressName,
            new Dictionary<string, object?>
            {
                ["name"] = PackStateProgressName,
                ["category"] = "in_progress",
                ["color"] = "#2196F3"
            },
            token,
            ct);

        var doneId = await EnsurePackStateAsync(
            PackStateDoneAliases,
            PackStateDoneName,
            new Dictionary<string, object?>
            {
                ["name"] = PackStateDoneName,
                ["category"] = "closed",
                ["isClosed"] = true,
                ["color"] = "#9E9E9E"
            },
            token,
            ct);

        return (openId, progressId, doneId);
    }

    private async Task<string> EnsurePackStateAsync(
        IReadOnlyList<string> aliases,
        string createName,
        Dictionary<string, object?> payload,
        string token,
        CancellationToken ct)
    {
        foreach (var alias in aliases)
        {
            var existing = await FindFirstIdAsync(
                OcDatasets.States,
                new Dictionary<string, object?> { ["name"] = alias },
                token,
                ct);
            if (!string.IsNullOrWhiteSpace(existing))
                return existing;
        }

        return await CreateNamedAsync(
            OcDatasets.States,
            new Dictionary<string, object?> { ["name"] = createName },
            payload,
            token,
            ct);
    }

    private async Task<string?> FindFirstIdAsync(
        string dataset,
        Dictionary<string, object?> match,
        string token,
        CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(dataset, match, "limit=5&expand=false", token, ct);
        var row = page.Items.FirstOrDefault();
        return row is null ? null : EmptyToNull(ReadId(row));
    }

    private async Task<string> EnsureNamedAsync(
        string dataset,
        Dictionary<string, object?> match,
        Dictionary<string, object?> payload,
        string token,
        CancellationToken ct)
    {
        var existing = await FindFirstIdAsync(dataset, match, token, ct);
        if (!string.IsNullOrWhiteSpace(existing))
            return existing;
        return await CreateNamedAsync(dataset, match, payload, token, ct);
    }

    private async Task<string> CreateNamedAsync(
        string dataset,
        Dictionary<string, object?> match,
        Dictionary<string, object?> payload,
        string token,
        CancellationToken ct)
    {
        try
        {
            var created = await _dg.CreateAsync(dataset, payload, token, ct);
            var id = EmptyToNull(ReadId(created));
            if (id is not null)
                return id;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
        {
            var again = await FindFirstIdAsync(dataset, match, token, ct);
            if (!string.IsNullOrWhiteSpace(again))
                return again;
            throw;
        }

        var retry = await FindFirstIdAsync(dataset, match, token, ct);
        if (!string.IsNullOrWhiteSpace(retry))
            return retry;
        throw new OperationCoreException(
            "CREATE_FAILED",
            $"Could not create {dataset}.",
            "Kayıt oluşturulamadı.",
            500);
    }
}
