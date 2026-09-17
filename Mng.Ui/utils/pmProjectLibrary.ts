import { diCreateTag, diGetChildren, diListTags } from '@/services/documentIntelligenceService';
import { pmUpdateProject } from '@/services/projectManagementService';
import type { DiResource, DiTag } from '@/types/apps/documentIntelligence';
import { collapseEmptyDuplicateFolders, ensureFolder, ensureProjectDocumentHub, findChildFolder } from '@/utils/pmJobPack';

export const PM_LIBRARY_MISSING_ROOT = 'PM_LIBRARY_MISSING_ROOT';
export const PM_LIBRARY_DEFAULT_FOLDERS = ['Wiki', 'Kararlar', 'Toplantı notları', 'Yüklemeler'] as const;
export const PM_LIBRARY_DECISIONS_FOLDER = 'Kararlar';
export const PM_LIBRARY_MEETINGS_FOLDER = 'Toplantı notları';
export const PM_LIBRARY_DECISION_PICK_FOLDERS = ['Kararlar', 'Wiki'] as const;

export type PmLibraryTagSpec = {
  name: string;
  color: string;
  description: string;
};

/** Default library folders → organizational catalog tag names (must exist in dm_tags). */
export const PM_LIBRARY_FOLDER_TAGS: Record<string, PmLibraryTagSpec> = {
  Wiki: { name: 'Wiki', color: 'teal', description: 'Project wiki page' },
  Kararlar: { name: 'Karar', color: 'indigo', description: 'Project decision document' },
  'Toplantı notları': { name: 'Toplantı', color: 'orange', description: 'Meeting notes' },
  Yüklemeler: { name: 'Yükleme', color: 'blue-grey', description: 'Project upload' },
};

export type PmLibraryDocumentHit = {
  folder: string;
  resource: DiResource;
};

const libraryEnsureLocks = new Map<string, Promise<string>>();
let libraryTagEnsure: Promise<Map<string, DiTag>> | null = null;
let libraryTagCatalog = new Map<string, DiTag>();

function tagKey(name: string): string {
  return name.trim().toLocaleLowerCase('tr');
}

function resolveFolderTagSpec(name: string | null | undefined): PmLibraryTagSpec | null {
  const normalized = String(name || '').trim().toLocaleLowerCase('tr');
  if (!normalized) return null;
  for (const [folder, spec] of Object.entries(PM_LIBRARY_FOLDER_TAGS)) {
    if (folder.toLocaleLowerCase('tr') === normalized) return spec;
  }
  return null;
}

export class PmLibraryError extends Error {
  readonly code: string;

  constructor(code: string, message: string) {
    super(message);
    this.name = 'PmLibraryError';
    this.code = code;
  }
}

export function isPmLibraryMissingRoot(error: unknown): boolean {
  return error instanceof PmLibraryError && error.code === PM_LIBRARY_MISSING_ROOT;
}

export function isPmLibraryDefaultFolder(name: string | null | undefined): boolean {
  const normalized = String(name || '').trim().toLocaleLowerCase('tr');
  return PM_LIBRARY_DEFAULT_FOLDERS.some((folder) => folder.toLocaleLowerCase('tr') === normalized);
}

export async function ensureProjectLibrary(
  projectId: string,
  projectCode: string,
  existingHubId?: string | null,
): Promise<string> {
  const key = projectId.trim();
  const pending = libraryEnsureLocks.get(key);
  if (pending) return pending;

  let task: Promise<string>;
  task = ensureProjectLibraryOnce(projectId, projectCode, existingHubId).finally(() => {
    if (libraryEnsureLocks.get(key) === task) libraryEnsureLocks.delete(key);
  });
  libraryEnsureLocks.set(key, task);
  return task;
}

