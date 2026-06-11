import {
  OC_DATASETS,
  ocCreateRecordId,
  ocDelete,
  ocListDataset,
  ocUpdate,
  resolveRelationId,
} from '@/services/operationCoreService';
import type { OpWorkspaceAutomation } from '@/types/apps/operationCore';

function parseJsonObject(raw: unknown): Record<string, unknown> | null {
  if (raw && typeof raw === 'object' && !Array.isArray(raw)) {
    return raw as Record<string, unknown>;
  }
  if (typeof raw === 'string' && raw.trim()) {
    try {
      const parsed = JSON.parse(raw) as unknown;
      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
        return parsed as Record<string, unknown>;
      }
    } catch {
      return null;
    }
  }
  return null;
}

function parseJsonArray(raw: unknown): unknown[] {
  if (Array.isArray(raw)) return raw;
  if (typeof raw === 'string' && raw.trim()) {
    try {
      const parsed = JSON.parse(raw) as unknown;
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }
  return [];
}

export function mapOpWorkspaceAutomation(raw: Record<string, unknown>): OpWorkspaceAutomation {
  const trigger = parseJsonObject(raw.trigger ?? raw.Trigger) ?? { kind: 'workItemStateReached' };
  const idempotency = parseJsonObject(raw.idempotency ?? raw.Idempotency) ?? { mode: 'none' };
  const relation = parseJsonObject(raw.relation ?? raw.Relation) ?? { mode: 'parent' };
  const actions = parseJsonArray(raw.actions ?? raw.Actions);

  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId) ?? '',
    name: String(raw.name ?? ''),
    description:
      raw.description != null
        ? String(raw.description)
        : raw.Description != null
          ? String(raw.Description)
          : null,
    isActive: raw.isActive !== false && raw.IsActive !== false,
    trigger: trigger as OpWorkspaceAutomation['trigger'],
    idempotency: {
      mode: (idempotency.mode === 'one_per_source' ? 'one_per_source' : 'none') as
        | 'none'
        | 'one_per_source',
    },
    relation: {
      mode: (relation.mode === 'none' ? 'none' : 'parent') as 'parent' | 'none',
    },
    actions: actions as OpWorkspaceAutomation['actions'],
    lastRunAt:
      raw.lastRunAt != null
        ? String(raw.lastRunAt)
        : raw.LastRunAt != null
          ? String(raw.LastRunAt)
          : null,
    lastCreatedWorkItemId:
      resolveRelationId(raw.lastCreatedWorkItemId ?? raw.LastCreatedWorkItemId) || null,
    runCount:
      typeof raw.runCount === 'number'
        ? raw.runCount
        : typeof raw.RunCount === 'number'
          ? raw.RunCount
          : null,
  };
}

export async function ocListAutomationsForWorkspace(
  workspaceId: string
): Promise<OpWorkspaceAutomation[]> {
  const rows = await ocListDataset(OC_DATASETS.workspaceAutomations, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'name:asc',
    limit: 200,
  });
  return rows
    .map((r) => mapOpWorkspaceAutomation(r as Record<string, unknown>))
    .filter((a) => a.__dataId && a.name && a.workspaceId === workspaceId);
}

export async function ocCreateWorkspaceAutomation(
  payload: Record<string, unknown>
): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.workspaceAutomations, payload);
}

export async function ocUpdateWorkspaceAutomation(
  automationId: string,
  payload: Record<string, unknown>
) {
  await ocUpdate(OC_DATASETS.workspaceAutomations, automationId, payload);
}

export async function ocDeleteWorkspaceAutomation(automationId: string) {
  await ocDelete(OC_DATASETS.workspaceAutomations, automationId);
}
