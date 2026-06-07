import {
  OC_DATASETS,
  ocCreateRecordId,
  ocDelete,
  ocListDataset,
  ocUpdate,
  resolveRelationId,
} from '@/services/operationCoreService';
import type { OpNotificationPolicy } from '@/types/apps/operationCore';

function parseStringArray(raw: unknown): string[] {
  if (raw == null) return [];
  if (Array.isArray(raw)) {
    return raw.map((v) => String(v).trim()).filter(Boolean);
  }
  if (typeof raw === 'string' && raw.trim()) return [raw.trim()];
  return [];
}

export function mapOpNotificationPolicy(raw: Record<string, unknown>): OpNotificationPolicy {
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? ''),
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId) ?? '',
    boardId: resolveRelationId(raw.boardId ?? raw.BoardId) || null,
    typeId: resolveRelationId(raw.typeId ?? raw.TypeId) || null,
    eventType: String(raw.eventType ?? raw.EventType ?? '').trim(),
    channels: parseStringArray(raw.channels ?? raw.Channels),
    recipients: parseStringArray(raw.recipients ?? raw.Recipients),
    emailTemplateKey:
      raw.emailTemplateKey != null
        ? String(raw.emailTemplateKey).trim() || null
        : raw.EmailTemplateKey != null
          ? String(raw.EmailTemplateKey).trim() || null
          : null,
    emailSubject:
      raw.emailSubject != null
        ? String(raw.emailSubject).trim() || null
        : raw.EmailSubject != null
          ? String(raw.EmailSubject).trim() || null
          : null,
    notificationTemplateKey:
      raw.notificationTemplateKey != null
        ? String(raw.notificationTemplateKey).trim() || null
        : raw.NotificationTemplateKey != null
          ? String(raw.NotificationTemplateKey).trim() || null
          : null,
    transitionKey:
      raw.transitionKey != null
        ? String(raw.transitionKey).trim() || null
        : raw.TransitionKey != null
          ? String(raw.TransitionKey).trim() || null
          : null,
    fromStateId: resolveRelationId(raw.fromStateId ?? raw.FromStateId) || null,
    toStateId: resolveRelationId(raw.toStateId ?? raw.ToStateId) || null,
    excludeActor: Boolean(raw.excludeActor ?? raw.ExcludeActor ?? false),
    isActive: raw.isActive !== false && raw.IsActive !== false,
    priority:
      raw.priority != null
        ? Number(raw.priority)
        : raw.Priority != null
          ? Number(raw.Priority)
          : 100,
    settings: parseSettings(raw.settings ?? raw.Settings),
  };
}

function parseSettings(raw: unknown): { pushToast?: boolean; toastSeverity?: string } | null {
  if (!raw || typeof raw !== 'object') return null;
  const settings = raw as Record<string, unknown>;
  const out: { pushToast?: boolean; toastSeverity?: string } = {};
  if (settings.pushToast === true) out.pushToast = true;
  if (settings.pushToast === false) out.pushToast = false;
  const severity = settings.toastSeverity != null ? String(settings.toastSeverity).trim().toLowerCase() : '';
  if (severity === 'info' || severity === 'success' || severity === 'warning' || severity === 'error') {
    out.toastSeverity = severity;
  }
  return Object.keys(out).length ? out : null;
}

export async function ocListNotificationPoliciesForWorkspace(
  workspaceId: string
): Promise<OpNotificationPolicy[]> {
  const rows = await ocListDataset(OC_DATASETS.notificationPolicies, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'priority:desc,name:asc',
    limit: 200,
  });
  return rows
    .map((r) => mapOpNotificationPolicy(r as Record<string, unknown>))
    .filter((p) => p.__dataId && p.name && p.workspaceId === workspaceId);
}

export async function ocCreateNotificationPolicy(payload: Record<string, unknown>): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.notificationPolicies, payload);
}

export async function ocUpdateNotificationPolicy(policyId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.notificationPolicies, policyId, payload);
}

export async function ocDeleteNotificationPolicy(policyId: string) {
  await ocDelete(OC_DATASETS.notificationPolicies, policyId);
}
