import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';

export type KeeperListSettingsScope = 'users_list' | 'groups_list';

const STORAGE_PREFIX = 'mng_keeper_list_settings';

function storageKey(domainId: string): string {
  return `${STORAGE_PREFIX}_${domainId || 'default'}`;
}

export function loadKeeperListConfigFromStorage(
  domainId: string,
  scope: KeeperListSettingsScope
): unknown | null {
  if (typeof localStorage === 'undefined') return null;
  try {
    const raw = localStorage.getItem(storageKey(domainId));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    return parsed[scope] ?? null;
  } catch {
    return null;
  }
}

export function saveKeeperListConfigToStorage(
  domainId: string,
  scope: KeeperListSettingsScope,
  listConfig: OdakHubListConfig
): void {
  if (typeof localStorage === 'undefined') return;
  try {
    const key = storageKey(domainId);
    const existing = localStorage.getItem(key);
    const root = existing ? (JSON.parse(existing) as Record<string, unknown>) : {};
    root[scope] = { listConfig };
    localStorage.setItem(key, JSON.stringify(root));
  } catch {
    // ignore quota / private mode
  }
}

export function clearKeeperListConfigFromStorage(domainId: string, scope: KeeperListSettingsScope): void {
  if (typeof localStorage === 'undefined') return;
  try {
    const key = storageKey(domainId);
    const existing = localStorage.getItem(key);
    if (!existing) return;
    const root = JSON.parse(existing) as Record<string, unknown>;
    delete root[scope];
    localStorage.setItem(key, JSON.stringify(root));
  } catch {
    // ignore
  }
}
