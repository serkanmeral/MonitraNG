import { ocCreate, ocDelete, ocListDataset, ocUpdate } from '@/services/operationCoreService';
import { ODAK_SIPARIS_CONFIG } from '@/utils/odakSiparisConfig';

export const ODAK_SIPARIS_NOTIFICATION_EVENT_TYPES = [
  'PackageCreated',
  'PackageUpdated',
  'ShipmentCompleted',
  'GlobalShipmentCreated',
] as const;

export type OdakSiparisNotificationEventType = (typeof ODAK_SIPARIS_NOTIFICATION_EVENT_TYPES)[number];

/** İş paketi ayarları — bildirim politikası olayları. */
export const ODAK_PACKAGE_NOTIFICATION_EVENT_TYPES = [
  'PackageCreated',
  'PackageUpdated',
  'ShipmentCompleted',
] as const satisfies readonly OdakSiparisNotificationEventType[];

export const ODAK_GLOBAL_SHIPMENT_NOTIFICATION_EVENT = 'GlobalShipmentCreated' as const;

export const ODAK_GLOBAL_SHIPMENT_NOTIFICATION_EVENT_TYPES = [
  ODAK_GLOBAL_SHIPMENT_NOTIFICATION_EVENT,
] as const satisfies readonly OdakSiparisNotificationEventType[];

export const ODAK_GLOBAL_SHIPMENT_DEFAULT_MAIL_TEMPLATE = 'odak-global-shipment-created';

export type OdakPackageUpdateTriggerMode = 'always' | 'fields';
export type OdakShipmentTriggerMode = 'transition' | 'toStatus' | 'always';

export interface OdakSiparisNotificationPolicy {
  __dataId?: string;
  id?: string;
  name: string;
  description?: string | null;
  eventType: OdakSiparisNotificationEventType | string;
  recipientPersonIds: string[];
  emailTemplateKey?: string | null;
  emailSubject?: string | null;
  excludeActor?: boolean;
  updateTriggerMode?: OdakPackageUpdateTriggerMode;
  watchedFields?: string[];
  shipmentTriggerMode?: OdakShipmentTriggerMode;
  fromStatus?: string | null;
  toStatus?: string | null;
  targetStatus?: string | null;
  priority?: number | null;
  isActive: boolean;
}

function parseStringArray(raw: unknown): string[] {
  if (raw == null) return [];
  if (Array.isArray(raw)) return raw.map((v) => String(v).trim()).filter(Boolean);
  return [];
}

function policyId(raw: Record<string, unknown>): string {
  return String(raw.__dataId ?? raw.dataId ?? raw.id ?? raw.Id ?? '').trim();
}

export function normalizeOdakNotificationPolicy(raw: Record<string, unknown>): OdakSiparisNotificationPolicy {
  return {
    __dataId: policyId(raw) || undefined,
    id: policyId(raw) || undefined,
    name: String(raw.name ?? raw.Name ?? '').trim(),
    description:
      raw.description != null
        ? String(raw.description)
        : raw.Description != null
          ? String(raw.Description)
          : null,
    eventType: String(raw.eventType ?? raw.EventType ?? 'PackageCreated'),
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
    excludeActor: raw.excludeActor === true || raw.ExcludeActor === true,
    updateTriggerMode:
      String(raw.updateTriggerMode ?? raw.UpdateTriggerMode ?? 'always') === 'fields' ? 'fields' : 'always',
    watchedFields: parseStringArray(raw.watchedFields ?? raw.WatchedFields),
    shipmentTriggerMode: (() => {
      const v = String(raw.shipmentTriggerMode ?? raw.ShipmentTriggerMode ?? 'transition');
      if (v === 'toStatus' || v === 'always') return v;
      return 'transition';
    })(),
    fromStatus:
      raw.fromStatus != null
        ? String(raw.fromStatus).trim() || null
        : raw.FromStatus != null
          ? String(raw.FromStatus).trim() || null
          : null,
    toStatus:
      raw.toStatus != null
        ? String(raw.toStatus).trim() || null
        : raw.ToStatus != null
          ? String(raw.ToStatus).trim() || null
          : null,
    targetStatus:
      raw.targetStatus != null
        ? String(raw.targetStatus).trim() || null
        : raw.TargetStatus != null
          ? String(raw.TargetStatus).trim() || null
          : null,
    priority:
      raw.priority != null
        ? Number(raw.priority)
        : raw.Priority != null
          ? Number(raw.Priority)
          : null,
    isActive: raw.isActive !== false && raw.IsActive !== false,
  };
}