async function ensureProjectLibraryOnce(
  projectId: string,
  projectCode: string,
  existingHubId?: string | null,
): Promise<string> {
  const hubId = await ensureProjectDocumentHub(projectCode, existingHubId);
  if (!hubId) {
    throw new PmLibraryError(PM_LIBRARY_MISSING_ROOT, 'DI documents root is missing');
  }

  for (const name of PM_LIBRARY_DEFAULT_FOLDERS) {
    await ensureFolder(hubId, name);
  }
  await collapseEmptyDuplicateFolders(hubId);

  if ((existingHubId || '').trim() !== hubId) {
    await pmUpdateProject(projectId, { diFolderId: hubId });
  }
  return hubId;
}

export async function ensureProjectDecisionsFolder(
  projectId: string,
  projectCode: string,
  existingHubId?: string | null,
): Promise<{ hubId: string; folderId: string }> {
  const hubId = await ensureProjectLibrary(projectId, projectCode, existingHubId);
  const folderId = await ensureFolder(hubId, PM_LIBRARY_DECISIONS_FOLDER);
  return { hubId, folderId };
}

export async function ensureProjectMeetingsFolder(
  projectId: string,
  projectCode: string,
  existingHubId?: string | null,
): Promise<{ hubId: string; folderId: string }> {
  const hubId = await ensureProjectLibrary(projectId, projectCode, existingHubId);
  const folderId = await ensureFolder(hubId, PM_LIBRARY_MEETINGS_FOLDER);
  return { hubId, folderId };
}

export async function listProjectLibraryDocuments(hubId: string): Promise<PmLibraryDocumentHit[]> {
  const hits: PmLibraryDocumentHit[] = [];
  for (const folderName of PM_LIBRARY_DECISION_PICK_FOLDERS) {
    const folder = await findChildFolder(hubId, [folderName]);
    if (!folder?.id) continue;
    const listing = await diGetChildren(folder.id);
    for (const row of listing.items || []) {
      if (row.type === 'folder') continue;
      hits.push({ folder: folderName, resource: row });
    }
  }
  return hits;
}

export function libraryTagNamesForFolderPath(folderNames: Array<string | null | undefined>): string[] {
  const names: string[] = [];
  const seen = new Set<string>();
  for (const folder of folderNames) {
    const spec = resolveFolderTagSpec(folder);
    if (!spec) continue;
    const key = tagKey(spec.name);
    if (seen.has(key)) continue;
    seen.add(key);
    names.push(spec.name);
  }
  return names;
}

export function pmLibraryTagColor(name: string | null | undefined): string | undefined {
  const tag = libraryTagCatalog.get(tagKey(String(name || '')));
  return tag?.color || undefined;
}

export async function ensurePmLibraryTags(): Promise<Map<string, DiTag>> {
  if (libraryTagEnsure) return libraryTagEnsure;

  const run = (async () => {
    const listed = await diListTags(true);
    const byName = new Map<string, DiTag>();
    for (const tag of listed.items || []) {
      byName.set(tagKey(tag.name), tag);
    }

    for (const spec of Object.values(PM_LIBRARY_FOLDER_TAGS)) {
      if (byName.has(tagKey(spec.name))) continue;
      try {
        const created = await diCreateTag({
          name: spec.name,
          color: spec.color,
          description: spec.description,
          kind: 'organizational',
          isActive: true,
          persistToFile: false,
        });
        byName.set(tagKey(created.name), created);
      } catch {
        const again = await diListTags(true);
        for (const tag of again.items || []) {
          byName.set(tagKey(tag.name), tag);
        }
      }
    }

    libraryTagCatalog = byName;
    return byName;
  })();

  libraryTagEnsure = run.catch((error) => {
    libraryTagEnsure = null;
    throw error;
  });
  return libraryTagEnsure;
}

export async function resolvePmLibraryAutoTags(
  folderNames: Array<string | null | undefined>,
): Promise<string[] | undefined> {
  const names = libraryTagNamesForFolderPath(folderNames);
  if (!names.length) return undefined;
  try {
    const catalog = await ensurePmLibraryTags();
    const resolved = names.filter((name) => catalog.has(tagKey(name)));
    return resolved.length ? resolved : undefined;
  } catch {
    return undefined;
  }
}
