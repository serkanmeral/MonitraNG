import {
  OC_DATASETS,
  ocCreateRecordId,
  ocDelete,
  ocListDataset,
  ocUpdate,
  resolveRelationId,
} from '@/services/operationCoreService';
import type { OpSlaPolicy } from '@/types/apps/operationCore';

export function mapOpSlaPolicy(raw: Record<string, unknown>): OpSlaPolicy {
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
    typeId: resolveRelationId(raw.typeId ?? raw.TypeId) || null,
    priorityId: resolveRelationId(raw.priorityId ?? raw.PriorityId) || null,
    responseTargetMinutes:
      raw.responseTargetMinutes != null
        ? Number(raw.responseTargetMinutes)
        : raw.ResponseTargetMinutes != null
          ? Number(raw.ResponseTargetMinutes)
          : null,
    resolveTargetMinutes:
      raw.resolveTargetMinutes != null
        ? Number(raw.resolveTargetMinutes)
        : raw.ResolveTargetMinutes != null
          ? Number(raw.ResolveTargetMinutes)
          : null,
    isActive: raw.isActive !== false && raw.IsActive !== false,
    priority:
      raw.priority != null
        ? Number(raw.priority)
        : raw.Priority != null
          ? Number(raw.Priority)
          : 100,
  };
}

export async function ocListSlaPoliciesForWorkspace(workspaceId: string): Promise<OpSlaPolicy[]> {
  const rows = await ocListDataset(OC_DATASETS.slaPolicies, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'priority:desc,name:asc',
    limit: 200,
  });
  return rows
    .map((r) => mapOpSlaPolicy(r as Record<string, unknown>))
    .filter((p) => p.__dataId && p.name && p.workspaceId === workspaceId);
}

export async function ocCreateSlaPolicy(payload: Record<string, unknown>): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.slaPolicies, payload);
}

export async function ocUpdateSlaPolicy(policyId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.slaPolicies, policyId, payload);
}

export async function ocDeleteSlaPolicy(policyId: string) {
  await ocDelete(OC_DATASETS.slaPolicies, policyId);
}
