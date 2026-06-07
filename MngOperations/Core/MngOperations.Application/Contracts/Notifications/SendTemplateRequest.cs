using System.Text.Json;

namespace MngOperations.Application.Contracts.Notifications;

public sealed class SendTemplateRequest
{
    public List<string> To { get; init; } = new();
    public List<string>? Cc { get; init; }
    public required string TemplateKey { get; init; }
    /// <summary>Boş ise Notifier template subject kullanır.</summary>
    public string? Subject { get; init; }
    public JsonElement Context { get; init; }
}
