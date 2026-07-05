/**
 * Odak Sipariş — iş paketi Odak personeli havuzu (hub scope: package_odak_personnel)
 * ve Keeper/Keycloak kullanıcı etiket çözümü.
 */
import { useUserStore } from '@/stores/apps/user';
import {
  collectPersonIdsFromValue,
  mapUserToOcPersonPickerItem,
  type OcPersonPickerItem,
} from '@/utils/ocPersonPicker';
import { collectLegacyPersonKeeperIds } from '@/utils/odakSiparisLegacyPersonMap';

export interface OdakPackagePersonnelConfig {
  /** Tasarım sorumlusu adayı Keeper kullanıcı kimlikleri */
  designPersonnelIds: string[];
  /** Üretim sorumlusu adayı Keeper kullanıcı kimlikleri */
  manufacturePersonnelIds: string[];
}

export function defaultOdakPackagePersonnelConfig(): OdakPackagePersonnelConfig {
  return { designPersonnelIds: [], manufacturePersonnelIds: [] };
}

function parsePersonIdArray(raw: unknown): string[] {
  if (raw == null) return [];
  if (!Array.isArray(raw)) return collectPersonIdsFromValue(raw);
  return raw.map((v) => String(v ?? '').trim()).filter(Boolean);
}

export function mergeOdakPackagePersonnelConfig(saved: unknown): OdakPackagePersonnelConfig {
  const base = defaultOdakPackagePersonnelConfig();
  if (!saved || typeof saved !== 'object') return base;
  const o = saved as Record<string, unknown>;
  const nested = o.packagePersonnel ?? o.PackagePersonnel ?? o.odakPersonnel ?? o.OdakPersonnel;
  const src = nested && typeof nested === 'object' ? (nested as Record<string, unknown>) : o;
  return {
    designPersonnelIds: parsePersonIdArray(
      src.designPersonnelIds ?? src.DesignPersonnelIds ?? src.designPersonIds
    ),
    manufacturePersonnelIds: parsePersonIdArray(
      src.manufacturePersonnelIds ?? src.ManufacturePersonnelIds ?? src.manufacturePersonIds
    ),
  };
}

export function personIdFromRow(raw: unknown): string {
  if (raw == null || raw === '') return '';
  if (typeof raw === 'string') return raw.trim();
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    return String(
      o.id ?? o.userId ?? o.UserId ?? o.keycloakUserId ?? o.__dataId ?? o.dataId ?? ''
    ).trim();
  }
  return String(raw).trim();
}

export function personLabelFromRow(raw: unknown, labels: Record<string, string> = {}): string {
  if (raw != null && typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    const combined = `${o.firstName ?? o.FirstName ?? ''} ${o.lastName ?? o.LastName ?? ''}`.trim();
    if (combined) return combined;
    const username = o.username ?? o.Username;
    if (username != null && String(username).trim()) return String(username).trim();
  }
  const id = personIdFromRow(raw);
  if (!id) return '—';
  return labels[id] || id;
}

export function collectPersonIdsFromPackageRows(
  rows: Array<Record<string, unknown>>,
  fields: Array<'designContactId' | 'manufactureContactId' | 'responsibleContactId'> = [
    'designContactId',
    'manufactureContactId',
    'responsibleContactId',
  ]
): string[] {
  const ids = new Set<string>();
  for (const row of rows) {
    for (const field of fields) {
      const id = personIdFromRow(row[field]);
      if (id) ids.add(id);
    }
  }
  return [...ids];
}

const LEGACY_PERSON_FIELD_KEYS = [
  'legacyDesignResponsibleId',
  'legacyManufactureResponsibleId',
  'legacyResponsibleId',
] as const;

function collectLegacyPersonIdsFromPackageRows(rows: Array<Record<string, unknown>>): string[] {
  const ids = new Set<string>();
  for (const row of rows) {
    for (const field of LEGACY_PERSON_FIELD_KEYS) {
      const raw = String(row[field] ?? '').trim();
      if (raw && /^\d+$/.test(raw)) ids.add(raw);
    }
  }
  return [...ids];
}

/** Keeper person alanlari + legacy ID map uzerinden cozulecek keeper id'leri. */
export function collectPersonIdsForPackageLabelResolution(
  rows: Array<Record<string, unknown>>
): string[] {
  const ids = new Set(collectPersonIdsFromPackageRows(rows));
  for (const keeperId of collectLegacyPersonKeeperIds(collectLegacyPersonIdsFromPackageRows(rows))) {
    ids.add(keeperId);
  }
  return [...ids];
}

export async function fetchPersonLabelMap(ids: string[]): Promise<Record<string, string>> {
  const unique = [...new Set(ids.map((id) => id.trim()).filter(Boolean))];
  if (!unique.length) return {};
  const userStore = useUserStore();
  const map: Record<string, string> = {};

  for (const id of unique) {
    let user = userStore.getUserById(id);
    if (!user) {
      try {
        user = (await userStore.fetchUserById(id)) ?? undefined;
      } catch {
        user = undefined;
      }
    }
    const item = user ? mapUserToOcPersonPickerItem(user) : null;
    map[id] = item?.title ?? id;
  }
  return map;
}

async function resolveSelectItems(ids: string[]): Promise<OcPersonPickerItem[]> {
  const unique = [...new Set(ids.map((id) => id.trim()).filter(Boolean))];
  if (!unique.length) return [];
  const userStore = useUserStore();
  const items: OcPersonPickerItem[] = [];

  for (const id of unique) {
    let user = userStore.getUserById(id);
    if (!user) {
      try {
        user = (await userStore.fetchUserById(id)) ?? undefined;
      } catch {
        user = undefined;
      }
    }
    const mapped = user ? mapUserToOcPersonPickerItem(user) : null;
    items.push(mapped ?? { value: id, title: id, subtitle: '' });
  }

  return items.sort((a, b) => a.title.localeCompare(b.title, 'tr'));
}

export async function loadDesignPersonnelSelectOptions(): Promise<OcPersonPickerItem[]> {
  const { loadOdakPackagePersonnelOnly } = await import('@/utils/odakSiparisHubSettingsService');
  const config = await loadOdakPackagePersonnelOnly();
  return resolveSelectItems(config.designPersonnelIds);
}

export async function loadManufacturePersonnelSelectOptions(): Promise<OcPersonPickerItem[]> {
  const { loadOdakPackagePersonnelOnly } = await import('@/utils/odakSiparisHubSettingsService');
  const config = await loadOdakPackagePersonnelOnly();
  return resolveSelectItems(config.manufacturePersonnelIds);
}

export async function ensurePersonnelOptionsIncludeSelected(
  items: OcPersonPickerItem[],
  selectedId: string | null | undefined
): Promise<OcPersonPickerItem[]> {
  const id = String(selectedId ?? '').trim();
  if (!id || items.some((x) => x.value === id)) return items;
  const [extra] = await resolveSelectItems([id]);
  return extra ? [extra, ...items] : items;
}