export interface OdakNotificationPolicyDraft {
  id?: string;
  name: string;
  description: string;
  eventType: string;
  recipientPersonIds: string[];
  emailTemplateKey: string;
  emailSubject: string;
  excludeActor: boolean;
  updateTriggerMode: OdakPackageUpdateTriggerMode;
  watchedFields: string[];
  shipmentTriggerMode: OdakShipmentTriggerMode;
  fromStatus: string | null;
  toStatus: string | null;
  targetStatus: string | null;
  priority: number;
  isActive: boolean;
}

export function newOdakNotificationPolicyDraft(
  seed?: Partial<OdakNotificationPolicyDraft>
): OdakNotificationPolicyDraft {
  const eventType = seed?.eventType ?? 'PackageCreated';
  return {
    name: '',
    description: '',
    eventType,
    recipientPersonIds: [],
    emailTemplateKey: '',
    emailSubject: '',
    excludeActor: false,
    updateTriggerMode: 'always',
    watchedFields: [],
    shipmentTriggerMode: 'transition',
    fromStatus: 'Planlandi',
    toStatus: 'Tamamlandi',
    targetStatus: 'Tamamlandi',
    priority: 50,
    isActive: true,
    ...seed,
  };
}

export function parseOdakNotificationPolicyToDraft(
  policy: OdakSiparisNotificationPolicy
): OdakNotificationPolicyDraft {
  return {
    id: policy.__dataId ?? policy.id,
    name: policy.name,
    description: policy.description ?? '',
    eventType: policy.eventType,
    recipientPersonIds: [...policy.recipientPersonIds],
    emailTemplateKey: policy.emailTemplateKey ?? '',
    emailSubject: policy.emailSubject ?? '',
    excludeActor: policy.excludeActor === true,
    updateTriggerMode: policy.updateTriggerMode ?? 'always',
    watchedFields: policy.watchedFields ? [...policy.watchedFields] : [],
    shipmentTriggerMode: policy.shipmentTriggerMode ?? 'transition',
    fromStatus: policy.fromStatus ?? 'Planlandi',
    toStatus: policy.toStatus ?? 'Tamamlandi',
    targetStatus: policy.targetStatus ?? 'Tamamlandi',
    priority: policy.priority ?? 50,
    isActive: policy.isActive !== false,
  };
}

export function validateOdakNotificationPolicyDraft(draft: OdakNotificationPolicyDraft): string | null {
  if (!draft.name.trim()) return 'name';
  if (!draft.eventType.trim()) return 'eventType';
  if (!draft.recipientPersonIds.length) return 'recipientPersonIds';
  if (!draft.emailTemplateKey.trim()) return 'emailTemplateKey';
  if (
    draft.eventType === 'PackageUpdated' &&
    draft.updateTriggerMode === 'fields' &&
    !draft.watchedFields.length
  ) {
    return 'watchedFields';
  }
  return null;
}

export function buildOdakNotificationPolicyPayload(
  draft: OdakNotificationPolicyDraft
): Record<string, unknown> {
  return {
    name: draft.name.trim(),
    description: draft.description.trim() || null,
    eventType: draft.eventType.trim(),
    recipientPersonIds: [...draft.recipientPersonIds],
    emailTemplateKey: draft.emailTemplateKey.trim() || null,
    emailSubject: draft.emailSubject.trim() || null,
    excludeActor: draft.excludeActor,
    updateTriggerMode: draft.eventType === 'PackageUpdated' ? draft.updateTriggerMode : null,
    watchedFields:
      draft.eventType === 'PackageUpdated' && draft.updateTriggerMode === 'fields'
        ? [...draft.watchedFields]
        : [],
    shipmentTriggerMode: draft.eventType === 'ShipmentCompleted' ? draft.shipmentTriggerMode : null,
    fromStatus:
      draft.eventType === 'ShipmentCompleted' && draft.shipmentTriggerMode === 'transition'
        ? draft.fromStatus
        : null,
    toStatus:
      draft.eventType === 'ShipmentCompleted' && draft.shipmentTriggerMode === 'transition'
        ? draft.toStatus
        : null,
    targetStatus:
      draft.eventType === 'ShipmentCompleted' && draft.shipmentTriggerMode === 'toStatus'
        ? draft.targetStatus
        : null,
    priority: draft.priority,
    isActive: draft.isActive,
  };
}

