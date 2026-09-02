using System.Net;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Application.Packs;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    private const string PackStateOpenName = "PM Open";
    private const string PackStateProgressName = "PM In Progress";
    private const string PackStateDoneName = "PM Done";

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

    private static string PackWorkspacePrefix(string? projectCode)
    {
        var chars = (projectCode ?? string.Empty).Where(char.IsLetterOrDigit).Take(12).ToArray();
        return chars.Length == 0 ? "PM" : new string(chars).ToUpperInvariant();
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
                    ["workspaceType"] = "team",
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
                        ["label"] = "Baslat",
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
                        ["label"] = "Yeniden ac",
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
                ["defaultStateFlowId"] = flowId,
                ["defaultFormId"] = formId,
                ["visibleFields"] = new[] { "title", "assignee", "priorityId", "key" },
                ["config"] = new Dictionary<string, object?>
                {
                    ["columns"] = new object[]
                    {
                        new Dictionary<string, object?> { ["stateId"] = openId, ["title"] = "Acik", ["queryKey"] = "wi_board_column" },
                        new Dictionary<string, object?> { ["stateId"] = progressId, ["title"] = "Devam", ["queryKey"] = "wi_board_column" },
                        new Dictionary<string, object?> { ["stateId"] = doneId, ["title"] = "Tamam", ["queryKey"] = "wi_board_column" }
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
                    new Dictionary<string, object?> { ["transitionKey"] = "start_progress", ["order"] = 0, ["label"] = "Baslat" },
                    new Dictionary<string, object?> { ["transitionKey"] = "resolve", ["order"] = 1, ["label"] = "Kapat" }
                },
                ["header"] = new Dictionary<string, object?> { ["showBreadcrumb"] = true, ["showKey"] = true },
                ["sidebar"] = new Dictionary<string, object?> { ["showSla"] = false, ["showWatchers"] = true },
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
        var openId = await FindFirstIdAsync(
            OcDatasets.States, new Dictionary<string, object?> { ["category"] = "open" }, token, ct)
            ?? await FindFirstIdAsync(
                OcDatasets.States, new Dictionary<string, object?> { ["name"] = PackStateOpenName }, token, ct)
            ?? await CreateNamedAsync(
                OcDatasets.States,
                new Dictionary<string, object?> { ["name"] = PackStateOpenName },
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

        var progressId = await FindFirstIdAsync(
            OcDatasets.States, new Dictionary<string, object?> { ["category"] = "in_progress" }, token, ct)
            ?? await FindFirstIdAsync(
                OcDatasets.States, new Dictionary<string, object?> { ["name"] = PackStateProgressName }, token, ct)
            ?? await CreateNamedAsync(
                OcDatasets.States,
                new Dictionary<string, object?> { ["name"] = PackStateProgressName },
                new Dictionary<string, object?>
                {
                    ["name"] = PackStateProgressName,
                    ["category"] = "in_progress",
                    ["color"] = "#2196F3"
                },
                token,
                ct);

        var doneId = await FindFirstIdAsync(
            OcDatasets.States, new Dictionary<string, object?> { ["category"] = "closed" }, token, ct)
            ?? await FindFirstIdAsync(
                OcDatasets.States, new Dictionary<string, object?> { ["isClosed"] = true }, token, ct)
            ?? await FindFirstIdAsync(
                OcDatasets.States, new Dictionary<string, object?> { ["name"] = PackStateDoneName }, token, ct)
            ?? await CreateNamedAsync(
                OcDatasets.States,
                new Dictionary<string, object?> { ["name"] = PackStateDoneName },
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
