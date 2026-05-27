using Microsoft.Extensions.Logging;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public class WorkItemKeyGenerator : IWorkItemKeyGenerator
{
    private readonly IMngDataGatewayClient _dg;
    private readonly ILogger<WorkItemKeyGenerator> _logger;

    public WorkItemKeyGenerator(IMngDataGatewayClient dg, ILogger<WorkItemKeyGenerator> logger)
    {
        _dg = dg;
        _logger = logger;
    }

    public async Task<string> GenerateNextKeyAsync(
        WorkspaceRecord workspace,
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var prefix = workspace.WorkItemKeyPrefix?.Trim();
        if (string.IsNullOrEmpty(prefix))
        {
            throw new OperationCoreException(
                "WORKSPACE_KEY_PREFIX_MISSING",
                "Workspace workItemKeyPrefix is required before creating work items.",
                "WorkItem oluşturmak için workspace workItemKeyPrefix tanımlı olmalıdır.",
                400);
        }

        var format = string.IsNullOrWhiteSpace(workspace.WorkItemKeyFormat)
            ? WorkItemKeyFormat.DefaultFormat
            : workspace.WorkItemKeyFormat.Trim();

        var sequenceStart = workspace.WorkItemSequenceStart is > 0 ? workspace.WorkItemSequenceStart.Value : 1;
        var filter =
            $"workspaceId:eq:{workspaceId},key:startsWith:{prefix}-";
        var query = $"filter={Uri.EscapeDataString(filter)}&fields=key&limit=1000";

        var existing = await _dg.GetAsync<WorkItemKeyRecord>(OcDatasets.WorkItems, query, token, cancellationToken);
        var maxSeq = existing
            .Select(x => x.Key)
            .Where(k => !string.IsNullOrEmpty(k))
            .Select(k => WorkItemKeyFormat.ParseSequence(k!, prefix))
            .DefaultIfEmpty(0)
            .Max();

        var next = maxSeq > 0 ? maxSeq + 1 : sequenceStart;
        var key = WorkItemKeyFormat.Apply(format, prefix, next);

        _logger.LogDebug("Generated work item key {Key} for workspace {WorkspaceId}", key, workspaceId);
        return key;
    }
}