let policiesCache: OdakSiparisNotificationPolicy[] | null = null;
let policiesCacheAt = 0;

export function invalidateOdakNotificationPoliciesCache(): void {
  policiesCache = null;
  policiesCacheAt = 0;
}

async function loadPoliciesCached(): Promise<OdakSiparisNotificationPolicy[]> {
  const now = Date.now();
  if (policiesCache && now - policiesCacheAt < 60_000) return policiesCache;
  policiesCache = await listOdakNotificationPolicies();
  policiesCacheAt = now;
  return policiesCache;
}

export { loadPoliciesCached as loadOdakNotificationPoliciesCached };

export async function listOdakNotificationPolicies(): Promise<OdakSiparisNotificationPolicy[]> {
  const rows = await ocListDataset(ODAK_SIPARIS_CONFIG.notificationPoliciesDataset, { limit: 500 });
  return rows
    .map((r) => normalizeOdakNotificationPolicy(r as Record<string, unknown>))
    .sort((a, b) => (b.priority ?? 0) - (a.priority ?? 0));
}

export async function listOdakNotificationPoliciesForEvents(
  eventTypes: readonly string[]
): Promise<OdakSiparisNotificationPolicy[]> {
  const allowed = new Set(eventTypes);
  const all = await listOdakNotificationPolicies();
  return all.filter((p) => allowed.has(p.eventType));
}

export async function createOdakNotificationPolicy(
  draft: OdakNotificationPolicyDraft
): Promise<OdakSiparisNotificationPolicy> {
  const created = (await ocCreate(
    ODAK_SIPARIS_CONFIG.notificationPoliciesDataset,
    buildOdakNotificationPolicyPayload(draft)
  )) as Record<string, unknown>;
  return normalizeOdakNotificationPolicy(created);
}

export async function updateOdakNotificationPolicy(
  id: string,
  draft: OdakNotificationPolicyDraft
): Promise<void> {
  await ocUpdate(ODAK_SIPARIS_CONFIG.notificationPoliciesDataset, id, buildOdakNotificationPolicyPayload(draft));
}

export async function deleteOdakNotificationPolicy(id: string): Promise<void> {
  await ocDelete(ODAK_SIPARIS_CONFIG.notificationPoliciesDataset, id);
}

/** Policy matches event + optional PackageUpdated field / Shipment status triggers. */
export function odakNotificationPolicyMatchesEvent(
  policy: OdakSiparisNotificationPolicy,
  eventType: string,
  context?: {
    changedFields?: string[];
    shipmentPreviousStatus?: string | null;
    shipmentNewStatus?: string | null;
  }
): boolean {
  if (policy.isActive === false) return false;
  if (policy.eventType !== eventType) return false;

  if (eventType === 'PackageUpdated') {
    const mode = policy.updateTriggerMode ?? 'always';
    if (mode === 'always') return true;
    const watched = policy.watchedFields ?? [];
    if (!watched.length) return false;
    const changed = new Set(context?.changedFields ?? []);
    return watched.some((f) => changed.has(f));
  }

  if (eventType === 'ShipmentCompleted') {
    const mode = policy.shipmentTriggerMode ?? 'transition';
    const prev = context?.shipmentPreviousStatus ?? null;
    const next = context?.shipmentNewStatus ?? null;
    if (mode === 'always') return true;
    if (mode === 'toStatus') {
      return next != null && next === (policy.targetStatus ?? 'Tamamlandi');
    }
    return prev === (policy.fromStatus ?? 'Planlandi') && next === (policy.toStatus ?? 'Tamamlandi');
  }

  return true;
}
