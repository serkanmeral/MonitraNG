/** Keeper — group capabilities (API-driven). */

export interface GroupCapabilities {
  canEdit: boolean;
  canDelete: boolean;
  canManageMembers: boolean;
  canChangeApplicationScope?: boolean;
}

export interface GroupProvisioningFields {
  provisioningSource?: string;
  directorySyncedAt?: string | Date | null;
  capabilities?: GroupCapabilities;
}

export function normalizeGroupCapabilities(
  raw: Record<string, unknown> | undefined | null
): GroupCapabilities | undefined {
  if (!raw || typeof raw !== 'object') return undefined;
  return {
    canEdit: Boolean(raw.canEdit ?? raw.CanEdit ?? true),
    canDelete: Boolean(raw.canDelete ?? raw.CanDelete ?? true),
    canManageMembers: Boolean(raw.canManageMembers ?? raw.CanManageMembers ?? true),
    canChangeApplicationScope: Boolean(raw.canChangeApplicationScope ?? raw.CanChangeApplicationScope ?? true),
  };
}

export function mapGroupProvisioningFromApi(group: Record<string, unknown>): GroupProvisioningFields {
  const src =
    (group.provisioningSource as string) ||
    (group.ProvisioningSource as string) ||
    'Local';
  const synced = (group.directorySyncedAt ?? group.DirectorySyncedAt) as
    | string
    | Date
    | null
    | undefined;
  const caps = normalizeGroupCapabilities(
    (group.capabilities ?? group.Capabilities) as Record<string, unknown> | undefined
  );
  return {
    provisioningSource: src,
    directorySyncedAt: synced ?? null,
    capabilities: caps,
  };
}

import { isDirectoryProvisioningSource } from './provisioningSourceUi';

export function isDirectoryGroup(
  group: { provisioningSource?: string } | null | undefined
): boolean {
  return isDirectoryProvisioningSource(group?.provisioningSource);
}

export function groupProvisioningSourceLabelKey(source?: string): string {
  return isDirectoryGroup({ provisioningSource: source })
    ? 'groups.source.directory'
    : 'groups.source.local';
}

export function canDeleteGroup(group: GroupProvisioningFields | null | undefined): boolean {
  if (group?.capabilities?.canDelete === false) return false;
  return !isDirectoryGroup(group);
}

export function canEditGroup(group: GroupProvisioningFields | null | undefined): boolean {
  if (group?.capabilities?.canEdit === false) return false;
  return !isDirectoryGroup(group);
}

export function canManageGroupMembers(
  group: GroupProvisioningFields | null | undefined
): boolean {
  if (group?.capabilities?.canManageMembers === false) return false;
  return !isDirectoryGroup(group);
}

export function canChangeGroupApplicationScope(
  group: GroupProvisioningFields | null | undefined
): boolean {
  if (group?.capabilities?.canChangeApplicationScope === false) return false;
  return true;
}

/** Liste/detay → düzenleme sayfası (AD gruplarında yalnızca uygulama kapsamı). */
export function canOpenGroupEditPage(
  group: GroupProvisioningFields | null | undefined
): boolean {
  return canEditGroup(group) || canChangeGroupApplicationScope(group);
}
