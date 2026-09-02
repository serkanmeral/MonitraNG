import {
  diCreateFolder,
  diCreateMarkdown,
  diDelete,
  diGetChildren,
  diGetTreeRoots,
  diUpdateResourceMetadata,
} from '@/services/documentIntelligenceService';
import { pmUpdateProject } from '@/services/projectManagementService';
import type { DiResource, DiTreeNode } from '@/types/apps/documentIntelligence';
import type { PmJobPack } from '@/types/apps/projectManagement';

const DOCS_FOLDER_NAMES = ['Dökümanlar', 'Dokumanlar', 'Documents'];
const PROJECTS_FOLDER = 'Projeler';

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
  return (pack.folders || []).map((name) => String(name || '').trim()).filter(Boolean);
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

async function findChildFolder(parentId: string | null, names: string[]): Promise<DiTreeNode | DiResource | null> {
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

async function ensureFolder(parentId: string | null, name: string): Promise<string> {
  const existing = await findChildFolder(parentId, [name]);
  if (existing?.id) return existing.id;
  const created = await diCreateFolder({ name, parentId: parentId || undefined });
  return created.id;
}

async function findProjectHub(projectCode: string): Promise<string | null> {
  const docs = await findChildFolder(null, DOCS_FOLDER_NAMES);
  if (!docs?.id) return null;
  const projects = await findChildFolder(docs.id, [PROJECTS_FOLDER]);
  if (!projects?.id) return null;
  const hub = await findChildFolder(projects.id, [projectCode]);
  return hub?.id || null;
}

function isFolderEmpty(listing: { items?: unknown[]; total?: number | null }): boolean {
  const items = listing.items || [];
  if (items.length > 0) return false;
  return (listing.total ?? 0) <= 0;
}

export async function applyJobPackDocuments(projectId: string, projectCode: string, pack: PmJobPack): Promise<string | null> {
  const docs = await findChildFolder(null, DOCS_FOLDER_NAMES);
  if (!docs?.id) return null;

  const projectsId = await ensureFolder(docs.id, PROJECTS_FOLDER);
  const hubId = await ensureFolder(projectsId, projectCode);
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
