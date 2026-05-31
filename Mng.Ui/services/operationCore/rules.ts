import {
  OC_DATASETS,
  ocCreateRecordId,
  ocDelete,
  ocListDataset,
  ocUpdate,
  resolveRelationId,
} from '@/services/operationCoreService';
import type { OpRule } from '@/types/apps/operationCore';

export function mapOpRule(raw: Record<string, unknown>): OpRule {
  const conditions = raw.conditions ?? raw.Conditions;
  const actions = raw.actions ?? raw.Actions;
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? ''),
    description:
      raw.description != null
        ? String(raw.description)
        : raw.Description != null
          ? String(raw.Description)
          : null,
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId) ?? '',
    ruleType: String(raw.ruleType ?? raw.RuleType ?? '').toLowerCase(),
    trigger: String(raw.trigger ?? raw.Trigger ?? ''),
    transitionKey:
      raw.transitionKey != null
        ? String(raw.transitionKey)
        : raw.TransitionKey != null
          ? String(raw.TransitionKey)
          : null,
    typeId: resolveRelationId(raw.typeId ?? raw.TypeId) || null,
    boardId: resolveRelationId(raw.boardId ?? raw.BoardId) || null,
    stateId: resolveRelationId(raw.stateId ?? raw.StateId) || null,
    fromStateId: resolveRelationId(raw.fromStateId ?? raw.FromStateId) || null,
    toStateId: resolveRelationId(raw.toStateId ?? raw.ToStateId) || null,
    isActive: raw.isActive !== false && raw.IsActive !== false,
    priority:
      raw.priority != null && raw.priority !== ''
        ? Number(raw.priority)
        : raw.Priority != null && raw.Priority !== ''
          ? Number(raw.Priority)
          : null,
    conditions,
    actions: Array.isArray(actions) ? actions : [],
    errorMessage:
      raw.errorMessage != null
        ? String(raw.errorMessage)
        : raw.ErrorMessage != null
          ? String(raw.ErrorMessage)
          : null,
    applyMode:
      raw.applyMode != null
        ? String(raw.applyMode)
        : raw.ApplyMode != null
          ? String(raw.ApplyMode)
          : null,
  };
}

export async function ocListRulesForWorkspace(workspaceId: string): Promise<OpRule[]> {
  const rows = await ocListDataset(OC_DATASETS.rules, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'priority:asc,name:asc',
    limit: 200,
  });
  return rows
    .map((r) => mapOpRule(r as Record<string, unknown>))
    .filter((rule) => rule.__dataId && rule.name && rule.workspaceId === workspaceId);
}

export async function ocCreateRule(payload: Record<string, unknown>): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.rules, payload);
}

export async function ocUpdateRule(ruleId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.rules, ruleId, payload);
}

export async function ocDeleteRule(ruleId: string) {
  await ocDelete(OC_DATASETS.rules, ruleId);
}
