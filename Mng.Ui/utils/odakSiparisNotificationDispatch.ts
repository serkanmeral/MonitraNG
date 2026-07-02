/**
 * Odak Sipariş — bildirim dispatch (MngNotifier send-template).
 * Fire-and-forget; hata UI akışını kesmez.
 */

import { getAccessToken } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';
import { useUserStore, type User } from '@/stores/apps/user';
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

function emailsFromUsers(users: User[], excludeActorId: string | null | undefined): string[] {
  const emails = new Set<string>();
  for (const user of users) {
    const id = (user.id || user.userId || '').trim();
    if (excludeActorId && id === excludeActorId) continue;
    const email = user.email?.trim();
    if (email) emails.add(email);
  }
  return [...emails];
}

function resolveRecipientEmails(
  personIds: string[],
  excludeActorId: string | null | undefined,
  userStore: ReturnType<typeof useUserStore>
): string[] {
  const users = personIds
    .map((id) => userStore.users.find((u) => u.id === id || u.userId === id || u.sub === id))
    .filter((u): u is User => u != null);
  return emailsFromUsers(users, excludeActorId);
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
        ? {
            fromStatus: ctx.shipmentPreviousStatus?.trim() || '—',
            toStatus: ctx.shipmentNewStatus?.trim() || '—',
          }
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

  const matching = policies.filter((p) => odakNotificationPolicyMatchesEvent(p, eventType, ctx));
  for (const policy of matching) {
    const templateKey = policy.emailTemplateKey?.trim();
    if (!templateKey) continue;

    const recipientIds = policy.recipientPersonIds ?? [];
    if (!recipientIds.length) continue;

    let resolvedUsers: User[] = [];
    try {
      resolvedUsers = await userStore.fetchUsersByIds(recipientIds);
    } catch {
      if (!userStore.users.length) {
        try {
          await userStore.fetchUsers({ pageSize: 500 });
        } catch {
          /* best effort */
        }
      }
    }

    const excludeActor = policy.excludeActor ? ctx.actorPersonId : null;
    const to =
      resolvedUsers.length > 0
        ? emailsFromUsers(resolvedUsers, excludeActor)
        : resolveRecipientEmails(recipientIds, excludeActor, userStore);
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
