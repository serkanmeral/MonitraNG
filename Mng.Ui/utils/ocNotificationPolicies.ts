import type { OpNotificationPolicy, OpStateFlowTransition } from '@/types/apps/operationCore';

export const OC_NOTIFICATION_EVENT_TYPES = [
  'WorkItemCreated',
  'WorkItemTransitioned',
  'WorkItemUpdated',
] as const;

export const OC_NOTIFICATION_CHANNELS = ['email', 'inApp'] as const;

export const OC_TOAST_SEVERITIES = ['info', 'success', 'warning', 'error'] as const;

export type OcToastSeverity = (typeof OC_TOAST_SEVERITIES)[number];

const DEFAULT_IN_APP_TEMPLATE_BY_EVENT: Record<string, string> = {
  WorkItemCreated: 'work-item-created-inapp',
  WorkItemTransitioned: 'work-item-transitioned-inapp',
  WorkItemUpdated: 'work-item-updated-inapp',
};

export function defaultInAppTemplateKeyForEvent(eventType: string): string {
  return DEFAULT_IN_APP_TEMPLATE_BY_EVENT[eventType] ?? '';
}

export const OC_NOTIFICATION_RECIPIENT_KEYS = [
  'assignee',
  'reporter',
  'watchers',
  'actor',
] as const;

export interface OcNotificationPolicyDraft {
  id?: string;
  name: string;
  boardId?: string | null;
  typeId?: string | null;
  eventType: string;
  channels: string[];
  recipients: string[];
  emailTemplateKey: string;
  emailSubject: string;
  notificationTemplateKey: string;
  transitionKey?: string | null;
  fromStateId?: string | null;
  toStateId?: string | null;
  excludeActor: boolean;
  policyPriority: number;
  isActive: boolean;
  pushToast: boolean;
  toastSeverity: OcToastSeverity;
}

export function newNotificationPolicyDraft(
  seed?: Partial<OcNotificationPolicyDraft>
): OcNotificationPolicyDraft {
  return {
    name: '',
    boardId: null,
    typeId: null,
    eventType: 'WorkItemTransitioned',
    channels: ['email'],
    recipients: ['assignee'],
    emailTemplateKey: 'work-item-transitioned',
    emailSubject: '',
    notificationTemplateKey: '',
    transitionKey: null,
    fromStateId: null,
    toStateId: null,
    excludeActor: false,
    policyPriority: 50,
    isActive: true,
    pushToast: false,
    toastSeverity: 'info',
    ...seed,
  };
}

export function parseOpNotificationPolicyToDraft(policy: OpNotificationPolicy): OcNotificationPolicyDraft {
  return {
    id: policy.__dataId,
    name: policy.name,
    boardId: policy.boardId ?? null,
    typeId: policy.typeId ?? null,
    eventType: policy.eventType || 'WorkItemTransitioned',
    channels: policy.channels.length ? [...policy.channels] : ['email'],
    recipients: policy.recipients.length ? [...policy.recipients] : ['assignee'],
    emailTemplateKey: policy.emailTemplateKey ?? '',
    emailSubject: policy.emailSubject ?? '',
    notificationTemplateKey: policy.notificationTemplateKey ?? '',
    transitionKey: policy.transitionKey ?? null,
    fromStateId: policy.fromStateId ?? null,
    toStateId: policy.toStateId ?? null,
    excludeActor: policy.excludeActor === true,
    policyPriority: policy.priority ?? 50,
    isActive: policy.isActive !== false,
    pushToast: policy.settings?.pushToast === true,
    toastSeverity: parseToastSeverity(policy.settings?.toastSeverity) ?? 'info',
  };
}

function parseToastSeverity(raw: unknown): OcToastSeverity | null {
  const value = raw != null ? String(raw).trim().toLowerCase() : '';
  return (OC_TOAST_SEVERITIES as readonly string[]).includes(value)
    ? (value as OcToastSeverity)
    : null;
}

function selectionText(raw: unknown): string {
  if (typeof raw === 'string') return raw.trim();
  if (raw && typeof raw === 'object') {
    const record = raw as Record<string, unknown>;
    if (record.value != null) return String(record.value).trim();
    if (record.title != null) return String(record.title).trim();
  }
  return '';
}

function selectionList(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];
  return raw.map(selectionText).filter(Boolean);
}

function selectionKey(raw: unknown, items?: { value: string; title: string }[]): string {
  const text = selectionText(raw);
  if (!text || !items?.length) return text;
  const byValue = items.find((item) => item.value === text);
  if (byValue) return byValue.value;
  const byTitle = items.find((item) => item.title === text);
  return byTitle?.value ?? text;
}

/** Combobox seçimleri nesne döndürebilir. Kayıt anahtarları düz metin olmalı. */
export function normalizeNotificationPolicyDraft(
  draft: OcNotificationPolicyDraft,
  templates?: {
    email?: { value: string; title: string }[];
    inApp?: { value: string; title: string }[];
  }
): OcNotificationPolicyDraft {
  return {
    ...draft,
    name: selectionText(draft.name),
    eventType: selectionText(draft.eventType),
    channels: selectionList(draft.channels),
    recipients: selectionList(draft.recipients),
    emailTemplateKey: selectionKey(draft.emailTemplateKey, templates?.email),
    emailSubject: selectionText(draft.emailSubject),
    notificationTemplateKey: selectionKey(draft.notificationTemplateKey, templates?.inApp),
  };
}

