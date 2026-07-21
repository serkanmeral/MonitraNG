namespace MngKeeper.Application.DTOs;

public class TelegramLinkRequest
{
    public string DomainId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TelegramChatId { get; set; } = string.Empty;
    public string? TelegramUsername { get; set; }
}

public class TelegramLinkResponse
{
    public bool Linked { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? TelegramChatId { get; set; }
    public string? TelegramUsername { get; set; }
    public DateTime? TelegramLinkedAt { get; set; }
    public string? Error { get; set; }
}
