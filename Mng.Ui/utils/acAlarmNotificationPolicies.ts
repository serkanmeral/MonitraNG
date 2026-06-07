import type {
  AlarmNotificationPolicy,
  AlarmNotificationPolicySettings,
  AcToastSeverity,
  CreateAlarmNotificationPolicyRequest,
  UpdateAlarmNotificationPolicyRequest,
} from '@/types/apps/alarmNotificationPolicy';

export const AC_ALARM_NOTIFICATION_EVENT_TYPES = [
  'AlarmRaised',
  'AlarmUpdated',
  'AlarmResolved',
] as const;

export const AC_ALARM_NOTIFICATION_CHANNELS = ['inApp', 'email'] as const;

export const AC_TOAST_SEVERITIES = ['info', 'success', 'warning', 'error'] as const;

const DEFAULT_EMAIL_TEMPLATE_BY_EVENT: Record<string, string> = {
  AlarmRaised: 'alarm-raised',
  AlarmUpdated: 'alarm-raised',
  AlarmResolved: 'alarm-resolved',
};

export function defaultEmailTemplateKeyForEvent(eventType: string): string {
  return DEFAULT_EMAIL_TEMPLATE_BY_EVENT[eventType] ?? '';
}

export interface AcAlarmNotificationPolicyDraft {
  id?: string;
  name: string;
  description: string;
  eventType: string;
  ruleId: string | null;
  minSeverity: number | null;
  maxSeverity: number | null;
  channels: string[];
  recipientPersonIds: string[];
  emailTemplateKey: string;
  emailSubject: string;
  pushToast: boolean;
  toastSeverity: AcToastSeverity;
  cooldownMinutes: number | null;
  excludeAcknowledgedBy: boolean;
  priority: number;
  isActive: boolean;
}

export function newAlarmNotificationPolicyDraft(
  seed?: Partial<AcAlarmNotificationPolicyDraft>
): AcAlarmNotificationPolicyDraft {
  const eventType = seed?.eventType ?? 'AlarmRaised';
  return {
    name: '',
    description: '',
    eventType,
    ruleId: null,
    minSeverity: null,
    maxSeverity: null,
    channels: ['inApp'],
    recipientPersonIds: [],
    emailTemplateKey: defaultEmailTemplateKeyForEvent(eventType),
    emailSubject: '',
    pushToast: true,
    toastSeverity: 'warning',
    cooldownMinutes: null,
    excludeAcknowledgedBy: false,
    priority: 50,
    isActive: true,
    ...seed,
  };
}

function parseStringArray(raw: unknown): string[] {
  if (raw == null) return [];
  if (Array.isArray(raw)) return raw.map((v) => String(v).trim()).filter(Boolean);
  return [];
}

function parseToastSeverity(raw: unknown): AcToastSeverity | null {
  const value = raw != null ? String(raw).trim().toLowerCase() : '';
  return (AC_TOAST_SEVERITIES as readonly string[]).includes(value)
    ? (value as AcToastSeverity)
    : null;
}

function parseSettings(raw: unknown): AlarmNotificationPolicySettings | null {
  if (!raw || typeof raw !== 'object') return null;
  const obj = raw as Record<string, unknown>;
  return {
    pushToast: obj.pushToast === true || obj.PushToast === true,
    toastSeverity:
      parseToastSeverity(obj.toastSeverity ?? obj.ToastSeverity) ??
      (obj.toastSeverity != null ? String(obj.toastSeverity) : null),
  };
}

export function normalizeAlarmNotificationPolicy(raw: Record<string, unknown>): AlarmNotificationPolicy {
  const settings = parseSettings(raw.settings ?? raw.Settings);
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    domainId: String(raw.domainId ?? raw.DomainId ?? ''),
    domainName: String(raw.domainName ?? raw.DomainName ?? ''),
    name: String(raw.name ?? raw.Name ?? '').trim(),
    description:
      raw.description != null
        ? String(raw.description)
        : raw.Description != null
          ? String(raw.Description)
          : null,
    eventType: String(raw.eventType ?? raw.EventType ?? 'AlarmRaised'),
    ruleId:
      raw.ruleId != null
        ? String(raw.ruleId).trim() || null
        : raw.RuleId != null
          ? String(raw.RuleId).trim() || null
          : null,
    minSeverity:
      raw.minSeverity != null
        ? Number(raw.minSeverity)
        : raw.MinSeverity != null
          ? Number(raw.MinSeverity)
          : null,
    maxSeverity:
      raw.maxSeverity != null
        ? Number(raw.maxSeverity)
        : raw.MaxSeverity != null
          ? Number(raw.MaxSeverity)
          : null,
    channels: parseStringArray(raw.channels ?? raw.Channels),
    recipientPersonIds: parseStringArray(raw.recipientPersonIds ?? raw.RecipientPersonIds),
    emailTemplateKey:
      raw.emailTemplateKey != null
        ? String(raw.emailTemplateKey).trim() || null
        : raw.EmailTemplateKey != null
          ? String(raw.EmailTemplateKey).trim() || null
          : null,
    emailSubject:
      raw.emailSubject != null
        ? String(raw.emailSubject)
        : raw.EmailSubject != null
          ? String(raw.EmailSubject)
          : null,
    settings,
    cooldownMinutes:
      raw.cooldownMinutes != null
        ? Number(raw.cooldownMinutes)
        : raw.CooldownMinutes != null
          ? Number(raw.CooldownMinutes)
          : null,
    excludeAcknowledgedBy: raw.excludeAcknowledgedBy === true || raw.ExcludeAcknowledgedBy === true,
    priority:
      raw.priority != null
        ? Number(raw.priority)
        : raw.Priority != null
          ? Number(raw.Priority)
          : null,
    isActive: raw.isActive !== false && raw.IsActive !== false,
    createdAt: raw.createdAt != null ? String(raw.createdAt) : raw.CreatedAt != null ? String(raw.CreatedAt) : undefined,
    updatedAt: raw.updatedAt != null ? String(raw.updatedAt) : raw.UpdatedAt != null ? String(raw.UpdatedAt) : undefined,
  };
}

