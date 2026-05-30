using MngOperations.Application.Contracts.Notifications;

namespace MngOperations.Application.Interfaces;

/// <summary>
/// Geçerli kullanıcının in-app bildirimlerini (op_notifications) okur ve okundu işaretler.
/// Kapsam daima <see cref="IRequestContext.UserId"/> ile sınırlıdır (kullanıcı başkasının bildirimini göremez/değiştiremez).
/// </summary>
public interface INotificationQueryService
{
    Task<NotificationListResponse> GetForCurrentUserAsync(
        int skip,
        int take,
        bool unreadOnly,
        CancellationToken cancellationToken = default);

    /// <summary>Tek bildirimi okundu işaretler; kayıt geçerli kullanıcıya ait değilse 404.</summary>
    Task MarkReadAsync(string notificationId, CancellationToken cancellationToken = default);

    /// <summary>Geçerli kullanıcının tüm okunmamış bildirimlerini okundu işaretler; işaretlenen sayısını döner.</summary>
    Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default);
}
