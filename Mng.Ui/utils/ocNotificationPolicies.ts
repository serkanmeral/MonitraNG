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

export function validateNotificationPolicyDraft(draft: OcNotificationPolicyDraft): string | null {
  if (!draft.name.trim()) return 'name';
  if (!draft.eventType.trim()) return 'eventType';
  if (!draft.channels.length) return 'channels';
  if (!draft.recipients.length) return 'recipients';
  const wantsEmail = draft.channels.includes('email');
  if (wantsEmail && !draft.emailTemplateKey.trim()) return 'emailTemplateKey';
  return null;
}

export function buildNotificationPolicyPayload(
  draft: OcNotificationPolicyDraft,
  workspaceId: string
): Record<string, unknown> {
  const body: Record<string, unknown> = {
    name: draft.name.trim(),
    workspaceId,
    eventType: draft.eventType.trim(),
    channels: [...draft.channels],
    recipients: [...draft.recipients],
    excludeActor: draft.excludeActor,
    isActive: draft.isActive,
    priority: draft.policyPriority,
  };
  if (draft.boardId) body.boardId = draft.boardId;
  if (draft.typeId) body.typeId = draft.typeId;
  if (draft.channels.includes('email')) {
    body.emailTemplateKey = draft.emailTemplateKey.trim();
    body.emailSubject = draft.emailSubject.trim() || null;
  } else {
    body.emailTemplateKey = null;
    body.emailSubject = null;
  }
  if (draft.channels.includes('inApp') && draft.notificationTemplateKey.trim()) {
    body.notificationTemplateKey = draft.notificationTemplateKey.trim();
  } else if (!draft.channels.includes('inApp')) {
    body.notificationTemplateKey = null;
  }
  if (draft.eventType === 'WorkItemTransitioned') {
    body.transitionKey = draft.transitionKey?.trim() || null;
    body.fromStateId = draft.fromStateId || null;
    body.toStateId = draft.toStateId || null;
  } else {
    body.transitionKey = null;
    body.fromStateId = null;
    body.toStateId = null;
  }
  if (draft.channels.includes('inApp')) {
    body.settings = {
      pushToast: draft.pushToast === true,
      toastSeverity: draft.toastSeverity,
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
