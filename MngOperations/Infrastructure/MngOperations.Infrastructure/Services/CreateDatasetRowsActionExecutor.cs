using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Rules;
using MngOperations.Application.Utilities;

namespace MngOperations.Infrastructure.Services;

public sealed class CreateDatasetRowsActionExecutor : ICreateDatasetRowsActionExecutor
{
    private readonly IMngDataGatewayClient _dg;
    private readonly ILogger<CreateDatasetRowsActionExecutor> _logger;

    public CreateDatasetRowsActionExecutor(
        IMngDataGatewayClient dg,
        ILogger<CreateDatasetRowsActionExecutor> logger)
    {
        _dg = dg;
        _logger = logger;
    }

    public async Task<CreateDatasetRowsResult> ExecuteAsync(
        IReadOnlyDictionary<string, object?> payload,
        IReadOnlyDictionary<string, object?> workItem,
        string workItemId,
        string workItemKey,
        string token,
        CancellationToken cancellationToken = default)
    {
        var action = CreateDatasetRowsPlanner.ResolveActionElement(payload);
        var dataset = CreateDatasetRowsPlanner.GetString(action, "dataset");
        if (string.IsNullOrWhiteSpace(dataset))
        {
            throw new OperationCoreException(
                "CREATE_DATASET_ROWS_INVALID",
                "createDatasetRows.dataset is required.",
                "dataset zorunludur.",
                400);
        }

        var onError = CreateDatasetRowsPlanner.GetString(action, "onError") ?? "failTransition";
        var failHard = !string.Equals(onError, "continue", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (await IsIdempotentSkipAsync(action, workItem, workItemId, workItemKey, dataset, token, cancellationToken))
            {
                _logger.LogInformation(
                    "createDatasetRows skipped (idempotent) dataset={Dataset} workItem={WorkItemKey}",
                    dataset,
                    workItemKey);
                return new CreateDatasetRowsResult
                {
                    SkippedIdempotent = true,
                    Dataset = dataset
                };
            }

            var rows = CreateDatasetRowsPlanner.BuildRows(action, workItem, workItemId, workItemKey);
            if (rows.Count == 0)
            {
                throw new OperationCoreException(
                    "CREATE_DATASET_ROWS_EMPTY",
                    "No rows to create.",
                    "Oluşturulacak satır yok.",
                    400);
            }

            if (rows.Count > CreateDatasetRowsPlanner.DefaultMaxRows)
            {
                throw new OperationCoreException(
                    "CREATE_DATASET_ROWS_LIMIT",
                    $"Row count {rows.Count} exceeds max {CreateDatasetRowsPlanner.DefaultMaxRows}.",
                    $"Satır sayısı ({rows.Count}) üst sınırı ({CreateDatasetRowsPlanner.DefaultMaxRows}) aşıyor.",
                    400);
            }

            var createdIds = new List<string>(rows.Count);
            foreach (var row in rows)
            {
                var created = await _dg.CreateAsync(dataset, row, token, cancellationToken);
                var id = WorkItemDataHelper.GetDataId(created);
                if (!string.IsNullOrWhiteSpace(id))
                    createdIds.Add(id);
            }

            _logger.LogInformation(
                "createDatasetRows created {Count} row(s) in {Dataset} for workItem={WorkItemKey}",
                createdIds.Count,
                dataset,
                workItemKey);

            return new CreateDatasetRowsResult
            {
                CreatedCount = createdIds.Count,
                CreatedIds = createdIds,
                Dataset = dataset
            };
        }
        catch (OperationCoreException)
        {
            throw;
        }
        catch (Exception ex) when (!failHard)
        {
            _logger.LogWarning(
                ex,
                "createDatasetRows failed (continue) workItem={WorkItemKey}",
                workItemKey);
            return new CreateDatasetRowsResult { Dataset = dataset };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "createDatasetRows failed for workItem={WorkItemKey}", workItemKey);
            throw new OperationCoreException(
                "CREATE_DATASET_ROWS_FAILED",
                $"Failed to create dataset rows: {ex.Message}",
                $"Dataset satırları oluşturulamadı: {ex.Message}",
                400);
        }
    }

    private async Task<bool> IsIdempotentSkipAsync(
        JsonElement action,
        IReadOnlyDictionary<string, object?> workItem,
        string workItemId,
        string workItemKey,
        string dataset,
        string token,
        CancellationToken cancellationToken)
    {
        if (!action.TryGetProperty("idempotency", out var idem)
            || idem.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var mode = CreateDatasetRowsPlanner.GetString(idem, "mode") ?? "none";
        if (!string.Equals(mode, "one_per_source", StringComparison.OrdinalIgnoreCase))
            return false;

        var lookupField = CreateDatasetRowsPlanner.GetString(idem, "lookupField");
        var lookupFrom = CreateDatasetRowsPlanner.GetString(idem, "lookupFrom") ?? "key";
        if (string.IsNullOrWhiteSpace(lookupField))
            return false;

        var lookupValue = WorkItemPathValueResolver.Resolve(lookupFrom, workItem, workItemId, workItemKey);
        var lookupText = WorkItemPathValueResolver.FormatScalar(lookupValue);
        if (string.IsNullOrWhiteSpace(lookupText))
            return false;

        var filter = $"{lookupField}:eq:{lookupText}";
        var query = $"filter={Uri.EscapeDataString(filter)}&limit=1&expand=false";
        var existing = await _dg.GetAsync<Dictionary<string, object?>>(dataset, query, token, cancellationToken);
        return existing.Any();
    }
}
