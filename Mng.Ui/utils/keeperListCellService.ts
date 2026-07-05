import type { User } from '@/stores/apps/user';
import type { Group } from '@/stores/apps/group';
import { userProvisioningSourceLabelKey } from '@/utils/userFieldPolicy';
import { groupProvisioningSourceLabelKey } from '@/utils/groupFieldPolicy';

export type KeeperUserListRow = User & { fullName?: string };
export type KeeperGroupListRow = Group;

function formatKeeperDate(value: string | Date | null | undefined, locale = 'tr-TR'): string {
  if (!value) return '—';
  try {
    return new Date(value).toLocaleDateString(locale, {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    });
  } catch {
    return '—';
  }
}

export function keeperUserListCellRaw(
  item: KeeperUserListRow,
  listKey: string,
  t: (key: string) => string
): string {
  switch (listKey) {
    case 'username':
      return item.username ?? '—';
    case 'email':
      return item.email ?? '—';
    case 'fullName':
      return `${item.firstName ?? ''} ${item.lastName ?? ''}`.trim() || '—';
    case 'department':
      return item.department?.trim() || '—';
    case 'title':
      return item.title?.trim() || '—';
    case 'provisioningSource':
      return t(userProvisioningSourceLabelKey(item.provisioningSource));
    case 'isActive':
      return item.isActive ? t('users.status.active') : t('users.status.inactive');
    case 'includeInApplication':
      return item.includeInApplication !== false
        ? t('users.applicationScope.inApp')
        : t('users.applicationScope.hidden');
    case 'groups':
      return (item.groups ?? []).join(', ') || t('users.groups.none');
    case 'createdAt':
    case 'updatedAt':
    case 'lastLoginAt':
      return formatKeeperDate(item[listKey as keyof KeeperUserListRow] as string | Date | null);
    default:
      return '—';
  }
}

export function keeperGroupListCellRaw(
  item: KeeperGroupListRow,
  listKey: string,
  t: (key: string) => string
): string {
  switch (listKey) {
    case 'name':
      return item.name ?? '—';
    case 'description':
      return item.description?.trim() || '—';
    case 'provisioningSource':
      return t(groupProvisioningSourceLabelKey(item.provisioningSource));
    case 'isActive':
      return item.isActive ? t('groups.list.statusActive') : t('groups.list.statusInactive');
    case 'includeInApplication':
      return item.includeInApplication !== false
        ? t('groups.applicationScope.inApp')
        : t('groups.applicationScope.hidden');
    case 'memberCount':
      return String(item.memberCount ?? 0);
    case 'createdAt':
    case 'updatedAt':
      return formatKeeperDate(item[listKey as keyof KeeperGroupListRow] as string | Date | null);
    default:
      return '—';
  }
}

export function keeperUserListFieldLabel(fieldName: string, t: (key: string) => string): string {
  const map: Record<string, string> = {
    username: 'users.table.username',
    provisioningSource: 'users.table.source',
    email: 'users.table.email',
    fullName: 'users.table.fullName',
    department: 'users.details.fields.department',
    title: 'users.details.fields.title',
    isActive: 'users.table.status',
    includeInApplication: 'users.applicationScope.column',
    groups: 'users.table.groups',
    createdAt: 'users.table.createdAt',
    updatedAt: 'users.details.fields.updatedAt',
    lastLoginAt: 'users.details.fields.lastLogin',
  };
  const key = map[fieldName];
  return key ? t(key) : fieldName;
}

export function keeperGroupListFieldLabel(fieldName: string, t: (key: string) => string): string {
  const map: Record<string, string> = {
    name: 'groups.table.name',
    provisioningSource: 'groups.table.source',
    description: 'groups.create.description',
    isActive: 'groups.table.status',
    includeInApplication: 'groups.applicationScope.column',
    memberCount: 'groups.table.memberCount',
    createdAt: 'groups.table.createdAt',
    updatedAt: 'groups.details.fields.updatedAt',
  };
  const key = map[fieldName];
  return key ? t(key) : fieldName;
}
