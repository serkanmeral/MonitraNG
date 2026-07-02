import type { Group } from '@/stores/apps/group';
import type { User } from '@/stores/apps/user';
import {
  buildOcPersonPickerSubtitle,
  buildOcPersonPickerTitle,
  collectPersonIdsFromValue,
  resolveUserPickerValue,
} from '@/utils/ocPersonPicker';

export const KEEPER_DIRECTORY_PICKER_PAGE_SIZE = 20;
/** Sunucu tarafı "hepsini seç" üst sınırı (mevcut arama filtresine göre). */
export const KEEPER_DIRECTORY_PICKER_SELECT_ALL_MAX = 500;

/**
 * Sunucu sayfalı directory picker için mevcut arama filtresine uyan tüm satırları getirir.
 */
export async function fetchAllMatchingDirectoryRows(options: {
  totalItems: number;
  fetchBatch: (page: number, pageSize: number) => Promise<KeeperDirectoryPickerRow[]>;
  getBatchTotal?: () => number;
  maxItems?: number;
  batchSize?: number;
}): Promise<KeeperDirectoryPickerRow[]> {
  const cap = Math.min(
    options.totalItems || 0,
    options.maxItems ?? KEEPER_DIRECTORY_PICKER_SELECT_ALL_MAX
  );
  if (cap <= 0) return [];

  const batchSize = Math.min(options.batchSize ?? 100, cap);
  const all: KeeperDirectoryPickerRow[] = [];
  const seen = new Set<string>();
  let currentPage = 1;

  while (all.length < cap) {
    const batch = await options.fetchBatch(currentPage, batchSize);
    if (!batch.length) break;

    for (const row of batch) {
      if (seen.has(row.value)) continue;
      seen.add(row.value);
      all.push(row);
      if (all.length >= cap) break;
    }

    const reportedTotal = options.getBatchTotal?.() ?? options.totalItems;
    if (
      batch.length < batchSize ||
      all.length >= cap ||
      currentPage * batchSize >= (reportedTotal || 0)
    ) {
      break;
    }
    currentPage++;
  }

  return all;
}

export type KeeperDirectoryEntity = 'user' | 'group';

/** Grup model değeri: id (varsayılan) veya ad (Odak/dashboard gibi ad tabanlı API'ler). */
export type KeeperGroupValueKey = 'id' | 'name';

export type KeeperDirectoryPickerRow = {
  title: string;
  value: string;
  subtitle?: string;
  raw: Record<string, unknown>;
};

export function collectDirectoryIdsFromValue(value: unknown): string[] {
  return collectPersonIdsFromValue(value);
}

export function resolveGroupPickerValue(
  group: Group,
  valueKey: KeeperGroupValueKey = 'id'
): string {
  if (valueKey === 'name') {
    return (group.name ?? group.groupName ?? '').trim();
  }
  return (group.id || group.groupId || '').trim();
}

export function buildGroupPickerTitle(group: Group): string {
  const name = group.name?.trim();
  return name || resolveGroupPickerValue(group) || '—';
}

export function buildGroupPickerSubtitle(group: Group): string {
  const parts: string[] = [];
  const desc = group.description?.trim();
  if (desc) parts.push(desc);
  if (group.memberCount > 0) parts.push(String(group.memberCount));
  return parts.join(' · ');
}

export function mapUserToDirectoryRow(user: User): KeeperDirectoryPickerRow | null {
  const value = resolveUserPickerValue(user);
  if (!value) return null;
  return {
    value,
    title: buildOcPersonPickerTitle(user),
    subtitle: buildOcPersonPickerSubtitle(user),
    raw: {
      username: user.username ?? '',
      email: user.email ?? '',
      department: user.department ?? '',
      title: user.title ?? '',
    },
  };
}

export function mapGroupToDirectoryRow(
  group: Group,
  valueKey: KeeperGroupValueKey = 'id'
): KeeperDirectoryPickerRow | null {
  const value = resolveGroupPickerValue(group, valueKey);
  if (!value) return null;
  return {
    value,
    title: buildGroupPickerTitle(group),
    subtitle: buildGroupPickerSubtitle(group),
    raw: {
      description: group.description ?? '',
      memberCount: group.memberCount ?? 0,
    },
  };
}

export function mapUserLookupToDirectoryRow(item: Record<string, unknown>): KeeperDirectoryPickerRow | null {
  const user = {
    id: String(item.userId ?? item.UserId ?? ''),
    userId: String(item.userId ?? item.UserId ?? ''),
    username: String(item.username ?? item.Username ?? ''),
    email: String(item.email ?? item.Email ?? ''),
    firstName: String(item.firstName ?? item.FirstName ?? ''),
    lastName: String(item.lastName ?? item.LastName ?? ''),
    title: (item.title ?? item.Title ?? null) as string | null,
    department: null,
    domainId: '',
    isActive: Boolean(item.isActive ?? item.IsActive ?? true),
    groups: [],
    createdAt: new Date(),
  } satisfies User;
  return mapUserToDirectoryRow(user);
}

export function mapGroupLookupToDirectoryRow(item: Record<string, unknown>): KeeperDirectoryPickerRow | null {
  const group = {
    id: String(item.groupId ?? item.GroupId ?? ''),
    groupId: String(item.groupId ?? item.GroupId ?? ''),
    name: String(item.name ?? item.Name ?? ''),
    memberCount: Number(item.memberCount ?? item.MemberCount ?? 0),
    isActive: Boolean(item.isActive ?? item.IsActive ?? true),
    createdAt: new Date(),
  } satisfies Group;
  return mapGroupToDirectoryRow(group);
}
