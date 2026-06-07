using System.Text.Json.Serialization;

namespace MngOperations.Application.Models;

public sealed class InAppNotificationTemplateRecord : DgRecord
{
    [JsonPropertyName("templateKey")]
    public string? TemplateKey { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Inbox + toaster başlığı (düz metin).</summary>
    public string? Title { get; set; }

    /// <summary>Inbox + toaster gövdesi (düz metin).</summary>
    public string? Message { get; set; }

    [JsonPropertyName("defaultToastSeverity")]
    public string? DefaultToastSeverity { get; set; }

    public string? Locale { get; set; }

    public string? Category { get; set; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }
}
