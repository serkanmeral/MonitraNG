namespace MngNotifier.Application.DTOs;

/// <summary>
/// Chat Room (DG <c>cht_messages</c>) mention bildirimi — MngDataGateway iç çağrısı (MVP).
/// </summary>
public class ChatMentionNotifyRequest
{
    /// <summary>Tenant domain adı (JWT <c>domain_name</c> ile uyumlu).</summary>
    public string DomainName { get; set; } = string.Empty;

    /// <summary>Oluşturulan mesaj <c>__dataId</c>.</summary>
    public string DataId { get; set; } = string.Empty;

    /// <summary>Mention yapılan Keeper kişi id'leri (yazar hariç, tekil).</summary>
    public List<string> TargetPersonIds { get; set; } = new();

    /// <summary>Mesajı yazan (<c>authorPersonId</c>).</summary>
    public string ActorPersonId { get; set; } = string.Empty;

    /// <summary>İsteğe bağlı kısa önizleme (log / ileride e-posta).</summary>
    public string? BodyPreview { get; set; }

    /// <summary>Kaynak sabiti; varsayılan <c>cht_messages</c>.</summary>
    public string Source { get; set; } = "cht_messages";
}
