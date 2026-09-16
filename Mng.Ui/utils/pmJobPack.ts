import {
  diCreateFolder,
  diCreateMarkdown,
  diDelete,
  diGetById,
  diGetChildren,
  diGetTreeRoots,
  diUpdateResourceMetadata,
} from '@/services/documentIntelligenceService';
import { pmUpdateProject } from '@/services/projectManagementService';
import type { DiResource, DiTreeNode } from '@/types/apps/documentIntelligence';
import type { PmJobPack } from '@/types/apps/projectManagement';

export const DOCS_FOLDER_NAMES = ['Dökümanlar', 'Dokumanlar', 'Documents'];
export const PROJECTS_FOLDER = 'Projeler';

export type PmPackFolderAction = 'remove' | 'keep' | 'skip';

export interface PmPackFolderPreviewItem {
  name: string;
  action: PmPackFolderAction;
}

export interface PmPackFolderPreview {
  items: PmPackFolderPreviewItem[];
  removeCount: number;
  keepCount: number;
  skipCount: number;
}

export interface PmPackFolderDetachResult {
  removed: number;
  kept: number;
  skipped: number;
}

function folderName(node: { name?: string | null }): string {
  return String(node.name || '').trim();
}

function packFolderNames(pack: PmJobPack): string[] {
  return (pack.folders || [])
    .map((entry) => {
      if (typeof entry === 'string') return entry.trim();
      if (entry && typeof entry === 'object' && 'name' in entry) {
        return String((entry as { name?: unknown }).name || '').trim();
      }
      return '';
    })
    .filter(Boolean);
}

function claimedFolderNames(packs: PmJobPack[]): Set<string> {
  const claimed = new Set<string>();
  for (const pack of packs) {
    for (const name of packFolderNames(pack)) {
      claimed.add(name.toLocaleLowerCase('tr'));
    }
  }
  return claimed;
}

const folderEnsureLocks = new Map<string, Promise<string>>();

export async function findChildFolder(parentId: string | null, names: string[]): Promise<DiTreeNode | DiResource | null> {
  const wanted = names.map((n) => n.toLocaleLowerCase('tr'));
  if (!parentId) {
    const roots = await diGetTreeRoots();
    return roots.find((row) => wanted.includes(folderName(row).toLocaleLowerCase('tr'))) ?? null;
  }
  const children = await diGetChildren(parentId);
  return (
    (children.items || []).find(
      (row) => row.type === 'folder' && wanted.includes(folderName(row).toLocaleLowerCase('tr')),
    ) ?? null
  );
}

export async function ensureFolder(parentId: string | null, name: string, tags?: string[]): Promise<string> {
  const trimmed = name.trim();
  const key = `${parentId || ''}::${trimmed.toLocaleLowerCase('tr')}`;
  const pending = folderEnsureLocks.get(key);
  if (pending) return pending;

  let task: Promise<string>;
  task = (async () => {
    const existing = await findChildFolder(parentId, [trimmed]);
    if (existing?.id) return existing.id;
    try {
      const created = await diCreateFolder({
        name: trimmed,
        parentId: parentId || undefined,
        tags: tags?.length ? tags : undefined,
      });
      return created.id;
    } catch (error) {
      const raced = await findChildFolder(parentId, [trimmed]);
      if (raced?.id) return raced.id;
      throw error;
    }
  })().finally(() => {
    if (folderEnsureLocks.get(key) === task) folderEnsureLocks.delete(key);
  });

  folderEnsureLocks.set(key, task);
  return task;
}

export async function collapseEmptyDuplicateFolders(parentId: string): Promise<void> {
  const listing = await diGetChildren(parentId);
  const groups = new Map<string, DiResource[]>();
  for (const row of listing.items || []) {
    if (row.type !== 'folder') continue;
    const key = folderName(row).toLocaleLowerCase('tr');
    if (!key) continue;
    const group = groups.get(key) ?? [];
    group.push(row);
    groups.set(key, group);
  }

  for (const group of groups.values()) {
    if (group.length <= 1) continue;
    const ranked = [...group].sort((a, b) => String(a.createdAt || '').localeCompare(String(b.createdAt || '')));
    for (const extra of ranked.slice(1)) {
      const inner = await diGetChildren(extra.id);
      if (!isFolderEmpty(inner)) continue;
      await diDelete(extra.id, false);
    }
  }
}

