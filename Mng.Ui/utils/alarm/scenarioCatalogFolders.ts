export type ScenarioCatalogRoot = 'product' | 'user';

export interface ScenarioCatalogFolder {
  id: string;
  name: string;
  /** Null means the folder hangs under its root (`product` / `user`). */
  parentId: string | null;
  root: ScenarioCatalogRoot;
}

export interface ScenarioCatalogFolderState {
  folders: ScenarioCatalogFolder[];
  /** scenarioId → folderId */
  placements: Record<string, string>;
  expandedIds: string[];
}

export const PRODUCT_ROOT_ID = 'root:product';
export const USER_ROOT_ID = 'root:user';

const STORAGE_PREFIX = 'siem-scenario-catalog-folders-v1';

function storageKey(domainName: string): string {
  return `${STORAGE_PREFIX}:${domainName || 'default'}`;
}

export function rootIdFor(root: ScenarioCatalogRoot): string {
  return root === 'product' ? PRODUCT_ROOT_ID : USER_ROOT_ID;
}

export function createDefaultFolderState(): ScenarioCatalogFolderState {
  return {
    folders: [],
    placements: {},
    expandedIds: [PRODUCT_ROOT_ID, USER_ROOT_ID],
  };
}

export function loadScenarioCatalogFolders(domainName: string): ScenarioCatalogFolderState {
  if (typeof window === 'undefined') return createDefaultFolderState();
  try {
    const raw = localStorage.getItem(storageKey(domainName));
    if (!raw) return createDefaultFolderState();
    const parsed = JSON.parse(raw) as Partial<ScenarioCatalogFolderState>;
    const folders = Array.isArray(parsed.folders)
      ? parsed.folders.filter((f): f is ScenarioCatalogFolder =>
        !!f
        && typeof f.id === 'string'
        && typeof f.name === 'string'
        && (f.root === 'product' || f.root === 'user')
        && (f.parentId === null || typeof f.parentId === 'string'))
      : [];
    const placements = parsed.placements && typeof parsed.placements === 'object'
      ? Object.fromEntries(
        Object.entries(parsed.placements).filter((entry): entry is [string, string] =>
          typeof entry[0] === 'string' && typeof entry[1] === 'string'),
      )
      : {};
    const expandedIds = Array.isArray(parsed.expandedIds)
      ? parsed.expandedIds.filter((id): id is string => typeof id === 'string')
      : [PRODUCT_ROOT_ID, USER_ROOT_ID];
    if (!expandedIds.includes(PRODUCT_ROOT_ID)) expandedIds.push(PRODUCT_ROOT_ID);
    if (!expandedIds.includes(USER_ROOT_ID)) expandedIds.push(USER_ROOT_ID);
    return { folders, placements, expandedIds };
  } catch {
    return createDefaultFolderState();
  }
}

export function saveScenarioCatalogFolders(domainName: string, state: ScenarioCatalogFolderState): void {
  if (typeof window === 'undefined') return;
  try {
    localStorage.setItem(storageKey(domainName), JSON.stringify(state));
  } catch {
    // ignore quota / private mode
  }
}

export function createScenarioCatalogFolder(
  state: ScenarioCatalogFolderState,
  input: { name: string; root: ScenarioCatalogRoot; parentId: string | null },
): ScenarioCatalogFolderState {
  const name = input.name.trim();
  if (!name) return state;
  const parentId = input.parentId && input.parentId.startsWith('root:')
    ? null
    : input.parentId;
  if (parentId) {
    const parent = state.folders.find(f => f.id === parentId);
    if (!parent || parent.root !== input.root) return state;
  }
  const folder: ScenarioCatalogFolder = {
    id: `folder-${crypto.randomUUID()}`,
    name,
    parentId,
    root: input.root,
  };
  const expanded = new Set(state.expandedIds);
  expanded.add(rootIdFor(input.root));
  if (parentId) expanded.add(parentId);
  expanded.add(folder.id);
  return {
    ...state,
    folders: [...state.folders, folder],
    expandedIds: [...expanded],
  };
}

export function renameScenarioCatalogFolder(
  state: ScenarioCatalogFolderState,
  folderId: string,
  name: string,
): ScenarioCatalogFolderState {
  const trimmed = name.trim();
  if (!trimmed) return state;
  return {
    ...state,
    folders: state.folders.map(f => (f.id === folderId ? { ...f, name: trimmed } : f)),
  };
}

export function deleteScenarioCatalogFolder(
  state: ScenarioCatalogFolderState,
  folderId: string,
): ScenarioCatalogFolderState | null {
  const folder = state.folders.find(f => f.id === folderId);
  if (!folder) return null;
  if (state.folders.some(f => f.parentId === folderId)) return null;
  if (Object.values(state.placements).includes(folderId)) return null;
  return {
    ...state,
    folders: state.folders.filter(f => f.id !== folderId),
    expandedIds: state.expandedIds.filter(id => id !== folderId),
  };
}

export function placeScenarioInFolder(
  state: ScenarioCatalogFolderState,
  scenarioId: string,
  folderId: string | null,
): ScenarioCatalogFolderState {
  const placements = { ...state.placements };
  if (!folderId || folderId.startsWith('root:')) {
    delete placements[scenarioId];
  } else {
    placements[scenarioId] = folderId;
  }
  return { ...state, placements };
}

export function toggleScenarioCatalogExpanded(
  state: ScenarioCatalogFolderState,
  id: string,
): ScenarioCatalogFolderState {
  const expanded = new Set(state.expandedIds);
  if (expanded.has(id)) expanded.delete(id);
  else expanded.add(id);
  return { ...state, expandedIds: [...expanded] };
}

export function childFoldersOf(
  state: ScenarioCatalogFolderState,
  root: ScenarioCatalogRoot,
  parentId: string | null,
): ScenarioCatalogFolder[] {
  return state.folders
    .filter(f => f.root === root && f.parentId === parentId)
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
}
