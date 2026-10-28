import { fetchFromOperations } from '@/services/apiService';

export interface OcSprint {
  id: string;
  workspaceId: string;
  name: string;
  goal: string | null;
  startDate: string;
  endDate: string;
  state: 'planned' | 'active' | 'closed';
  itemCount: number;
}

export interface OcSprintItem {
  id: string;
  key: string;
  title: string;
  stateId: string | null;
}

export interface OcSprintForm {
  name: string;
  goal: string;
  startDate: string;
  endDate: string;
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return value && typeof value === 'object' ? (value as Record<string, unknown>) : null;
}

function text(raw: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const value = raw[key];
    if (value != null && String(value).trim()) return String(value).trim();
  }
  return '';
}

function mapSprint(raw: unknown): OcSprint | null {
  const row = asRecord(raw);
  if (!row) return null;
  const id = text(row, 'id', 'Id');
  const name = text(row, 'name', 'Name');
  if (!id || !name) return null;
  const state = text(row, 'state', 'State');
  const count = Number(row.itemCount ?? row.ItemCount ?? 0);
  return {
    id,
    workspaceId: text(row, 'workspaceId', 'WorkspaceId'),
    name,
    goal: text(row, 'goal', 'Goal') || null,
    startDate: text(row, 'startDate', 'StartDate'),
    endDate: text(row, 'endDate', 'EndDate'),
    state: state === 'active' || state === 'closed' ? state : 'planned',
    itemCount: Number.isFinite(count) ? count : 0,
  };
}

function mapItem(raw: unknown): OcSprintItem | null {
  const row = asRecord(raw);
  if (!row) return null;
  const id = text(row, 'id', 'Id');
  if (!id) return null;
  return {
    id,
    key: text(row, 'key', 'Key'),
    title: text(row, 'title', 'Title'),
    stateId: text(row, 'stateId', 'StateId') || null,
  };
}

function itemsOf(raw: unknown): unknown[] {
  const body = asRecord(raw);
  if (Array.isArray(body?.items)) return body.items;
  return [];
}

export async function ocListSprints(workspaceId: string): Promise<OcSprint[]> {
  const raw = await fetchFromOperations(`/api/v1/workspaces/${encodeURIComponent(workspaceId)}/sprints`, 'GET');
  return itemsOf(raw).map(mapSprint).filter((row): row is OcSprint => !!row);
}

export async function ocCreateSprint(workspaceId: string, form: OcSprintForm): Promise<OcSprint> {
  const raw = await fetchFromOperations(`/api/v1/workspaces/${encodeURIComponent(workspaceId)}/sprints`, 'POST', {
    name: form.name.trim(),
    goal: form.goal.trim() || null,
    startDate: form.startDate,
    endDate: form.endDate,
  });
  const mapped = mapSprint(raw);
  if (!mapped) throw new Error('Sprint oluşturulamadı.');
  return mapped;
}

export async function ocActivateSprint(workspaceId: string, sprintId: string): Promise<OcSprint> {
  const raw = await fetchFromOperations(
    `/api/v1/workspaces/${encodeURIComponent(workspaceId)}/sprints/${encodeURIComponent(sprintId)}/activate`,
    'POST',
    {}
  );
  const mapped = mapSprint(raw);
  if (!mapped) throw new Error('Sprint aktif edilemedi.');
  return mapped;
}

export async function ocCloseSprint(workspaceId: string, sprintId: string): Promise<OcSprint> {
  const raw = await fetchFromOperations(
    `/api/v1/workspaces/${encodeURIComponent(workspaceId)}/sprints/${encodeURIComponent(sprintId)}/close`,
    'POST',
    {}
  );
  const mapped = mapSprint(raw);
  if (!mapped) throw new Error('Sprint kapatılamadı.');
  return mapped;
}

export async function ocDeleteSprint(workspaceId: string, sprintId: string): Promise<void> {
  await fetchFromOperations(
    `/api/v1/workspaces/${encodeURIComponent(workspaceId)}/sprints/${encodeURIComponent(sprintId)}`,
    'DELETE'
  );
}

export async function ocListSprintItems(workspaceId: string, sprintId: string): Promise<OcSprintItem[]> {
  const raw = await fetchFromOperations(
    `/api/v1/workspaces/${encodeURIComponent(workspaceId)}/sprints/${encodeURIComponent(sprintId)}/items`,
    'GET'
  );
  return itemsOf(raw).map(mapItem).filter((row): row is OcSprintItem => !!row);
}

export async function ocSetSprintMembership(workspaceId: string, workItemId: string, sprintId: string | null): Promise<void> {
  await fetchFromOperations(`/api/v1/workspaces/${encodeURIComponent(workspaceId)}/sprints/membership`, 'POST', {
    workItemId,
    sprintId,
  });
}
