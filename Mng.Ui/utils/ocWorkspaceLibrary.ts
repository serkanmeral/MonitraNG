import { diBreakInheritance, diGetById, diGetPermissions, diRename, diRestoreInheritance, diSetPermissions } from '@/services/documentIntelligenceService';
import { ocGetWorkspace, ocUpdateWorkspace } from '@/services/operationCoreService';
import { pmListProjects, pmUpdateProject } from '@/services/projectManagementService';
import { useAuthStore } from '@/stores/auth';
import { useGroupStore } from '@/stores/apps/group';
import type { DiFolderPermissions } from '@/types/apps/documentIntelligence';
import type { OpWorkspaceDetail } from '@/types/apps/operationCore';
import type { PmProject } from '@/types/apps/projectManagement';
import {
  DOCS_FOLDER_NAMES,
  ensureFolder,
  ensureProjectDocumentHub,
  findChildFolder,
} from '@/utils/pmJobPack';
import {
  buildLibraryGrants,
  decideLibrarySync,
  folderPermissionStamp,
  libraryPermissionStamp,
  type LibraryGrant,
  type LibraryGroupRef,
} from '@/utils/ocWorkspaceLibraryPermissions';

export const OC_LIBRARY_FOLDER = 'Operasyon';

export interface OcLibrarySyncResult {
  folderId: string | null;
  detached: boolean;
  canRewrite: boolean;
  failed: boolean;
}

const resolveLocks = new Map<string, Promise<OcLibrarySyncResult>>();

const emptyResult: OcLibrarySyncResult = {
  folderId: null,
  detached: false,
  canRewrite: false,
  failed: false,
};

function sameFolderName(left: string, right: string) {
  return left.trim().toLocaleLowerCase('tr') === right.trim().toLocaleLowerCase('tr');
}

/** A folder still labeled with the workspace code takes the workspace name. Anything else is left as it is. */
async function alignCodeFolderName(folderId: string, currentName: string, folderName: string, legacyCode: string) {
  if (!folderName || sameFolderName(currentName, folderName)) return;
  if (!legacyCode || !sameFolderName(currentName, legacyCode)) return;
  try {
    await diRename(folderId, { name: folderName });
  } catch {
    // The library stays on this folder even when the label cannot be updated.
  }
}

async function ensureOwnFolder(
  workspaceId: string,
  folderName: string,
  legacyCode: string,
  existingId: string | null,
) {
  const stored = existingId?.trim();
  if (stored) {
    try {
      const resource = await diGetById(stored);
      if (resource?.id && resource.type === 'folder') {
        await alignCodeFolderName(resource.id, resource.name || '', folderName, legacyCode);
        return resource.id;
      }
    } catch {
      // Stale id. Look up or create the folder again.
    }
  }

  const docs = await findChildFolder(null, DOCS_FOLDER_NAMES);
  if (!docs?.id) return null;
  const operationId = await ensureFolder(docs.id, OC_LIBRARY_FOLDER);
  let found = await findChildFolder(operationId, [folderName]);
  if (!found?.id && legacyCode && !sameFolderName(legacyCode, folderName)) {
    found = await findChildFolder(operationId, [legacyCode]);
  }
  const folderId = found?.id || (await ensureFolder(operationId, folderName));
  if (found?.id) await alignCodeFolderName(found.id, found.name || '', folderName, legacyCode);
  if (folderId !== stored) {
    await ocUpdateWorkspace(workspaceId, { diFolderId: folderId });
  }
  return folderId;
}

async function knownGroups(tokens: string[]): Promise<LibraryGroupRef[]> {
  const unique = [...new Set(tokens.map((token) => token.trim()).filter(Boolean))];
  if (!unique.length) return [];

  const store = useGroupStore();
  const found = await store.fetchGroupsByIds(unique);
  const refs = new Map<string, LibraryGroupRef>();
  for (const group of found) {
    const id = (group.groupId || group.id || '').trim();
    const name = group.name.trim();
    if (id && name) refs.set(id, { id, name });
  }

  const knownTokens = new Set<string>();
  for (const ref of refs.values()) {
    knownTokens.add(ref.id);
    knownTokens.add(ref.name.toLocaleLowerCase('tr'));
  }
  const missing = unique.filter((token) => !knownTokens.has(token) && !knownTokens.has(token.toLocaleLowerCase('tr')));
  if (missing.length) {
    await store.fetchGroups({ page: 1, pageSize: 200, isActive: true });
    for (const group of store.groups) {
      const id = (group.groupId || group.id || '').trim();
      const name = group.name.trim();
      if (!id || !name) continue;
      const nameKey = name.toLocaleLowerCase('tr');
      if (missing.some((token) => token === id || token.toLocaleLowerCase('tr') === nameKey)) {
        refs.set(id, { id, name });
      }
    }
  }

  return [...refs.values()];
}

function canRewriteLibrary(workspace: OpWorkspaceDetail, permissions: DiFolderPermissions): boolean {
  const auth = useAuthStore();
  if (auth.isAdmin || permissions.effective?.canShare) return true;
  const mine = new Set(auth.userGroups.map((group) => group.trim().toLocaleLowerCase('tr')));
  return (workspace.adminGroups ?? []).some((token) => mine.has(token.trim().toLocaleLowerCase('tr')));
}

async function writeDesired(folderId: string, inheritanceBroken: boolean, grants: LibraryGrant[]) {
  if (!grants.length) {
    if (inheritanceBroken) await diRestoreInheritance(folderId);
    return;
  }
  if (!inheritanceBroken) await diBreakInheritance(folderId);
  await diSetPermissions(folderId, {
    groups: grants.map((grant) => ({
      groupId: grant.groupId,
      groupName: grant.groupName,
      permissions: grant.permissions,
    })),
  });
}