export function parseAlarmNotificationPolicyToDraft(
  policy: AlarmNotificationPolicy
): AcAlarmNotificationPolicyDraft {
  return {
    id: policy.id,
    name: policy.name,
    description: policy.description ?? '',
    eventType: policy.eventType || 'AlarmRaised',
    ruleId: policy.ruleId ?? null,
    minSeverity: policy.minSeverity ?? null,
    maxSeverity: policy.maxSeverity ?? null,
    channels: policy.channels.length ? [...policy.channels] : ['inApp'],
    recipientPersonIds: policy.recipientPersonIds.length ? [...policy.recipientPersonIds] : [],
    emailTemplateKey: policy.emailTemplateKey ?? defaultEmailTemplateKeyForEvent(policy.eventType),
    emailSubject: policy.emailSubject ?? '',
    pushToast: policy.settings?.pushToast !== false,
    toastSeverity: parseToastSeverity(policy.settings?.toastSeverity) ?? 'warning',
    cooldownMinutes: policy.cooldownMinutes ?? null,
    excludeAcknowledgedBy: policy.excludeAcknowledgedBy === true,
    priority: policy.priority ?? 50,
    isActive: policy.isActive !== false,
  };
}

export function validateAlarmNotificationPolicyDraft(draft: AcAlarmNotificationPolicyDraft): string | null {
  if (!draft.name.trim()) return 'name';
  if (!draft.eventType.trim()) return 'eventType';
  if (!draft.channels.length) return 'channels';
  if (!draft.recipientPersonIds.length) return 'recipientPersonIds';
  if (draft.channels.includes('email') && !draft.emailTemplateKey.trim()) return 'emailTemplateKey';
  if (draft.minSeverity != null && draft.maxSeverity != null && draft.minSeverity > draft.maxSeverity) {
    return 'severityRange';
  }
  return null;
}

export function alarmNotificationPolicySpecificityScore(policy: AlarmNotificationPolicy): number {
  let score = 0;
  if (policy.ruleId) score += 4;
  if (policy.minSeverity != null || policy.maxSeverity != null) score += 2;
  return score + (policy.priority ?? 0) / 100;
}

export function buildCreateAlarmNotificationPolicyPayload(
  draft: AcAlarmNotificationPolicyDraft
): CreateAlarmNotificationPolicyRequest {
  const wantsInApp = draft.channels.includes('inApp');
  return {
    name: draft.name.trim(),
    description: draft.description.trim() || null,
    eventType: draft.eventType.trim(),
    ruleId: draft.ruleId?.trim() || null,
    minSeverity: draft.minSeverity,
    maxSeverity: draft.maxSeverity,
    channels: [...draft.channels],
    recipientPersonIds: [...draft.recipientPersonIds],
    emailTemplateKey: draft.channels.includes('email') ? draft.emailTemplateKey.trim() || null : null,
    emailSubject: draft.emailSubject.trim() || null,
    settings: wantsInApp
      ? {
          pushToast: draft.pushToast,
          toastSeverity: draft.toastSeverity,
        }
      : null,
    cooldownMinutes: draft.cooldownMinutes,
    excludeAcknowledgedBy: draft.excludeAcknowledgedBy,
    priority: draft.priority,
    isActive: draft.isActive,
  };
}

export function buildUpdateAlarmNotificationPolicyPayload(
  draft: AcAlarmNotificationPolicyDraft
): UpdateAlarmNotificationPolicyRequest {
  const wantsInApp = draft.channels.includes('inApp');
  return {
    name: draft.name.trim(),
    description: draft.description.trim() || null,
    eventType: draft.eventType.trim(),
    ruleId: draft.ruleId?.trim() || null,
    minSeverity: draft.minSeverity,
    maxSeverity: draft.maxSeverity,
    channels: [...draft.channels],
    recipientPersonIds: [...draft.recipientPersonIds],
    emailTemplateKey: draft.channels.includes('email') ? draft.emailTemplateKey.trim() || null : null,
    emailSubject: draft.emailSubject.trim() || null,
    settings: wantsInApp
      ? {
          pushToast: draft.pushToast,
          toastSeverity: draft.toastSeverity,
        }
      : { pushToast: false, toastSeverity: null },
    cooldownMinutes: draft.cooldownMinutes,
    excludeAcknowledgedBy: draft.excludeAcknowledgedBy,
    priority: draft.priority,
    isActive: draft.isActive,
  };
}

export function formatAlarmNotificationChannelsSummary(
  channels: string[],
  t: (key: string) => string
): string {
  if (!channels.length) return '—';
  return channels
    .map((ch) => {
      const key = `alarmCenter.notificationPolicies.channels.${ch}`;
      const translated = t(key);
      return translated !== key ? translated : ch;
    })
    .join(', ');
}

export function formatAlarmNotificationSeverityRange(
  min: number | null | undefined,
  max: number | null | undefined,
  t: (key: string, params?: Record<string, unknown>) => string
): string {
  if (min == null && max == null) return t('alarmCenter.notificationPolicies.severityAny');
  if (min != null && max != null) {
    return t('alarmCenter.notificationPolicies.severityRange', { min, max });
  }
  if (min != null) return t('alarmCenter.notificationPolicies.severityMin', { min });
  return t('alarmCenter.notificationPolicies.severityMax', { max: max! });
}
