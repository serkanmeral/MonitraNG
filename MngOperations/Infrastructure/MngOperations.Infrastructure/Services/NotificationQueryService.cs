using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Notifications;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

/// <summary>
/// op_notifications okuma/okundu işaretleme. Kapsam daima JWT'deki <see cref="IRequestContext.UserId"/>;
/// match koşulu user'a sabitlenir, mark işlemleri sahiplik doğrular.
/// </summary>
public class NotificationQueryService : INotificationQueryService
{
    private const int MarkAllBatchLimit = 500;

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<NotificationQueryService> _logger;

    public NotificationQueryService(
        IMngDataGatewayClient dg,
        IRequestContext requestContext,
        ILogger<NotificationQueryService> logger)
    {
        _dg = dg;
        _requestContext = requestContext;
        _logger = logger;
    }

    public async Task<NotificationListResponse> GetForCurrentUserAsync(
        int skip,
        int take,
        bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        var (userIds, token) = RequireUser();
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);

        var match = BuildMatch(userIds, unreadOnly);

        var query = string.Join("&", new[]
        {
            "sort=-createdAt",
            $"skip={skip}",
            $"limit={take}",
            "expand=false"
        });

        var page = await _dg.QueryPageAsync(OcDatasets.Notifications, match, query, token, cancellationToken);
        var items = page.Items.Select(MapNotification).ToList();

        // Okunmamış sayısı badge için: unreadOnly ise zaten filtreli toplam, değilse ayrı sayım sorgusu.
        long unreadCount;
        if (unreadOnly)
        {
            unreadCount = page.Total;
        }
        else
        {
            var unreadPage = await _dg.QueryPageAsync(
                OcDatasets.Notifications,
                BuildMatch(userIds, unreadOnly: true),
                "limit=1&expand=false",
                token,
                cancellationToken);
            unreadCount = unreadPage.Total;
        }

        return new NotificationListResponse
        {
            Items = items,
            Skip = skip,
            Take = take,
            Total = page.Total,
            UnreadCount = unreadCount
        };
    }

    public async Task MarkReadAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        var (userIds, token) = RequireUser();
        if (string.IsNullOrWhiteSpace(notificationId))
            throw NotFound();

        var record = await _dg.GetByIdAsync<Dictionary<string, object?>>(
            OcDatasets.Notifications, notificationId, token, cancellationToken);

        var ownerId = record == null ? null : WorkItemDataHelper.GetString(record, "userId");
        if (record == null
            || ownerId == null
            || !userIds.Contains(ownerId, StringComparer.OrdinalIgnoreCase))
        {
            throw NotFound();
        }

        if (WorkItemDataHelper.GetBool(record, "isRead") == true)
            return;

        await MarkSingleAsync(notificationId, token, cancellationToken);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var (userIds, token) = RequireUser();

        var page = await _dg.QueryPageAsync(
            OcDatasets.Notifications,
            BuildMatch(userIds, unreadOnly: true),
            $"limit={MarkAllBatchLimit}&expand=false",
            token,
            cancellationToken);

        var marked = 0;
        foreach (var row in page.Items)
        {
            var id = WorkItemDataHelper.GetDataId(row);
            if (string.IsNullOrEmpty(id))
                continue;

            try
            {
                await MarkSingleAsync(id, token, cancellationToken);
                marked++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Mark-all-read failed for notification {NotificationId}", id);
            }
        }

        return marked;
    }

    private Task MarkSingleAsync(string id, string token, CancellationToken cancellationToken) =>
        _dg.UpdateAsync(
            OcDatasets.Notifications,
            id,
            new Dictionary<string, object?>
            {
                ["isRead"] = true,
                ["readAt"] = DateTime.UtcNow.ToString("o")
            },
            token,
            cancellationToken);

    private static Dictionary<string, object?> BuildMatch(IReadOnlyList<string> userIds, bool unreadOnly)
    {
        // Bildirimler person picker kimliğiyle (mng_person_id) yazılır; eski/karışık kayıtlar için
        // sub de adaylara dahil edilir. Tekil id'de düz eşitlik, çoklu id'de $in.
        var match = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["userId"] = userIds.Count == 1
                ? userIds[0]
                : new Dictionary<string, object?> { ["$in"] = userIds.Cast<object?>().ToList() }
        };
        if (unreadOnly)
            match["isRead"] = new Dictionary<string, object?> { ["$ne"] = true };
        return match;
    }

    private static NotificationDto MapNotification(Dictionary<string, object?> row) => new()
    {
        Id = WorkItemDataHelper.GetDataId(row),
        NotificationType = WorkItemDataHelper.GetString(row, "notificationType"),
        Title = WorkItemDataHelper.GetString(row, "title"),
        Message = WorkItemDataHelper.GetString(row, "message"),
        IsRead = WorkItemDataHelper.GetBool(row, "isRead") ?? false,
        WorkItemId = WorkItemDataHelper.GetString(row, "workItemId"),
        WorkItemKey = WorkItemDataHelper.GetString(row, "workItemKey"),
        SourceDataset = WorkItemDataHelper.GetString(row, "sourceDataset"),
        SourceRecordId = WorkItemDataHelper.GetString(row, "sourceRecordId"),
        CreatedAt = WorkItemDataHelper.GetDateTime(row, "createdAt")
    };

    private (IReadOnlyList<string> UserIds, string Token) RequireUser()
    {
        if (string.IsNullOrEmpty(_requestContext.BearerToken)
            || (string.IsNullOrEmpty(_requestContext.MngPersonId) && string.IsNullOrEmpty(_requestContext.UserId)))
        {
            throw new OperationCoreException(
                "UNAUTHORIZED",
                "Authenticated user is required.",
                "Oturum açmış kullanıcı gerekli.",
                401);
        }

        // Bildirim alıcıları person picker kimliğiyle (mng_person_id) yazılır; sub yedek olarak eklenir.
        var ids = new[] { _requestContext.MngPersonId, _requestContext.UserId }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (ids, _requestContext.BearerToken!);
    }

    private static OperationCoreException NotFound() =>
        new("NOTIFICATION_NOT_FOUND", "Notification not found.", "Bildirim bulunamadı.", 404);
}
