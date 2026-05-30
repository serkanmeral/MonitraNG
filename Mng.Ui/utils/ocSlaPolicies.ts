import type { OpSlaPolicy } from '@/types/apps/operationCore';

export interface OcSlaPolicyDraft {
  id?: string;
  name: string;
  description?: string;
  typeId?: string | null;
  priorityId?: string | null;
  responseTargetMinutes: number | null;
  resolveTargetMinutes: number | null;
  policyPriority: number;
  isActive: boolean;
}

export function newSlaPolicyDraft(seed?: Partial<OcSlaPolicyDraft>): OcSlaPolicyDraft {
  return {
    name: '',
    description: '',
    typeId: null,
    priorityId: null,
    responseTargetMinutes: 60,
    resolveTargetMinutes: 480,
    policyPriority: 100,
    isActive: true,
    ...seed,
  };
}

export function parseOpSlaPolicyToDraft(policy: OpSlaPolicy): OcSlaPolicyDraft {
  return {
    id: policy.__dataId,
    name: policy.name,
    description: policy.description ?? '',
    typeId: policy.typeId ?? null,
    priorityId: policy.priorityId ?? null,
    responseTargetMinutes: policy.responseTargetMinutes ?? null,
    resolveTargetMinutes: policy.resolveTargetMinutes ?? null,
    policyPriority: policy.priority ?? 100,
    isActive: policy.isActive !== false,
  };
}

export function validateSlaPolicyDraft(draft: OcSlaPolicyDraft): string | null {
  if (!draft.name.trim()) return 'name';
  const hasResponse = draft.responseTargetMinutes != null && draft.responseTargetMinutes > 0;
  const hasResolve = draft.resolveTargetMinutes != null && draft.resolveTargetMinutes > 0;
  if (!hasResponse && !hasResolve) return 'targets';
  return null;
}

export function buildSlaPolicyPayload(draft: OcSlaPolicyDraft, workspaceId: string): Record<string, unknown> {
  const body: Record<string, unknown> = {
    name: draft.name.trim(),
    workspaceId,
    isActive: draft.isActive,
    priority: draft.policyPriority,
  };
  if (draft.description?.trim()) body.description = draft.description.trim();
  if (draft.typeId) body.typeId = draft.typeId;
  if (draft.priorityId) body.priorityId = draft.priorityId;
  if (draft.responseTargetMinutes != null && draft.responseTargetMinutes > 0) {
    body.responseTargetMinutes = draft.responseTargetMinutes;
  }
  if (draft.resolveTargetMinutes != null && draft.resolveTargetMinutes > 0) {
    body.resolveTargetMinutes = draft.resolveTargetMinutes;
  }
  return body;
}

export function formatSlaMinutes(minutes: number | null | undefined): string {
  if (minutes == null || minutes <= 0) return '—';
  if (minutes < 60) return `${minutes} dk`;
  if (minutes % 60 === 0) return `${minutes / 60} sa`;
  const h = Math.floor(minutes / 60);
  const m = Math.round(minutes % 60);
  return `${h} sa ${m} dk`;
}

export function formatSlaScopeSummary(
  policy: Pick<OpSlaPolicy, 'typeId' | 'priorityId'>,
  typeNameById: Map<string, string>,
  priorityNameById: Map<string, string>,
  anyLabel: string
): string {
  const parts: string[] = [];
  if (policy.typeId) {
    parts.push(typeNameById.get(policy.typeId) ?? policy.typeId);
  }
  if (policy.priorityId) {
    parts.push(priorityNameById.get(policy.priorityId) ?? policy.priorityId);
  }
  return parts.length ? parts.join(' · ') : anyLabel;
}

export function formatSlaTargetsSummary(
  policy: Pick<OpSlaPolicy, 'responseTargetMinutes' | 'resolveTargetMinutes'>,
  responseLabel: string,
  resolveLabel: string
): string {
  const parts: string[] = [];
  if (policy.responseTargetMinutes != null && policy.responseTargetMinutes > 0) {
    parts.push(`${responseLabel}: ${formatSlaMinutes(policy.responseTargetMinutes)}`);
  }
  if (policy.resolveTargetMinutes != null && policy.resolveTargetMinutes > 0) {
    parts.push(`${resolveLabel}: ${formatSlaMinutes(policy.resolveTargetMinutes)}`);
  }
  return parts.length ? parts.join(' · ') : '—';
}

export function slaPolicySpecificityScore(policy: Pick<OpSlaPolicy, 'typeId' | 'priorityId'>): number {
  let score = 0;
  if (policy.typeId) score += 2;
  if (policy.priorityId) score += 1;
  return score;
}
