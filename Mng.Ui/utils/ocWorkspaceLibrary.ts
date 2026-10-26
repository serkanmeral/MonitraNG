import { diBreakInheritance, diGetById, diSetPermissions } from '@/services/documentIntelligenceService';
import { ocGetWorkspace, ocUpdateWorkspace } from '@/services/operationCoreService';
import { pmListProjects, pmUpdateProject } from '@/services/projectManagementService';
import { useGroupStore } from '@/stores/apps/group';
import type { PmProject } from '@/types/apps/projectManagement';
import {
  DOCS_FOLDER_NAMES,
  ensureFolder,
  ensureProjectDocumentHub,
  findChildFolder,
} from '@/utils/pmJobPack';

export const OC_LIBRARY_FOLDER = 'Operasyon';

const VIEW_ACTIONS = ['view', 'download'];
const EDIT_ACTIONS = ['view', 'create', 'edit', 'delete', 'upload', 'download', 'move'];

const resolveLocks = new Map<string, Promise<string | null>>();

function linkedProject(projects: PmProject[], workspaceId: string): PmProject | null {
  const asWorkspace = projects.filter((project) => project.workspaceId === workspaceId);
  if (asWorkspace.length === 1) return asWorkspace[0];
  if (asWorkspace.length > 1) return null;
  const asMeeting = projects.filter((project) => project.meetingWorkspaceId === workspaceId);
  return asMeeting.length === 1 ? asMeeting[0] : null;
}

async function writeFolderPermissions(folderId: string, viewGroups: string[], editGroups: string[]) {
  const ids = [...new Set([...viewGroups, ...editGroups].map((id) => id.trim()).filter(Boolean))];
  if (!ids.length) return;

  const groups = await useGroupStore().fetchGroupsByIds(ids);
  const byId = new Map(groups.map((group) => [group.groupId || group.id, group.name.trim()]));
  const actions = new Map<string, { groupId: string; groupName: string; permissions: Set<string> }>();

  function grant(groupId: string, actionList: string[]) {
    const groupName = byId.get(groupId);
    if (!groupName) return;
    const row = actions.get(groupId) ?? { groupId, groupName, permissions: new Set<string>() };
    for (const action of actionList) row.permissions.add(action);
    actions.set(groupId, row);
  }

  for (const groupId of viewGroups) grant(groupId, VIEW_ACTIONS);
  for (const groupId of editGroups) grant(groupId, EDIT_ACTIONS);
  if (!actions.size) return;

  await diBreakInheritance(folderId);
  await diSetPermissions(folderId, {
    groups: [...actions.values()].map((row) => ({
      groupId: row.groupId,
      groupName: row.groupName,
      permissions: [...row.permissions],
    })),
  });
}

async function ensureOwnFolder(workspaceId: string, code: string, existingId: string | null, viewGroups: string[], editGroups: string[]) {
  const stored = existingId?.trim();
  if (stored) {
    try {
      const resource = await diGetById(stored);
      if (resource?.id && resource.type === 'folder') return resource.id;
    } catch {
      // Stale id. Look up or create the code folder again.
    }
  }

  const docs = await findChildFolder(null, DOCS_FOLDER_NAMES);
  if (!docs?.id) return null;
  const operationId = await ensureFolder(docs.id, OC_LIBRARY_FOLDER);
  const found = await findChildFolder(operationId, [code]);
  const created = !found?.id;
  const folderId = found?.id || (await ensureFolder(operationId, code));
  if (created) {
    try {
      await writeFolderPermissions(folderId, viewGroups, editGroups);
    } catch {
      // The folder is the library even when the permission copy fails.
    }
  }
  if (folderId !== stored) {
    await ocUpdateWorkspace(workspaceId, { diFolderId: folderId });
  }
  return folderId;
}

async function resolveOnce(workspaceId: string): Promise<string | null> {
  const workspace = await ocGetWorkspace(workspaceId);
  const code = workspace?.key?.trim();
  if (!workspace || !code) return null;

  const projects = await pmListProjects();
  const project = linkedProject(projects, workspaceId);
  if (project?.code) {
    const hubId = await ensureProjectDocumentHub(project.code, project.diFolderId);
    if (!hubId) return null;
    if ((project.diFolderId || '').trim() !== hubId) {
      await pmUpdateProject(project.id, { diFolderId: hubId });
    }
    return hubId;
  }

  return ensureOwnFolder(
    workspaceId,
    code,
    workspace.diFolderId,
    workspace.viewGroups ?? [],
    workspace.editGroups ?? [],
  );
}

/** Resolved library folder. Project link wins. Missing Documents root returns null. */
export async function resolveOcWorkspaceLibrary(workspaceId: string): Promise<string | null> {
  const id = workspaceId.trim();
  if (!id) return null;
  const pending = resolveLocks.get(id);
  if (pending) return pending;

  let task: Promise<string | null>;
  task = resolveOnce(id).finally(() => {
    if (resolveLocks.get(id) === task) resolveLocks.delete(id);
  });
  resolveLocks.set(id, task);
  return task;
}
