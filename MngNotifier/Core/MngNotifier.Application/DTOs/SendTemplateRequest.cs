using System.Text.Json;

namespace MngNotifier.Application.DTOs;

public class SendTemplateRequest
{
    public List<string> To { get; set; } = new();
    public List<string>? Cc { get; set; }
    public MailAddressDto? From { get; set; }
    public required string TemplateKey { get; set; }
    /// <summary>Boş/null ise template subject kullanılır (placeholder render).</summary>
    public string? Subject { get; set; }
    public JsonElement Context { get; set; }

    /// <summary>Tenant name. When empty, domain_name is read from the Bearer token.</summary>
    public string? DomainName { get; set; }
}
