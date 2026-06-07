namespace MngOperations.Application.Contracts.Notifications;

/// <summary>
/// In-app bildirim (op_notifications) — geçerli kullanıcıya ait tek kayıt.
/// </summary>
public sealed class NotificationDto
{
    public required string Id { get; init; }
    public string? NotificationType { get; init; }
    public string? Title { get; init; }
    public string? Message { get; init; }
    public bool IsRead { get; init; }
    public string? WorkItemId { get; init; }
    public string? WorkItemKey { get; init; }
    public string? SourceDataset { get; init; }
    public string? SourceRecordId { get; init; }
    public string? DeepLink { get; init; }
    public DateTime? CreatedAt { get; init; }
}

/// <summary>Geçerli kullanıcının bildirim sayfası + filtre sonrası toplam + okunmamış sayısı.</summary>
public sealed class NotificationListResponse
{
    public IReadOnlyList<NotificationDto> Items { get; init; } = Array.Empty<NotificationDto>();
    public int Skip { get; init; }
    public int Take { get; init; }
    public long Total { get; init; }
    public long UnreadCount { get; init; }
}
