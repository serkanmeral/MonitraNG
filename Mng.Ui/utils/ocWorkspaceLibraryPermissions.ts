export interface LibraryGroupRef {
  id: string;
  name: string;
}

export interface LibraryGrant {
  groupId: string;
  groupName: string;
  permissions: string[];
}

const VIEW_ACTIONS = ['view', 'download'];
const EDIT_ACTIONS = ['view', 'create', 'edit', 'delete', 'upload', 'download', 'move'];
const ADMIN_ACTIONS = [...EDIT_ACTIONS, 'share'];

function fold(value: string): string {
  return value.trim().toLocaleLowerCase('tr');
}

/** Görme, düzenleme ve yönetim listelerinden klasör iznini üretir. Sahip grubu belge izni almaz. */
export function buildLibraryGrants(
  viewGroups: string[],
  editGroups: string[],
  adminGroups: string[],
  known: LibraryGroupRef[],
): LibraryGrant[] {
  const byId = new Map<string, LibraryGroupRef>();
  const byName = new Map<string, LibraryGroupRef>();
  for (const group of known) {
    const id = group.id.trim();
    const name = group.name.trim();
    if (!id || !name) continue;
    const ref = { id, name };
    byId.set(id, ref);
    byName.set(fold(name), ref);
  }

  const actions = new Map<string, { groupId: string; groupName: string; permissions: Set<string> }>();

  function grant(tokens: string[], actionList: string[]) {
    for (const token of tokens) {
      const key = token.trim();
      if (!key) continue;
      const group = byId.get(key) ?? byName.get(fold(key));
      if (!group) continue;
      const row = actions.get(group.id) ?? {
        groupId: group.id,
        groupName: group.name,
        permissions: new Set<string>(),
      };
      for (const action of actionList) row.permissions.add(action);
      actions.set(group.id, row);
    }
  }

  grant(viewGroups, VIEW_ACTIONS);
  grant(editGroups, EDIT_ACTIONS);
  grant(adminGroups, ADMIN_ACTIONS);

  return [...actions.values()]
    .map((row) => ({
      groupId: row.groupId,
      groupName: row.groupName,
      permissions: [...row.permissions].sort(),
    }))
    .sort((left, right) => left.groupName.localeCompare(right.groupName, 'tr'));
}

/** Son yazılan kopyanın imzası. Boş liste açık klasör demektir. Sıra imzayı değiştirmez. */
export function libraryPermissionStamp(grants: LibraryGrant[]): string {
  if (!grants.length) return 'open';
  return [...grants]
    .sort((left, right) => fold(left.groupName).localeCompare(fold(right.groupName), 'tr'))
    .map((grant) => `${fold(grant.groupName)}\u001f${grant.permissions.map(fold).sort().join(',')}`)
    .join('\u001e');
}

export function folderPermissionStamp(folder: {
  inheritanceBroken: boolean;
  groups: { groupName: string; permissions: string[] }[];
}): string {
  if (!folder.inheritanceBroken) return 'open';
  const grants: LibraryGrant[] = folder.groups
    .map((group) => ({
      groupId: '',
      groupName: group.groupName.trim(),
      permissions: [...group.permissions].map(fold).filter(Boolean).sort(),
    }))
    .filter((group) => group.groupName && group.permissions.length);
  return libraryPermissionStamp(grants);
}

export interface LibrarySyncDecision {
  write: boolean;
  detached: boolean;
  /** null: henüz bir kopya yazılmadı. */
  stamp: string | null;
}

/**
 * Kök izin son kopyayla aynıysa yeniden yazılır.
 * Kök izin son kopyadan farklıysa klasör ayrılmış sayılır.
 * Damga yokken açık klasör veya kopyayla aynı klasör yazılır. Elle kurulmuş izin ayrılır.
 */
export function decideLibrarySync(input: {
  storedStamp: string | null;
  detached: boolean;
  actualStamp: string;
  desiredStamp: string;
  force: boolean;
}): LibrarySyncDecision {
  if (input.force) {
    return { write: true, detached: false, stamp: input.desiredStamp };
  }

  const stored = input.storedStamp?.trim() || '';
  if (stored) {
    if (input.actualStamp === stored) {
      return {
        write: input.actualStamp !== input.desiredStamp,
        detached: false,
        stamp: input.desiredStamp,
      };
    }
    return { write: false, detached: true, stamp: stored };
  }

  if (input.detached) {
    return { write: false, detached: true, stamp: null };
  }

  if (input.actualStamp === 'open' || input.actualStamp === input.desiredStamp) {
    return {
      write: input.actualStamp !== input.desiredStamp,
      detached: false,
      stamp: input.desiredStamp,
    };
  }

  return { write: false, detached: true, stamp: null };
}
