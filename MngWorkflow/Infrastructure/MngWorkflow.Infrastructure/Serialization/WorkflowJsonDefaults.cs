using System.Text.Json;
using System.Text.Json.Serialization;

namespace MngWorkflow.Infrastructure.Serialization;

internal static class WorkflowJsonDefaults
{
    public static JsonSerializerOptions Message { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
