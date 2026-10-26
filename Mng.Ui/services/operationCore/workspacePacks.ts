import { fetchFromOperations } from '@/services/apiService';

export interface OcWorkspacePackBoardName {
  key: string;
  name: string;
}

export interface OcWorkspacePackSummary {
  code: string;
  name: string;
  version: string;
  description: string | null;
  suggestedWorkspaceType: string | null;
  suggestedPrefix: string | null;
  boards: OcWorkspacePackBoardName[];
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return value && typeof value === 'object' ? (value as Record<string, unknown>) : null;
}

function text(row: Record<string, unknown>, ...keys: string[]): string | null {
  for (const key of keys) {
    const value = row[key];
    if (typeof value === 'string' && value.trim()) return value.trim();
  }
  return null;
}

function mapPack(raw: Record<string, unknown>): OcWorkspacePackSummary | null {
  const code = text(raw, 'code', 'Code');
  const name = text(raw, 'name', 'Name');
  if (!code || !name) return null;
  return {
    code,
    name,
    version: text(raw, 'version', 'Version') ?? '1.0.0',
    description: text(raw, 'description', 'Description'),
    suggestedWorkspaceType: text(raw, 'suggestedWorkspaceType', 'SuggestedWorkspaceType'),
    suggestedPrefix: text(raw, 'suggestedPrefix', 'SuggestedPrefix'),
    boards: mapBoards(raw.boards ?? raw.Boards),
  };
}

function mapBoards(raw: unknown): OcWorkspacePackBoardName[] {
  if (!Array.isArray(raw)) return [];
  return raw
    .map((item) => {
      const row = asRecord(item);
      if (!row) return null;
      const key = text(row, 'key', 'Key');
      const name = text(row, 'name', 'Name');
      if (!key || !name) return null;
      return { key, name };
    })
    .filter((board): board is OcWorkspacePackBoardName => board != null);
}

export async function ocListWorkspacePacks(): Promise<OcWorkspacePackSummary[]> {
  const raw = await fetchFromOperations('/api/v1/workspace-packs', 'GET');
  const body = asRecord(raw);
  const items = Array.isArray(body?.items) ? body.items : Array.isArray(body?.Items) ? body.Items : [];
  return items
    .map((item) => mapPack(asRecord(item) ?? {}))
    .filter((pack): pack is OcWorkspacePackSummary => pack != null);
}

export async function ocInstallWorkspacePack(
  workspaceId: string,
  code: string,
  boardNames?: Record<string, string>
): Promise<void> {
  const names: Record<string, string> = {};
  for (const [key, value] of Object.entries(boardNames ?? {})) {
    const trimmed = value.trim();
    if (key.trim() && trimmed) names[key.trim()] = trimmed;
  }
  await fetchFromOperations(`/api/v1/workspaces/${encodeURIComponent(workspaceId)}/pack`, 'POST', {
    code,
    upgrade: false,
    boardNames: names,
  });
}
