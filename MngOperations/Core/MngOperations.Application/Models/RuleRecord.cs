using System.Text.Json;
using System.Text.Json.Serialization;

namespace MngOperations.Application.Models;

public sealed class RuleRecord : DgRecord
{
    public string? Name { get; set; }
    public string? Description { get; set; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }

    [JsonPropertyName("boardId")]
    public string? BoardId { get; set; }

    [JsonPropertyName("typeId")]
    public string? TypeId { get; set; }

    [JsonPropertyName("stateId")]
    public string? StateId { get; set; }

    [JsonPropertyName("fromStateId")]
    public string? FromStateId { get; set; }

    [JsonPropertyName("toStateId")]
    public string? ToStateId { get; set; }

    [JsonPropertyName("transitionKey")]
    public string? TransitionKey { get; set; }

    [JsonPropertyName("ruleType")]
    public string? RuleType { get; set; }

    public string? Trigger { get; set; }

    [JsonPropertyName("applyMode")]
    public string? ApplyMode { get; set; }

    public JsonElement? Conditions { get; set; }
    public JsonElement? Actions { get; set; }
    public JsonElement? Validation { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("stopProcessing")]
    public bool? StopProcessing { get; set; }

    public int? Priority { get; set; }

    [JsonPropertyName("validFrom")]
    public DateTime? ValidFrom { get; set; }

    [JsonPropertyName("validTo")]
    public DateTime? ValidTo { get; set; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }
}
