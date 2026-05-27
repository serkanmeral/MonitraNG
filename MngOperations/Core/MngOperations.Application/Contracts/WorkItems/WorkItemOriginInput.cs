using System.Text.Json;

namespace MngOperations.Application.Contracts.WorkItems;

public sealed class WorkItemOriginInput
{
    public required string SourceType { get; init; }
    public required string SourceId { get; init; }
    public required string CorrelationId { get; init; }
    public string? SourceSystem { get; init; }
    public JsonElement? Payload { get; init; }
}
