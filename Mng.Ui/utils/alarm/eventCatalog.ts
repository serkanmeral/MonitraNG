import type { EventLogChannelDictionary } from '@/types/apps/eventLogPackageCatalog';
import { fetchEventLogChannelDictionary } from '@/services/eventLogPackageCatalogService';

/** Stable picker value: channel + event id (ids can collide across channels). */
export function eventCatalogValue(channel: string, eventId: number): string {
  return `${channel}::${eventId}`;
}

export function parseEventCatalogValue(value: string): { channel: string; eventId: number } | null {
  const idx = value.lastIndexOf('::');
  if (idx <= 0) return null;
  const channel = value.slice(0, idx);
  const eventId = Number(value.slice(idx + 2));
  if (!channel || !Number.isFinite(eventId) || eventId <= 0) return null;
  return { channel, eventId };
}

export interface EventCatalogRow {
  value: string;
  eventId: number;
  channel: string;
  channelLabel: string;
  label: string;
  /** Best-effort observation matchKey (event.action); may be empty. */
  matchKey: string;
}

export interface EventCatalogSelection {
  value: string;
  eventId: number;
  channel: string;
  channelLabel: string;
  label: string;
  matchKey: string;
}

/**
 * Known EventID → observation key map (channel-scoped).
 * Prefer optional semantic keys (e.g. rdp.logon); otherwise flows use package id + eventCode filter.
 */
const MATCH_KEY_BY_VALUE: Record<string, string> = {
  [eventCatalogValue('Security', 4624)]: 'login_success',
  [eventCatalogValue('Security', 4625)]: 'login_failed',
  [eventCatalogValue('Security', 4634)]: 'logoff',
  [eventCatalogValue('Security', 4647)]: 'logoff',
  [eventCatalogValue('Security', 4720)]: 'account_created',
  [eventCatalogValue('Security', 4728)]: 'group_member_added',
  [eventCatalogValue('Security', 4732)]: 'group_member_added',
  [eventCatalogValue('Security', 4738)]: 'directory_object_modified',
  [eventCatalogValue('Security', 4740)]: 'account_locked',
  [eventCatalogValue('Microsoft-Windows-TerminalServices-LocalSessionManager/Operational', 21)]: 'rdp.logon',
  [eventCatalogValue('Microsoft-Windows-TerminalServices-LocalSessionManager/Operational', 23)]: 'rdp.logoff',
  [eventCatalogValue('Microsoft-Windows-TerminalServices-LocalSessionManager/Operational', 24)]: 'rdp.disconnect',
  [eventCatalogValue('Microsoft-Windows-TerminalServices-LocalSessionManager/Operational', 25)]: 'rdp.reconnect',
};

/** Channel → agent package id (collector observation key when no semantic map). */
const PACKAGE_KEY_BY_CHANNEL: Record<string, string> = {
  'Windows PowerShell': 'powershell-engine',
  'Microsoft-Windows-PowerShell/Operational': 'powershell-scriptblock',
  'Microsoft-Windows-TerminalServices-LocalSessionManager/Operational': 'rdp-session',
  Security: 'security-auth',
  System: 'system-lifecycle',
  Application: 'application-signals',
};

export function packageKeyForChannel(channel: string): string {
  const exact = PACKAGE_KEY_BY_CHANNEL[channel];
  if (exact) return exact;
  const normalized = channel.trim().toLowerCase();
  for (const [name, key] of Object.entries(PACKAGE_KEY_BY_CHANNEL)) {
    if (name.toLowerCase() === normalized) return key;
  }
  // Stable fallback from channel path (no per-EventID map required).
  const leaf = channel.split(/[\\/]/).filter(Boolean).at(-1) || channel;
  return leaf
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '') || 'windows.eventlog';
}

export function observationKeyForEvent(event: Pick<EventCatalogSelection, 'channel' | 'matchKey'>): string {
  const semantic = String(event.matchKey || '').trim();
  if (semantic) return semantic;
  return packageKeyForChannel(event.channel);
}

