using System.Text.Json;

namespace MngOperations.Application.Contracts.WorkItems;

public sealed class PatchWorkItemRequest
{
    public string? Title { get; init; }

    // Nullable scalar core alanlar JsonElement? olarak tutulur: "absent" (HasValue==false → değişmedi)
    // ile "explicit null" (ValueKind==Null → temizle) ayrımı yapılabilsin. string? ile bu ayrım imkansızdı,
    // dolayısıyla edit'te bu alanlar boşaltılamıyordu (bkz. PatchAsync).
    public JsonElement? Description { get; init; }
    public JsonElement? Assignee { get; init; }
    public JsonElement? PriorityId { get; init; }
    public JsonElement? BoardId { get; init; }
    public JsonElement? Fields { get; init; }
}