export async function findProjectHub(projectCode: string): Promise<string | null> {
  const docs = await findChildFolder(null, DOCS_FOLDER_NAMES);
  if (!docs?.id) return null;
  const projects = await findChildFolder(docs.id, [PROJECTS_FOLDER]);
  if (!projects?.id) return null;
  const hub = await findChildFolder(projects.id, [projectCode]);
  return hub?.id || null;
}

export async function ensureProjectDocumentHub(
  projectCode: string,
  existingHubId?: string | null,
): Promise<string | null> {
  const code = projectCode.trim();
  if (!code) return null;
  const existing = existingHubId?.trim();
  if (existing) {
    try {
      const resource = await diGetById(existing);
      if (resource?.id && resource.type === 'folder') return resource.id;
    } catch {
      // stale hub id — recreate under Dökümanlar/Projeler
    }
  }
  const found = await findProjectHub(code);
  if (found) return found;
  const docs = await findChildFolder(null, DOCS_FOLDER_NAMES);
  if (!docs?.id) return null;
  const projectsId = await ensureFolder(docs.id, PROJECTS_FOLDER);
  return ensureFolder(projectsId, code);
}

function isFolderEmpty(listing: { items?: unknown[]; total?: number | null }): boolean {
  const items = listing.items || [];
  if (items.length > 0) return false;
  return (listing.total ?? 0) <= 0;
}

export async function applyJobPackDocuments(projectId: string, projectCode: string, pack: PmJobPack): Promise<string | null> {
  const hubId = await ensureProjectDocumentHub(projectCode);
  if (!hubId) return null;
  const folderIds = new Map<string, string>();
  for (const name of packFolderNames(pack)) {
    folderIds.set(name, await ensureFolder(hubId, name));
  }

  for (const starter of pack.starters || []) {
    const parent = folderIds.get(starter.folder);
    if (!parent) continue;
    const listing = await diGetChildren(parent);
    const exists = (listing.items || []).some(
      (row) => row.type === 'markdown' && (row.title === starter.title || row.name === starter.title),
    );
    if (exists) continue;
    const created = await diCreateMarkdown({
      parentId: parent,
      title: starter.title,
      content: starter.body || '',
      isDraft: false,
    });
    if (starter.kind && created.id) {
      await diUpdateResourceMetadata(created.id, { kind: starter.kind });
    }
  }

  await collapseEmptyDuplicateFolders(hubId);
  await pmUpdateProject(projectId, { diFolderId: hubId });
  return hubId;
}

export async function previewJobPackFolders(
  projectCode: string,
  pack: PmJobPack,
  remainingPacks: PmJobPack[],
): Promise<PmPackFolderPreview> {
  const claimed = claimedFolderNames(remainingPacks);
  const hubId = await findProjectHub(projectCode);
  const items: PmPackFolderPreviewItem[] = [];

  for (const name of packFolderNames(pack)) {
    if (claimed.has(name.toLocaleLowerCase('tr'))) {
      items.push({ name, action: 'keep' });
      continue;
    }
    if (!hubId) {
      items.push({ name, action: 'skip' });
      continue;
    }
    const folder = await findChildFolder(hubId, [name]);
    if (!folder?.id) {
      items.push({ name, action: 'skip' });
      continue;
    }
    const listing = await diGetChildren(folder.id);
    items.push({ name, action: isFolderEmpty(listing) ? 'remove' : 'keep' });
  }

  return {
    items,
    removeCount: items.filter((row) => row.action === 'remove').length,
    keepCount: items.filter((row) => row.action === 'keep').length,
    skipCount: items.filter((row) => row.action === 'skip').length,
  };
}

export async function detachJobPackDocuments(
  projectCode: string,
  pack: PmJobPack,
  remainingPacks: PmJobPack[],
): Promise<PmPackFolderDetachResult> {
  const preview = await previewJobPackFolders(projectCode, pack, remainingPacks);
  const hubId = await findProjectHub(projectCode);
  let removed = 0;
  let kept = preview.keepCount;
  let skipped = preview.skipCount;

  if (!hubId) {
    return { removed: 0, kept, skipped };
  }

  for (const row of preview.items) {
    if (row.action !== 'remove') continue;
    const folder = await findChildFolder(hubId, [row.name]);
    if (!folder?.id) {
      skipped += 1;
      continue;
    }
    const listing = await diGetChildren(folder.id);
    if (!isFolderEmpty(listing)) {
      kept += 1;
      continue;
    }
    await diDelete(folder.id, false);
    removed += 1;
  }

  return { removed, kept, skipped };
}