async function syncFolder(
  workspace: OpWorkspaceDetail,
  folderId: string,
  force: boolean,
): Promise<OcLibrarySyncResult> {
  const kept: OcLibrarySyncResult = {
    folderId,
    detached: workspace.diPermissionDetached === true,
    canRewrite: false,
    failed: true,
  };
  try {
    const permissions = await diGetPermissions(folderId);
    const tokens = [
      ...(workspace.viewGroups ?? []),
      ...(workspace.editGroups ?? []),
      ...(workspace.adminGroups ?? []),
    ];
    const grants = buildLibraryGrants(
      workspace.viewGroups ?? [],
      workspace.editGroups ?? [],
      workspace.adminGroups ?? [],
      await knownGroups(tokens),
    );
    const desiredStamp = libraryPermissionStamp(grants);
    const actualStamp = folderPermissionStamp(permissions);
    const decision = decideLibrarySync({
      storedStamp: workspace.diPermissionStamp ?? null,
      detached: workspace.diPermissionDetached === true,
      actualStamp,
      desiredStamp,
      force,
    });

    if (decision.write) {
      const brokeNow = !permissions.inheritanceBroken && grants.length > 0;
      try {
        await writeDesired(folderId, permissions.inheritanceBroken, grants);
      } catch (error) {
        if (brokeNow) {
          try {
            await diRestoreInheritance(folderId);
          } catch {
            // The folder keeps the broken copy when restore is also refused.
          }
        }
        throw error;
      }
    }

    const stored = workspace.diPermissionStamp?.trim() || '';
    const nextStamp = decision.stamp?.trim() || '';
    if (stored !== nextStamp || (workspace.diPermissionDetached === true) !== decision.detached) {
      await ocUpdateWorkspace(workspace.__dataId, {
        diPermissionStamp: decision.stamp,
        diPermissionDetached: decision.detached,
      });
    }

    const visible = decision.write ? await diGetPermissions(folderId) : permissions;
    return {
      folderId,
      detached: decision.detached,
      canRewrite: decision.detached && canRewriteLibrary(workspace, visible),
      failed: false,
    };
  } catch {
    return kept;
  }
}

async function resolveOnce(workspaceId: string, force: boolean): Promise<OcLibrarySyncResult> {
  const workspace = await ocGetWorkspace(workspaceId);
  const legacyCode = workspace?.key?.trim() || '';
  const folderName = workspace?.name?.trim() || legacyCode;
  if (!workspace || !folderName) return emptyResult;

  const projects = await pmListProjects();
  const asWork = projects.filter((project) => project.workspaceId === workspaceId);
  if (asWork.length === 1) {
    return syncProjectHub(asWork[0], workspace, force);
  }

  const asMeeting = projects.filter((project) => project.meetingWorkspaceId === workspaceId);
  if (asWork.length === 0 && asMeeting.length === 1) {
    return openMeetingHub(asMeeting[0]);
  }

  const folderId = await ensureOwnFolder(workspaceId, folderName, legacyCode, workspace.diFolderId ?? null);
  if (!folderId) return emptyResult;
  return syncFolder(workspace, folderId, force);
}

async function syncProjectHub(
  project: PmProject,
  workspace: OpWorkspaceDetail,
  force: boolean,
): Promise<OcLibrarySyncResult> {
  const hubId = await ensureProjectDocumentHub(project.code, project.diFolderId);
  if (!hubId) return emptyResult;
  if ((project.diFolderId || '').trim() !== hubId) {
    await pmUpdateProject(project.id, { diFolderId: hubId });
  }
  return syncFolder(workspace, hubId, force);
}

async function openMeetingHub(project: PmProject): Promise<OcLibrarySyncResult> {
  const hubId = await ensureProjectDocumentHub(project.code, project.diFolderId);
  if (!hubId) return emptyResult;
  if ((project.diFolderId || '').trim() !== hubId) {
    await pmUpdateProject(project.id, { diFolderId: hubId });
  }
  let detached = false;
  const sourceId = project.workspaceId?.trim() || '';
  if (sourceId) {
    const source = await ocGetWorkspace(sourceId);
    detached = source?.diPermissionDetached === true;
  }
  return { folderId: hubId, detached, canRewrite: false, failed: false };
}

async function resolveLocked(workspaceId: string, force: boolean): Promise<OcLibrarySyncResult> {
  const id = workspaceId.trim();
  if (!id) return emptyResult;

  const pending = resolveLocks.get(id);
  if (pending && !force) return pending;

  const task = (async () => {
    if (pending && force) {
      try {
        await pending;
      } catch {
        // The forced write still runs after the in-flight resolve settles.
      }
    }
    return resolveOnce(id, force);
  })();

  resolveLocks.set(id, task);
  try {
    return await task;
  } finally {
    if (resolveLocks.get(id) === task) resolveLocks.delete(id);
  }
}

/** Resolved library folder. Project link wins. Missing Documents root returns null. */
export async function resolveOcWorkspaceLibrary(workspaceId: string): Promise<string | null> {
  const state = await resolveLocked(workspaceId, false);
  return state.folderId;
}

/** Library folder plus whether its permission has left the workspace copy. */
export async function resolveOcWorkspaceLibraryState(workspaceId: string): Promise<OcLibrarySyncResult> {
  return resolveLocked(workspaceId, false);
}

/** Writes the workspace lists onto the library folder and clears the detached mark. */
export async function rewriteOcWorkspaceLibraryPermissions(workspaceId: string): Promise<OcLibrarySyncResult> {
  return resolveLocked(workspaceId, true);
}
