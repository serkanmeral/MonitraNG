import { fetchFromOperations } from '@/services/apiService';
import { pickStr } from '@/services/operationCoreService';
import type { OcNotification, OcNotificationListResponse } from '@/types/apps/operationCore';

function mapNotification(raw: Record<string, unknown>): OcNotification {
  return {
    id: pickStr(raw, 'id', 'Id') ?? '',
    notificationType: pickStr(raw, 'notificationType', 'NotificationType') ?? null,
    title: pickStr(raw, 'title', 'Title') ?? null,
    message: pickStr(raw, 'message', 'Message') ?? null,
    isRead: Boolean(raw.isRead ?? raw.IsRead ?? false),
    workItemId: pickStr(raw, 'workItemId', 'WorkItemId') ?? null,
    workItemKey: pickStr(raw, 'workItemKey', 'WorkItemKey') ?? null,
    sourceDataset: pickStr(raw, 'sourceDataset', 'SourceDataset') ?? null,
    sourceRecordId: pickStr(raw, 'sourceRecordId', 'SourceRecordId') ?? null,
    createdAt: pickStr(raw, 'createdAt', 'CreatedAt') ?? null,
  };
}

/** Geçerli kullanıcının in-app bildirimleri (op_notifications, en yeni önce). */
export async function ocGetNotifications(options?: {
  skip?: number;
  take?: number;
  unreadOnly?: boolean;
}): Promise<OcNotificationListResponse> {
  const qs = new URLSearchParams({
    skip: String(options?.skip ?? 0),
    take: String(options?.take ?? 20),
    unreadOnly: String(options?.unreadOnly ?? false),
  });
  const raw = (await fetchFromOperations(
    `/api/v1/notifications?${qs.toString()}`,
    'GET'
  )) as Record<string, unknown>;
  const items = raw.items ?? raw.Items;
  return {
    items: Array.isArray(items) ? items.map((i) => mapNotification(i as Record<string, unknown>)) : [],
    skip: Number(raw.skip ?? raw.Skip ?? 0),
    take: Number(raw.take ?? raw.Take ?? 0),
    total: Number(raw.total ?? raw.Total ?? 0),
    unreadCount: Number(raw.unreadCount ?? raw.UnreadCount ?? 0),
  };
}

/** Tek bildirimi okundu işaretler. */
export async function ocMarkNotificationRead(notificationId: string): Promise<void> {
  await fetchFromOperations(
    `/api/v1/notifications/${encodeURIComponent(notificationId)}/read`,
    'POST'
  );
}

/** Tüm okunmamış bildirimleri okundu işaretler; işaretlenen sayısını döner. */
export async function ocMarkAllNotificationsRead(): Promise<number> {
  const raw = (await fetchFromOperations(
    '/api/v1/notifications/read-all',
    'POST'
  )) as Record<string, unknown>;
  return Number(raw?.marked ?? 0);
}
