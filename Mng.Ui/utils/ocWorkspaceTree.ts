import type { OcNavNode, OpBoard, OpWorkspace, OpWorkspaceFolder } from '@/types/apps/operationCore';

function createsFolderCycle(folderId: string, parentId: string, folders: OpWorkspaceFolder[]): boolean {
  const byId = new Map(folders.map((folder) => [folder.__dataId, folder]));
  const seen = new Set<string>([folderId]);
  let current: string | null = parentId;
  while (current) {
    if (seen.has(current)) return true;
    seen.add(current);
    current = byId.get(current)?.parentFolderId ?? null;
  }
  return false;
}

function byName(left: string, right: string): number {
  return left.localeCompare(right, undefined, { sensitivity: 'base' });
}

function sortNav(nodes: OcNavNode[]) {
  nodes.sort((left, right) => {
    if (left.type !== right.type) return left.type === 'folder' ? -1 : 1;
    if (left.type === 'folder' && right.type === 'folder') {
      const order = left.folder.sortOrder - right.folder.sortOrder;
      if (order !== 0) return order;
      return byName(left.folder.name, right.folder.name);
    }
    if (left.type === 'workspace' && right.type === 'workspace') {
      return byName(left.data.name, right.data.name);
    }
    return 0;
  });
  for (const node of nodes) {
    if (node.type === 'folder') sortNav(node.children);
  }
}

export function buildOcNavTree(
  folders: OpWorkspaceFolder[],
  workspaces: OpWorkspace[],
  boardsFor: (workspaceId: string) => OpBoard[]
): OcNavNode[] {
  const folderNodes = new Map<string, Extract<OcNavNode, { type: 'folder' }>>();
  for (const folder of folders) {
    if (!folder.__dataId) continue;
    folderNodes.set(folder.__dataId, { type: 'folder', folder, children: [] });
  }

  const roots: OcNavNode[] = [];
  for (const node of folderNodes.values()) {
    const parentId = node.folder.parentFolderId;
    const parent = parentId && !createsFolderCycle(node.folder.__dataId, parentId, folders) ? folderNodes.get(parentId) : undefined;
    if (parent) parent.children.push(node);
    else roots.push(node);
  }

  for (const workspace of workspaces) {
    if (!workspace.__dataId) continue;
    const node: OcNavNode = {
      type: 'workspace',
      data: workspace,
      children: boardsFor(workspace.__dataId).map((board) => ({ type: 'board' as const, data: board })),
    };
    const parent = workspace.folderId ? folderNodes.get(workspace.folderId) : undefined;
    if (parent) parent.children.push(node);
    else roots.push(node);
  }

  sortNav(roots);
  return roots;
}

export function collectFolderDescendantIds(folders: OpWorkspaceFolder[], folderId: string): Set<string> {
  const blocked = new Set<string>([folderId]);
  let grew = true;
  while (grew) {
    grew = false;
    for (const folder of folders) {
      if (folder.parentFolderId && blocked.has(folder.parentFolderId) && !blocked.has(folder.__dataId)) {
        blocked.add(folder.__dataId);
        grew = true;
      }
    }
  }
  return blocked;
}

export function folderAncestorIds(folders: OpWorkspaceFolder[], folderId: string | null | undefined): string[] {
  if (!folderId) return [];
  const byId = new Map(folders.map((folder) => [folder.__dataId, folder]));
  const ids: string[] = [];
  const seen = new Set<string>();
  let current = folderId;
  while (current && !seen.has(current)) {
    seen.add(current);
    ids.push(current);
    current = byId.get(current)?.parentFolderId ?? '';
  }
  return ids;
}
