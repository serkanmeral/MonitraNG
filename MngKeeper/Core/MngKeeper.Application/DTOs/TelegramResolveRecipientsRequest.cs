namespace MngKeeper.Application.DTOs;

public class TelegramResolveRecipientsRequest
{
    public string DomainId { get; set; } = string.Empty;
    public List<string> UserIds { get; set; } = new();
}

public class TelegramResolveRecipientsResponse
{
    public List<string> ChatIds { get; set; } = new();
    public List<TelegramResolveRecipientItem> Results { get; set; } = new();
    public string? Error { get; set; }
}

public class TelegramResolveRecipientItem
{
    public string UserId { get; set; } = string.Empty;
    public string? TelegramChatId { get; set; }
    public string? TelegramUsername { get; set; }
    public bool HasChatId { get; set; }
}
