using System.Text.Json;
using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Packs;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    private sealed class PackOcRuntimeResult
    {
        public int RulesCreated { get; set; }
        public int RulesSkipped { get; set; }
        public int SlaCreated { get; set; }
        public int SlaSkipped { get; set; }
        public int DashboardsCreated { get; set; }
        public int DashboardsSkipped { get; set; }
    }

    private async Task<PackOcRuntimeResult> PreviewPackOcRuntimeAsync(
        string? workspaceId,
        JobPackDefinition pack,
        string token,
        CancellationToken ct)
    {
        var result = new PackOcRuntimeResult();
        var extras = pack.Workspace;
        if (extras is null)
            return result;

        foreach (var rule in extras.Rules ?? [])
        {
            if (string.IsNullOrWhiteSpace(rule.Name)) continue;
            if (await OcRuntimeExistsAsync(OcDatasets.Rules, workspaceId, QualifiedOcName(pack, rule.Name), token, ct))
                result.RulesSkipped++;
            else
                result.RulesCreated++;
        }

        foreach (var sla in extras.SlaPolicies ?? [])
        {
            if (string.IsNullOrWhiteSpace(sla.Name)) continue;
            if (await OcRuntimeExistsAsync(OcDatasets.SlaPolicies, workspaceId, QualifiedOcName(pack, sla.Name), token, ct))
                result.SlaSkipped++;
            else
                result.SlaCreated++;
        }

        foreach (var dashboard in extras.Dashboards ?? [])
        {
            if (string.IsNullOrWhiteSpace(dashboard.Name)) continue;
            if (await OcRuntimeExistsAsync(OcDatasets.Dashboards, workspaceId, QualifiedOcName(pack, dashboard.Name), token, ct))
                result.DashboardsSkipped++;
            else
                result.DashboardsCreated++;
        }

        return result;
    }

    private async Task<PackOcRuntimeResult> EnsurePackOcRuntimeAsync(
        string? workspaceId,
        JobPackDefinition pack,
        string token,
        CancellationToken ct)
    {
        var result = new PackOcRuntimeResult();
        var extras = pack.Workspace;
        if (string.IsNullOrWhiteSpace(workspaceId) || extras is null)
            return result;

        var (openId, progressId, doneId) = await EnsurePackStatesAsync(token, ct);
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["workspaceId"] = workspaceId,
            ["openStateId"] = openId,
            ["progressStateId"] = progressId,
            ["doneStateId"] = doneId
        };

        foreach (var rule in extras.Rules ?? [])
        {
            if (string.IsNullOrWhiteSpace(rule.Name)) continue;
            var name = QualifiedOcName(pack, rule.Name);
            var created = await EnsureNamedCreatedAsync(
                OcDatasets.Rules,
                new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = name },
                new Dictionary<string, object?>
                {
                    ["name"] = name,
                    ["description"] = EmptyToNull(rule.Description),
                    ["workspaceId"] = workspaceId,
                    ["ruleType"] = string.IsNullOrWhiteSpace(rule.RuleType) ? "validation" : rule.RuleType.Trim(),
                    ["trigger"] = string.IsNullOrWhiteSpace(rule.Trigger) ? "WorkItemTransition" : rule.Trigger.Trim(),
                    ["transitionKey"] = EmptyToNull(rule.TransitionKey),
                    ["applyMode"] = string.IsNullOrWhiteSpace(rule.ApplyMode) ? "pre" : rule.ApplyMode.Trim(),
                    ["conditions"] = JsonValue(rule.Conditions),
                    ["errorMessage"] = EmptyToNull(rule.ErrorMessage),
                    ["priority"] = rule.Priority ?? 100,
                    ["isActive"] = true
                },
                token,
                ct);
            if (created) result.RulesCreated++;
            else result.RulesSkipped++;
        }

        foreach (var sla in extras.SlaPolicies ?? [])
        {
            if (string.IsNullOrWhiteSpace(sla.Name)) continue;
            var name = QualifiedOcName(pack, sla.Name);
            var created = await EnsureNamedCreatedAsync(
                OcDatasets.SlaPolicies,
                new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = name },
                new Dictionary<string, object?>
                {
                    ["name"] = name,
                    ["workspaceId"] = workspaceId,
                    ["responseTargetMinutes"] = sla.ResponseTargetMinutes ?? 480,
                    ["resolveTargetMinutes"] = sla.ResolveTargetMinutes ?? 2400,
                    ["priority"] = sla.Priority ?? 50,
                    ["isActive"] = true
                },
                token,
                ct);
            if (created) result.SlaCreated++;
            else result.SlaSkipped++;
        }

        foreach (var dashboard in extras.Dashboards ?? [])
        {
            if (string.IsNullOrWhiteSpace(dashboard.Name)) continue;
            var name = QualifiedOcName(pack, dashboard.Name);
            var created = await EnsureNamedCreatedAsync(
                OcDatasets.Dashboards,
                new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = name },
                new Dictionary<string, object?>
                {
                    ["name"] = name,
                    ["description"] = EmptyToNull(dashboard.Description),
                    ["workspaceId"] = workspaceId,
                    ["scope"] = string.IsNullOrWhiteSpace(dashboard.Scope) ? "workspace" : dashboard.Scope.Trim(),
                    ["isDefault"] = dashboard.IsDefault ?? true,
                    ["isActive"] = true,
                    ["layout"] = HasJson(dashboard.Layout)
                        ? SubstituteJson(dashboard.Layout, map)
                        : DefaultDashboardLayout(),
                    ["widgets"] = HasJson(dashboard.Widgets)
                        ? SubstituteJson(dashboard.Widgets, map)
                        : DefaultDashboardWidgets(map)
                },
                token,
                ct);
            if (created) result.DashboardsCreated++;
            else result.DashboardsSkipped++;
        }

        return result;
    }

    private async Task<bool> OcRuntimeExistsAsync(
        string dataset,
        string? workspaceId,
        string name,
        string token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
            return false;
        var id = await FindFirstIdAsync(
            dataset,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = name },
            token,
            ct);
        return !string.IsNullOrWhiteSpace(id);
    }

    private async Task<bool> EnsureNamedCreatedAsync(
        string dataset,
        Dictionary<string, object?> match,
        Dictionary<string, object?> payload,
        string token,
        CancellationToken ct)
    {
        var existing = await FindFirstIdAsync(dataset, match, token, ct);
        if (!string.IsNullOrWhiteSpace(existing))
            return false;
        await CreateNamedAsync(dataset, match, payload, token, ct);
        return true;
    }

    private static string QualifiedOcName(JobPackDefinition pack, string name) =>
        $"{pack.Name} — {name.Trim()}";

    private static bool HasJson(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object or JsonValueKind.Array };

    private static object? JsonValue(JsonElement? element)
    {
        if (!HasJson(element))
            return null;
        return JsonSerializer.Deserialize<object>(element!.Value.GetRawText());
    }

    private static object? SubstituteJson(JsonElement? element, Dictionary<string, string> map)
    {
        if (!HasJson(element))
            return null;
        var json = element!.Value.GetRawText();
        foreach (var pair in map)
            json = json.Replace("{" + pair.Key + "}", pair.Value, StringComparison.Ordinal);
        return JsonSerializer.Deserialize<object>(json);
    }

    private static object DefaultDashboardLayout() => new Dictionary<string, object?>
    {
        ["type"] = "rows",
        ["rows"] = new object[]
        {
            new Dictionary<string, object?>
            {
                ["cols"] = new object[]
                {
                    new Dictionary<string, object?> { ["widgetId"] = "count_open", ["span"] = 12, ["spanMd"] = 4 },
                    new Dictionary<string, object?> { ["widgetId"] = "count_progress", ["span"] = 12, ["spanMd"] = 4 },
                    new Dictionary<string, object?> { ["widgetId"] = "count_done", ["span"] = 12, ["spanMd"] = 4 }
                }
            }
        }
    };

    private static object DefaultDashboardWidgets(Dictionary<string, string> map) => new object[]
    {
        SummaryCard("count_open", "Açık", "mdi-inbox-outline", "info", map["workspaceId"], map["openStateId"]),
        SummaryCard("count_progress", "Devam", "mdi-progress-clock", "primary", map["workspaceId"], map["progressStateId"]),
        SummaryCard("count_done", "Tamam", "mdi-check-circle-outline", "success", map["workspaceId"], map["doneStateId"])
    };

    private static Dictionary<string, object?> SummaryCard(
        string key,
        string title,
        string icon,
        string accent,
        string workspaceId,
        string stateId) => new()
    {
        ["key"] = key,
        ["type"] = "summaryCard",
        ["title"] = title,
        ["icon"] = icon,
        ["accentColor"] = accent,
        ["dataset"] = "op_work_items",
        ["queryKey"] = "wi_by_workspace_and_state",
        ["parameters"] = new Dictionary<string, object?>
        {
            ["workspaceId"] = workspaceId,
            ["stateId"] = stateId
        },
        ["take"] = 500
    };
}
