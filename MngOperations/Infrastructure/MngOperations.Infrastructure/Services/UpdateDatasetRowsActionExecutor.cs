using Microsoft.Extensions.Logging;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Rules;
using MngOperations.Application.Utilities;

namespace MngOperations.Infrastructure.Services;

public sealed class UpdateDatasetRowsActionExecutor : IUpdateDatasetRowsActionExecutor
{
    private readonly IMngDataGatewayClient _dg;
    private readonly ILogger<UpdateDatasetRowsActionExecutor> _logger;

    public UpdateDatasetRowsActionExecutor(
        IMngDataGatewayClient dg,
        ILogger<UpdateDatasetRowsActionExecutor> logger)
    {
        _dg = dg;
        _logger = logger;
    }

    public async Task<UpdateDatasetRowsResult> ExecuteAsync(
        IReadOnlyDictionary<string, object?> payload,
        IReadOnlyDictionary<string, object?> workItem,
        string workItemId,
        string workItemKey,
        string token,
        CancellationToken cancellationToken = default)
    {
        var action = UpdateDatasetRowsPlanner.ResolveActionElement(payload);
        var dataset = UpdateDatasetRowsPlanner.GetString(action, "dataset");
        if (string.IsNullOrWhiteSpace(dataset))
        {
            throw new OperationCoreException(
                "UPDATE_DATASET_ROWS_INVALID",
                "updateDatasetRows.dataset is required.",
                "dataset zorunludur.",
                400);
        }

        var onError = UpdateDatasetRowsPlanner.GetString(action, "onError") ?? "failTransition";
        var failHard = !string.Equals(onError, "continue", StringComparison.OrdinalIgnoreCase);

        try
        {
            var updates = UpdateDatasetRowsPlanner.BuildUpdates(action, workItem, workItemId, workItemKey);
            if (updates.Count == 0)
            {
                throw new OperationCoreException(
                    "UPDATE_DATASET_ROWS_EMPTY",
                    "No rows to update.",
                    "Güncellenecek satır yok.",
                    400);
            }

            if (updates.Count > UpdateDatasetRowsPlanner.DefaultMaxRows)
            {
                throw new OperationCoreException(
                    "UPDATE_DATASET_ROWS_LIMIT",
                    $"Row count {updates.Count} exceeds max {UpdateDatasetRowsPlanner.DefaultMaxRows}.",
                    $"Satır sayısı ({updates.Count}) üst sınırı ({UpdateDatasetRowsPlanner.DefaultMaxRows}) aşıyor.",
                    400);
            }

            var updatedIds = new List<string>(updates.Count);
            foreach (var (targetId, patch) in updates)
            {
                var existing = await _dg.GetByIdAsync<Dictionary<string, object?>>(
                    dataset,
                    targetId,
                    token,
                    cancellationToken,
                    expand: false);

                if (existing is null || existing.Count == 0)
                {
                    throw new OperationCoreException(
                        "UPDATE_DATASET_ROWS_NOT_FOUND",
                        $"Row '{targetId}' not found in dataset '{dataset}'.",
                        $"Dataset '{dataset}' içinde satır bulunamadı: {targetId}",
                        400);
                }

                foreach (var (key, value) in patch)
                    existing[key] = value;

                // DG PUT expects document body without identity keys colliding with route id.
                existing.Remove("__dataId");
                existing.Remove("dataId");

                await _dg.UpdateAsync(dataset, targetId, existing, token, cancellationToken);
                updatedIds.Add(targetId);
            }

            _logger.LogInformation(
                "updateDatasetRows updated {Count} row(s) in {Dataset} for workItem={WorkItemKey}",
                updatedIds.Count,
                dataset,
                workItemKey);

            return new UpdateDatasetRowsResult
            {
                UpdatedCount = updatedIds.Count,
                UpdatedIds = updatedIds,
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
                "updateDatasetRows failed (continue) workItem={WorkItemKey}",
                workItemKey);
            return new UpdateDatasetRowsResult { Dataset = dataset };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "updateDatasetRows failed for workItem={WorkItemKey}", workItemKey);
            throw new OperationCoreException(
                "UPDATE_DATASET_ROWS_FAILED",
                $"Failed to update dataset rows: {ex.Message}",
                $"Dataset satırları güncellenemedi: {ex.Message}",
                400);
        }
    }
}
