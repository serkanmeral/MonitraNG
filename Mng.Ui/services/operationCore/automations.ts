import {
  OC_DATASETS,
  ocCreateRecordId,
  ocDelete,
  ocListDataset,
  ocUpdate,
  resolveRelationId,
} from '@/services/operationCoreService';
import { fetchFromOperations } from '@/services/apiService';
import type { OcAutomationSimulateResult, OpWorkspaceAutomation } from '@/types/apps/operationCore';

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

function mapAutomationSimulatePreview(raw: Record<string, unknown>) {
  const fieldsRaw = raw.resolvedFields ?? raw.ResolvedFields;
  const fields =
    fieldsRaw && typeof fieldsRaw === 'object' && !Array.isArray(fieldsRaw)
      ? (fieldsRaw as Record<string, unknown>)
      : {};
  return {
    resolvedTitle: raw.resolvedTitle != null ? String(raw.resolvedTitle) : raw.ResolvedTitle != null ? String(raw.ResolvedTitle) : null,
    resolvedDescription:
      raw.resolvedDescription != null
        ? String(raw.resolvedDescription)
        : raw.ResolvedDescription != null
          ? String(raw.ResolvedDescription)
          : null,
    targetBoardId: String(raw.targetBoardId ?? raw.TargetBoardId ?? '') || null,
    targetTypeId: String(raw.targetTypeId ?? raw.TargetTypeId ?? '') || null,
    resolvedAssignee:
      raw.resolvedAssignee != null
        ? String(raw.resolvedAssignee)
        : raw.ResolvedAssignee != null
          ? String(raw.ResolvedAssignee)
          : null,
    resolvedFields: fields,
  };
}

function mapAutomationSimulateResult(raw: Record<string, unknown>): OcAutomationSimulateResult {
  const previewRaw = raw.preview ?? raw.Preview;
  const createdRaw = raw.createdWorkItem ?? raw.CreatedWorkItem;
  const preview =
    previewRaw && typeof previewRaw === 'object'
      ? mapAutomationSimulatePreview(previewRaw as Record<string, unknown>)
      : null;
  const created =
    createdRaw && typeof createdRaw === 'object'
      ? {
          id: String((createdRaw as Record<string, unknown>).id ?? (createdRaw as Record<string, unknown>).Id ?? ''),
          key: String((createdRaw as Record<string, unknown>).key ?? (createdRaw as Record<string, unknown>).Key ?? ''),
          code:
            (createdRaw as Record<string, unknown>).code != null
              ? String((createdRaw as Record<string, unknown>).code)
              : (createdRaw as Record<string, unknown>).Code != null
                ? String((createdRaw as Record<string, unknown>).Code)
                : null,
        }
      : null;

  return {
    matched: Boolean(raw.matched ?? raw.Matched),
    reason: raw.reason != null ? String(raw.reason) : raw.Reason != null ? String(raw.Reason) : null,
    executed: Boolean(raw.executed ?? raw.Executed),
    preview,
    createdWorkItem: created?.id ? created : null,
  };
}

/** SW-A4: Otomasyonu kaynak iş kaydına karşı simüle eder (önizleme veya çalıştırma). */
export async function ocSimulateWorkspaceAutomation(
  automationId: string,
  workItemId: string,
  execute = false
): Promise<OcAutomationSimulateResult> {
  const raw = (await fetchFromOperations(
    `/api/v1/workspace-automations/${encodeURIComponent(automationId)}/simulate`,
    'POST',
    { workItemId, execute }
  )) as Record<string, unknown>;
  return mapAutomationSimulateResult(raw);
}
