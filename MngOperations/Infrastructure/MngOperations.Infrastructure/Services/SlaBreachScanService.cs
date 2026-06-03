using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Sla;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Rules;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed class SlaBreachScanService : ISlaBreachScanService
{
    private const string ResponseNotifyField = "responseBreachNotifiedAt";
    private const string ResolveNotifyField = "resolveBreachNotifiedAt";
    private const int MaxCandidates = 500;

    private readonly IMngDataGatewayClient _dg;
    private readonly IWorkItemCommandService _workItemCommand;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<SlaBreachScanService> _logger;

    public SlaBreachScanService(
        IMngDataGatewayClient dg,
        IWorkItemCommandService workItemCommand,
        IRequestContext requestContext,
        ILogger<SlaBreachScanService> logger)
    {
        _dg = dg;
        _workItemCommand = workItemCommand;
        _requestContext = requestContext;
        _logger = logger;
    }

    public async Task<SlaBreachScanResponse> ScanWorkspaceAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        RequireManagerOrAdmin();

        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new OperationCoreException(
                "VALIDATION_ERROR",
                "workspaceId is required.",
                "workspaceId zorunludur.",
                400);
        }

        var token = RequireBearerToken();
        var asOf = DateTime.UtcNow;

        var candidates = await LoadOpenWorkItemsAsync(workspaceId.Trim(), token, cancellationToken);

        var responseRows = candidates
            .Where(row => IsSlaDueBreached(row, asOf, dueField: "responseDueAt", ResponseNotifyField))
            .ToList();

        var resolveRows = candidates
            .Where(row => IsSlaDueBreached(row, asOf, dueField: "resolveDueAt", ResolveNotifyField))
            .ToList();

        var processedIds = new List<string>();
        var responseCount = 0;
        var resolveCount = 0;

        foreach (var row in responseRows)
        {
            if (IsAlreadyNotified(row, ResponseNotifyField))
                continue;

            var workItemId = WorkItemDataHelper.GetDataId(row);
            if (string.IsNullOrWhiteSpace(workItemId))
                continue;

            _logger.LogInformation(
                "SLA response breach detected for work item {WorkItemId} in workspace {WorkspaceId}",
                workItemId,
                workspaceId);

            await _workItemCommand.RunAutomationRulesAsync(
                workItemId,
                RuleTriggers.WorkItemSlaResponseBreached,
                cancellationToken);

            await MarkBreachNotifiedAsync(
                row,
                ResponseNotifyField,
                breachFlagField: "responseBreached",
                asOf,
                token,
                cancellationToken);

            processedIds.Add(workItemId);
            responseCount++;
        }

        foreach (var row in resolveRows)
        {
            if (IsAlreadyNotified(row, ResolveNotifyField))
                continue;

            var workItemId = WorkItemDataHelper.GetDataId(row);
            if (string.IsNullOrWhiteSpace(workItemId))
                continue;

            if (processedIds.Contains(workItemId, StringComparer.Ordinal))
                continue;

            _logger.LogInformation(
                "SLA resolve breach detected for work item {WorkItemId} in workspace {WorkspaceId}",
                workItemId,
                workspaceId);

            await _workItemCommand.RunAutomationRulesAsync(
                workItemId,
                RuleTriggers.WorkItemSlaResolveBreached,
                cancellationToken);

            await MarkBreachNotifiedAsync(
                row,
                ResolveNotifyField,
                breachFlagField: "resolveBreached",
                asOf,
                token,
                cancellationToken);

            processedIds.Add(workItemId);
            resolveCount++;
        }

        return new SlaBreachScanResponse
        {
            WorkspaceId = workspaceId.Trim(),
            ScannedAtUtc = asOf,
            ResponseBreachesProcessed = responseCount,
            ResolveBreachesProcessed = resolveCount,
            WorkItemIds = processedIds
        };
    }

    private async Task<IReadOnlyList<Dictionary<string, object?>>> LoadOpenWorkItemsAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken)
    {
        var filter =
            $"filter=workspaceId:eq:{Uri.EscapeDataString(workspaceId)}," +
            $"closedAt:null&limit={MaxCandidates}";

        var rows = await _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.WorkItems,
            filter,
            token,
            cancellationToken);

        return rows.ToList();
    }

    private static bool IsSlaDueBreached(
        IReadOnlyDictionary<string, object?> row,
        DateTime asOfUtc,
        string dueField,
        string notifyField)
    {
        if (IsAlreadyNotified(row, notifyField))
            return false;

        var sla = CloneSlaDict(row);
        var dueAt = WorkItemDataHelper.GetDateTime(sla, dueField);
        return dueAt.HasValue && dueAt.Value < asOfUtc;
    }

    private async Task MarkBreachNotifiedAsync(
        IReadOnlyDictionary<string, object?> row,
        string notifyField,
        string breachFlagField,
        DateTime notifiedAt,
        string token,
        CancellationToken cancellationToken)
    {
        var workItemId = WorkItemDataHelper.GetDataId(row);
        var sla = CloneSlaDict(row);
        sla[notifyField] = notifiedAt;
        sla[breachFlagField] = true;
        sla["calculatedAt"] = notifiedAt;

        await _dg.UpdateAsync(
            OcDatasets.WorkItems,
            workItemId,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["sla"] = sla },
            token,
            cancellationToken);
    }

    private static bool IsAlreadyNotified(IReadOnlyDictionary<string, object?> row, string notifyField)
    {
        var sla = CloneSlaDict(row);
        return WorkItemDataHelper.GetDateTime(sla, notifyField).HasValue;
    }

    private static Dictionary<string, object?> CloneSlaDict(IReadOnlyDictionary<string, object?> row)
    {
        if (!row.TryGetValue("sla", out var slaRaw) || slaRaw == null)
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (slaRaw is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(el.GetRawText())
                ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        if (slaRaw is Dictionary<string, object?> dict)
            return new Dictionary<string, object?>(dict, StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private void RequireManagerOrAdmin()
    {
        if (_requestContext.IsAdmin || _requestContext.IsManager)
            return;

        throw new OperationCoreException(
            "FORBIDDEN",
            "Only domain managers can scan SLA breaches.",
            "SLA ihlali taramasını yalnızca domain yöneticileri çalıştırabilir.",
            403);
    }

    private string RequireBearerToken()
    {
        if (string.IsNullOrWhiteSpace(_requestContext.BearerToken))
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
