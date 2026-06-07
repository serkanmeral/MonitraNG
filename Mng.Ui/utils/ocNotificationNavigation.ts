import type { OcNotification } from '@/types/apps/operationCore';

export const OC_ALARM_NOTIFICATION_TYPES = new Set([
  'AlarmRaised',
  'AlarmUpdated',
  'AlarmResolved',
]);

/** Bildirim tıklanınca gidilecek iç rota (work item profil veya alarm detayı). */
export function resolveNotificationNavigationTarget(item: OcNotification): string | null {
  const deepLink = item.deepLink?.trim();
  if (deepLink) return deepLink;

  const workItemId = item.workItemId?.trim();
  if (workItemId) {
    return `/apps/operation-core/work-items/${encodeURIComponent(workItemId)}/profile`;
  }

  const dataset = (item.sourceDataset ?? '').trim().toLowerCase();
  const recordId = item.sourceRecordId?.trim();
  if (dataset === 'alarms' && recordId) {
    const params = new URLSearchParams({ alarmId: recordId });
    if (item.notificationType === 'AlarmResolved') {
      params.set('view', 'history');
    }
    return `/apps/alarm-center/alarms?${params.toString()}`;
  }

  return null;
}

export function isAlarmNotification(item: OcNotification): boolean {
  const type = (item.notificationType ?? '').trim();
  if (OC_ALARM_NOTIFICATION_TYPES.has(type)) return true;
  return (item.sourceDataset ?? '').trim().toLowerCase() === 'alarms';
}

export function notificationTypeIcon(type?: string | null): string {
  switch (type) {
    case 'CommentMention':
      return 'mdi-at';
    case 'WorkItemAssigned':
      return 'mdi-account-arrow-right';
    case 'AlarmRaised':
      return 'mdi-bell-alert-outline';
    case 'AlarmUpdated':
      return 'mdi-bell-ring-outline';
    case 'AlarmResolved':
      return 'mdi-bell-check-outline';
    default:
      return 'mdi-bell-outline';
  }
}

export function notificationTypeColor(type?: string | null): string {
  switch (type) {
    case 'CommentMention':
      return 'primary';
    case 'WorkItemAssigned':
      return 'info';
    case 'AlarmRaised':
      return 'warning';
    case 'AlarmUpdated':
      return 'orange';
    case 'AlarmResolved':
      return 'success';
    default:
      return 'secondary';
  }
}
