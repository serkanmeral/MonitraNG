namespace MngNotifier.Application.DTOs;

public sealed class MailDeliveryLogEntry
{
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string? DomainName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public sealed class MailDeliveryLogItem
{
    public DateTime SentAt { get; set; }
    public string Subject { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public sealed class MailDeliveryLogPage
{
    public long Total { get; set; }
    public List<MailDeliveryLogItem> Items { get; set; } = new();
}

public sealed class MailDeliveryLogQuery
{
    public int Skip { get; init; }
    public int Take { get; init; } = 25;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; } = true;
    public string? Search { get; init; }
    public bool? Success { get; init; }
}