export function validateNotificationPolicyDraft(draft: OcNotificationPolicyDraft): string | null {
  const normalized = normalizeNotificationPolicyDraft(draft);
  if (!normalized.name) return 'name';
  if (!normalized.eventType) return 'eventType';
  if (!normalized.channels.length) return 'channels';
  if (!normalized.recipients.length) return 'recipients';
  if (normalized.channels.includes('email') && !normalized.emailTemplateKey) return 'emailTemplateKey';
  return null;
}

export function buildNotificationPolicyPayload(
  draft: OcNotificationPolicyDraft,
  workspaceId: string,
  templates?: {
    email?: { value: string; title: string }[];
    inApp?: { value: string; title: string }[];
  }
): Record<string, unknown> {
  const normalized = normalizeNotificationPolicyDraft(draft, templates);
  const body: Record<string, unknown> = {
    name: normalized.name,
    workspaceId,
    eventType: normalized.eventType,
    channels: [...normalized.channels],
    recipients: [...normalized.recipients],
    excludeActor: normalized.excludeActor,
    isActive: normalized.isActive,
    priority: normalized.policyPriority,
  };
  if (normalized.boardId) body.boardId = normalized.boardId;
  if (normalized.typeId) body.typeId = normalized.typeId;
  if (normalized.channels.includes('email')) {
    body.emailTemplateKey = normalized.emailTemplateKey;
    body.emailSubject = normalized.emailSubject || null;
  } else {
    body.emailTemplateKey = null;
    body.emailSubject = null;
  }
  if (normalized.channels.includes('inApp') && normalized.notificationTemplateKey) {
    body.notificationTemplateKey = normalized.notificationTemplateKey;
  } else if (!normalized.channels.includes('inApp')) {
    body.notificationTemplateKey = null;
  }
  if (normalized.eventType === 'WorkItemTransitioned') {
    body.transitionKey = selectionText(draft.transitionKey) || null;
    body.fromStateId = selectionText(draft.fromStateId) || null;
    body.toStateId = selectionText(draft.toStateId) || null;
  } else {
    body.transitionKey = null;
    body.fromStateId = null;
    body.toStateId = null;
  }
  if (normalized.channels.includes('inApp')) {
    body.settings = {
      pushToast: normalized.pushToast === true,
      toastSeverity: normalized.toastSeverity,
    };
  } else {
    body.settings = null;
  }
  return body;
}

export function collectTransitionOptions(
  transitions: OpStateFlowTransition[]
): { value: string; title: string; fromStateId: string; toStateId: string }[] {
  const seen = new Set<string>();
  const items: { value: string; title: string; fromStateId: string; toStateId: string }[] = [];
  for (const tr of transitions) {
    if (!tr.transitionKey || seen.has(tr.transitionKey)) continue;
    seen.add(tr.transitionKey);
    items.push({
      value: tr.transitionKey,
      title: tr.label?.trim() ? `${tr.label} (${tr.transitionKey})` : tr.transitionKey,
      fromStateId: tr.fromStateId,
      toStateId: tr.toStateId,
    });
  }
  return items.sort((a, b) => a.title.localeCompare(b.title, 'tr'));
}

export function formatNotificationTransitionSummary(
  policy: Pick<OpNotificationPolicy, 'eventType' | 'transitionKey' | 'fromStateId' | 'toStateId'>,
  stateNameById: Map<string, string>,
  anyTransitionLabel: string
): string {
  if (policy.eventType !== 'WorkItemTransitioned') return '—';
  const parts: string[] = [];
  if (policy.transitionKey) parts.push(policy.transitionKey);
  if (policy.fromStateId || policy.toStateId) {
    const from = policy.fromStateId
      ? stateNameById.get(policy.fromStateId) ?? policy.fromStateId
      : '…';
    const to = policy.toStateId
      ? stateNameById.get(policy.toStateId) ?? policy.toStateId
      : '…';
    parts.push(`${from} → ${to}`);
  }
  return parts.length ? parts.join(' · ') : anyTransitionLabel;
}

export function formatNotificationRecipientsSummary(
  recipients: string[],
  recipientLabel: (key: string) => string
): string {
  if (!recipients.length) return '—';
  return recipients.map((r) => recipientLabel(r)).join(', ');
}

export function formatNotificationChannelsSummary(
  channels: string[],
  channelLabel: (key: string) => string
): string {
  if (!channels.length) return '—';
  return channels.map((c) => channelLabel(c)).join(' + ');
}

export function notificationPolicySpecificityScore(
  policy: Pick<OpNotificationPolicy, 'transitionKey' | 'fromStateId' | 'toStateId' | 'typeId' | 'boardId'>
): number {
  let score = 0;
  if (policy.transitionKey) score += 4;
  if (policy.fromStateId && policy.toStateId) score += 3;
  if (policy.typeId) score += 2;
  if (policy.boardId) score += 1;
  return score;
}

export function recipientDisplayKey(key: string): string {
  if (key.startsWith('field:')) return key;
  return key;
}
