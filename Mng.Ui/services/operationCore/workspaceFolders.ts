import { fetchFromOperations } from '@/services/apiService';
import type { OpWorkspaceFolder } from '@/types/apps/operationCore';

function asRecord(value: unknown): Record<string, unknown> | null {
  return value && typeof value === 'object' ? (value as Record<string, unknown>) : null;
}

function readGroups(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];
  return raw
    .map((item) => {
      if (typeof item === 'string') return item.trim();
      const row = asRecord(item);
      const id = row?.__dataId ?? row?.dataId;
      return id != null ? String(id).trim() : '';
    })
    .filter(Boolean);
}

function mapFolder(raw: Record<string, unknown>): OpWorkspaceFolder | null {
  const id = String(raw.id ?? raw.Id ?? raw.__dataId ?? '').trim();
  const name = String(raw.name ?? raw.Name ?? '').trim();
  if (!id || !name) return null;
  const parent = raw.parentFolderId ?? raw.ParentFolderId;
  const sortRaw = raw.sortOrder ?? raw.SortOrder;
  const sort = Number(sortRaw);
  return {
    __dataId: id,
    name,
    parentFolderId: parent != null && String(parent).trim() ? String(parent).trim() : null,
    viewGroups: readGroups(raw.viewGroups ?? raw.ViewGroups),
    sortOrder: Number.isFinite(sort) ? sort : 0,
  };
}

export async function ocListWorkspaceFolders(): Promise<OpWorkspaceFolder[]> {
  const raw = await fetchFromOperations('/api/v1/workspace-folders', 'GET');
  const body = asRecord(raw);
  const items = Array.isArray(body?.items) ? body.items : Array.isArray(raw) ? raw : [];
  return items
    .map((item) => mapFolder(asRecord(item) ?? {}))
    .filter((folder): folder is OpWorkspaceFolder => folder != null);
}

export async function ocCreateWorkspaceFolder(body: {
  name: string;
  parentFolderId?: string | null;
  viewGroups?: string[];
}): Promise<OpWorkspaceFolder> {
  const raw = await fetchFromOperations('/api/v1/workspace-folders', 'POST', {
    name: body.name,
    parentFolderId: body.parentFolderId || null,
    viewGroups: body.viewGroups ?? [],
  });
  const folder = mapFolder(asRecord(raw) ?? {});
  if (!folder) throw new Error('Klasör oluşturulamadı.');
  return folder;
}

export async function ocUpdateWorkspaceFolder(
  id: string,
  body: { name: string; viewGroups: string[] }
): Promise<OpWorkspaceFolder> {
  const raw = await fetchFromOperations(`/api/v1/workspace-folders/${encodeURIComponent(id)}`, 'PUT', body);
  const folder = mapFolder(asRecord(raw) ?? {});
  if (!folder) throw new Error('Klasör güncellenemedi.');
  return folder;
}

export async function ocMoveWorkspaceFolder(id: string, parentFolderId: string | null): Promise<void> {
  await fetchFromOperations(`/api/v1/workspace-folders/${encodeURIComponent(id)}/move`, 'POST', {
    parentFolderId,
  });
}

export async function ocDeleteWorkspaceFolder(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/workspace-folders/${encodeURIComponent(id)}`, 'DELETE');
}

export async function ocMoveWorkspaceToFolder(workspaceId: string, folderId: string | null): Promise<void> {
  await fetchFromOperations(
    `/api/v1/workspace-folders/workspaces/${encodeURIComponent(workspaceId)}/move`,
    'POST',
    { folderId }
  );
}