export function flattenEventLogChannelDictionary(
  channels: EventLogChannelDictionary[],
): EventCatalogRow[] {
  const rows: EventCatalogRow[] = [];
  for (const channel of channels) {
    for (const item of channel.knownEventIds ?? []) {
      const eventId = Number(item.id);
      if (!Number.isFinite(eventId) || eventId <= 0) continue;
      const value = eventCatalogValue(channel.channel, eventId);
      rows.push({
        value,
        eventId,
        channel: channel.channel,
        channelLabel: channel.label || channel.channel,
        label: item.label || String(eventId),
        matchKey: MATCH_KEY_BY_VALUE[value] ?? '',
      });
    }
  }
  return rows.sort((a, b) => {
    const channelCmp = a.channelLabel.localeCompare(b.channelLabel, undefined, { sensitivity: 'base' });
    if (channelCmp !== 0) return channelCmp;
    return a.eventId - b.eventId;
  });
}

let catalogCache: EventCatalogRow[] | null = null;
let catalogPromise: Promise<EventCatalogRow[]> | null = null;

export async function loadEventCatalogRows(force = false): Promise<EventCatalogRow[]> {
  if (!force && catalogCache) return catalogCache;
  if (!force && catalogPromise) return catalogPromise;

  catalogPromise = (async () => {
    try {
      const channels = await fetchEventLogChannelDictionary();
      catalogCache = flattenEventLogChannelDictionary(channels);
      return catalogCache;
    } catch {
      catalogCache = catalogCache ?? [];
      return catalogCache;
    } finally {
      catalogPromise = null;
    }
  })();

  return catalogPromise;
}

export function rowToSelection(row: EventCatalogRow): EventCatalogSelection {
  return {
    value: row.value,
    eventId: row.eventId,
    channel: row.channel,
    channelLabel: row.channelLabel,
    label: row.label,
    matchKey: row.matchKey,
  };
}

/** Build a selection for an Event ID not present in the curated dictionary. */
export function createCustomEventSelection(input: {
  channel: string;
  eventId: number;
  label?: string;
  matchKey?: string;
}): EventCatalogSelection | null {
  const channel = String(input.channel ?? '').trim();
  const eventId = Number(input.eventId);
  if (!channel || !Number.isFinite(eventId) || eventId <= 0 || eventId >= 1_000_000) return null;
  const value = eventCatalogValue(channel, eventId);
  const label = String(input.label ?? '').trim() || `Custom ${eventId}`;
  return {
    value,
    eventId,
    channel,
    channelLabel: channel,
    label,
    matchKey: String(input.matchKey ?? MATCH_KEY_BY_VALUE[value] ?? '').trim(),
  };
}

export function selectionToRow(item: EventCatalogSelection): EventCatalogRow {
  return {
    value: item.value,
    eventId: item.eventId,
    channel: item.channel,
    channelLabel: item.channelLabel || item.channel,
    label: item.label,
    matchKey: item.matchKey,
  };
}

export function selectionLabel(item: EventCatalogSelection): string {
  return `${item.label} (${item.eventId})`;
}

export function deriveMatchKeysFromEvents(events: EventCatalogSelection[]): string[] {
  const keys: string[] = [];
  const seen = new Set<string>();

  for (const event of events) {
    const key = observationKeyForEvent(event);
    if (!key || seen.has(key)) continue;
    seen.add(key);
    keys.push(key);
  }

  return keys;
}

export function eventCodesFromEvents(events: EventCatalogSelection[]): string[] {
  const codes: string[] = [];
  const seen = new Set<string>();
  for (const event of events) {
    // Linux journal selections use eventId 0 (matchKey-based).
    if (!event.eventId || event.eventId <= 0) continue;
    const code = String(event.eventId);
    if (seen.has(code)) continue;
    seen.add(code);
    codes.push(code);
  }
  return codes;
}

export function filterEventCatalogRows(
  rows: EventCatalogRow[],
  search: string,
  channelFilter: string | null,
): EventCatalogRow[] {
  const q = search.trim().toLowerCase();
  return rows.filter((row) => {
    if (channelFilter && row.channel !== channelFilter) return false;
    if (!q) return true;
    return (
      String(row.eventId).includes(q)
      || row.label.toLowerCase().includes(q)
      || row.channel.toLowerCase().includes(q)
      || row.channelLabel.toLowerCase().includes(q)
      || row.matchKey.toLowerCase().includes(q)
    );
  });
}
