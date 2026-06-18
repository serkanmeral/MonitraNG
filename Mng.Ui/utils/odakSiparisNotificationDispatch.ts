/**
 * Odak Sipariş — bildirim dispatch (MngNotifier send-template).
 * Fire-and-forget; hata UI akışını kesmez.
 */

import { getAccessToken } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';
import type { OdakPackageRow } from '@/utils/odakSiparisConfig';
import {
  listOdakNotificationPolicies,
  loadOdakNotificationPoliciesCached,
  odakNotificationPolicyMatchesEvent,
  type OdakSiparisNotificationPolicy,
} from '@/utils/odakSiparisNotificationPolicies';
import { packageDisplayNo } from '@/utils/odakSiparisService';

export interface OdakNotificationDispatchContext {
  changedFields?: string[];
  shipmentPreviousStatus?: string | null;
  shipmentNewStatus?: string | null;
  actorPersonId?: string | null;
}

async function notifierSendTemplate(body: Record<string, unknown>): Promise<void> {
  const auth = useAuthStore();
  await auth.ensureValidToken();
  const token = getAccessToken();
  if (!token) return;
  await $fetch('/api/notifier/v1/notifications/send-template', {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },
    body,
  });
}

function resolveRecipientEmails(
  personIds: string[],
  excludeActorId: string | null | undefined,
  userStore: ReturnType<typeof useUserStore>
): string[] {
  const emails = new Set<string>();
  for (const id of personIds) {
    if (excludeActorId && id === excludeActorId) continue;
    const user = userStore.users.find((u) => u.id === id || u.sub === id);
    const email = user?.email?.trim();
    if (email) emails.add(email);
  }
  return [...emails];
}

function buildMailContext(
  eventType: string,
  pkg: OdakPackageRow | null | undefined,
  ctx: OdakNotificationDispatchContext
): Record<string, unknown> {
  return {
    event: { type: eventType, timestamp: new Date().toISOString() },
    package: pkg
      ? {
          id: pkg.__dataId ?? pkg.dataId,
          packageNo: pkg.packageNo,
          displayNo: packageDisplayNo(pkg),
          name: pkg.name,
          status: pkg.status,
        }
      : null,
    shipment:
      ctx.shipmentPreviousStatus != null || ctx.shipmentNewStatus != null
        ? { fromStatus: ctx.shipmentPreviousStatus, toStatus: ctx.shipmentNewStatus }
        : null,
    changedFields: ctx.changedFields ?? [],
  };
}

async function dispatchPolicies(
  eventType: string,
  policies: OdakSiparisNotificationPolicy[],
  pkg: OdakPackageRow | null | undefined,
  ctx: OdakNotificationDispatchContext
): Promise<void> {
  const userStore = useUserStore();
  if (!userStore.users.length) {
    try {
      await userStore.fetchUsers({ limit: 500 });
    } catch {
      /* best effort */
    }
  }

  const matching = policies.filter((p) => odakNotificationPolicyMatchesEvent(p, eventType, ctx));
  for (const policy of matching) {
    const templateKey = policy.emailTemplateKey?.trim();
    if (!templateKey) continue;
    const to = resolveRecipientEmails(
      policy.recipientPersonIds,
      policy.excludeActor ? ctx.actorPersonId : null,
      userStore
    );
    if (!to.length) continue;
    try {
      await notifierSendTemplate({
        templateKey,
        to,
        subject: policy.emailSubject?.trim() || undefined,
        context: buildMailContext(eventType, pkg, ctx),
      });
    } catch (e) {
      console.warn('[OdakSiparis] notification dispatch failed', policy.name, e);
    }
  }
}

async function loadPolicies(): Promise<OdakSiparisNotificationPolicy[]> {
  return loadOdakNotificationPoliciesCached();
}

export async function dispatchOdakPackageNotification(
  eventType: 'PackageCreated' | 'PackageUpdated' | 'ShipmentCompleted',
  pkg: OdakPackageRow | null | undefined,
  ctx: OdakNotificationDispatchContext = {}
): Promise<void> {
  try {
    const policies = await loadPolicies();
    await dispatchPolicies(eventType, policies, pkg, ctx);
  } catch (e) {
    console.warn('[OdakSiparis] notification policies load failed', e);
  }
}

export function diffPackageFields(
  before: Record<string, unknown>,
  after: Record<string, unknown>
): string[] {
  const keys = new Set([...Object.keys(before), ...Object.keys(after)]);
  const changed: string[] = [];
  for (const key of keys) {
    if (JSON.stringify(before[key]) !== JSON.stringify(after[key])) changed.push(key);
  }
  return changed;
}
