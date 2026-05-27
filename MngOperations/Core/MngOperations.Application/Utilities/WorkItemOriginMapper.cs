using System.Text.Json;
using MngOperations.Application.Contracts.WorkItems;
using MngOperations.Application.Exceptions;

namespace MngOperations.Application.Utilities;

public static class WorkItemOriginMapper
{
    public static void Validate(WorkItemOriginInput origin)
    {
        if (string.IsNullOrWhiteSpace(origin.SourceType))
            throw new OperationCoreException("VALIDATION_ERROR", "origin.sourceType is required.", "origin.sourceType zorunludur.", 400);

        if (string.IsNullOrWhiteSpace(origin.SourceId))
            throw new OperationCoreException("VALIDATION_ERROR", "origin.sourceId is required.", "origin.sourceId zorunludur.", 400);

        if (string.IsNullOrWhiteSpace(origin.CorrelationId))
            throw new OperationCoreException("VALIDATION_ERROR", "origin.correlationId is required.", "origin.correlationId zorunludur.", 400);
    }

    public static Dictionary<string, object?> ToDictionary(WorkItemOriginInput origin)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceType"] = origin.SourceType.Trim(),
            ["sourceId"] = origin.SourceId.Trim(),
            ["correlationId"] = origin.CorrelationId.Trim()
        };

        if (!string.IsNullOrWhiteSpace(origin.SourceSystem))
            dict["sourceSystem"] = origin.SourceSystem.Trim();

        if (origin.Payload is { } payload
            && payload.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            dict["payload"] = JsonSerializer.Deserialize<object?>(payload.GetRawText());
        }

        return dict;
    }
}
