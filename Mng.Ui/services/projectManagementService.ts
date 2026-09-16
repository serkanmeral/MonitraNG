import { fetchFromOperations } from '@/services/apiService';
import type {
  PmCreateDependencyRequest,
  PmCreateProjectRequest,
  PmCreateWbsRequest,
  PmDependency,
  PmProject,
  PmPortfolio,
  PmProjectDetail,
  PmUpdateProjectRequest,
  PmUpdateWbsRequest,
  PmWbsItem,
  PmWorkItemCandidate,
  PmWorkItemCandidatePage,
  PmProjectStatusPack,
  PmTraceDocument,
  PmProjectDocument,
  PmCreateDecisionRequest,
  PmUpdateDecisionRequest,
  PmDecision,
  PmJobPack,
  PmProjectPackCatalog,
  PmApplyPackResult,
  PmPackPreview,
  PmStageGate,
  PmCreateStageGateRequest,
  PmUpdateStageGateRequest,
  PmRaidItem,
  PmCreateRaidItemRequest,
  PmUpdateRaidItemRequest,
  PmResourceAssignment,
  PmCreateResourceAssignmentRequest,
  PmUpdateResourceAssignmentRequest,
  PmProjectCapacity,
  PmBudgetLine,
  PmCreateBudgetLineRequest,
  PmUpdateBudgetLineRequest,
  PmProjectBudget,
  PmAcknowledgement,
  PmCreateAcknowledgementRequest,
  PmUpdateAcknowledgementRequest,
  PmProjectAcknowledgements,
  PmObligation,
  PmCreateObligationRequest,
  PmUpdateObligationRequest,
  PmProjectObligations,
  PmAuditPack,
  PmCreateAuditPackRequest,
  PmUpdateAuditPackRequest,
  PmProjectAuditPacks,
  PmMeeting,
  PmMeetingAction,
  PmCreateMeetingRequest,
  PmUpdateMeetingRequest,
  PmCreateMeetingActionRequest,
  PmUpdateMeetingActionRequest,
  PmProjectMeetings,
  PmStakeholder,
  PmCreateStakeholderRequest,
  PmUpdateStakeholderRequest,
  PmProjectStakeholders,
  PmProcessMap,
  PmCreateProcessMapRequest,
  PmUpdateProcessMapRequest,
  PmProjectProcessMaps,
} from '@/types/apps/projectManagement';

function asArray<T>(raw: unknown): T[] {
  return Array.isArray(raw) ? (raw as T[]) : [];
}

export async function pmListProjects(): Promise<PmProject[]> {
  const raw = await fetchFromOperations('/api/v1/projects', 'GET');
  return asArray<PmProject>(raw);
}

export async function pmGetPortfolio(): Promise<PmPortfolio> {
  return (await fetchFromOperations('/api/v1/projects/portfolio', 'GET')) as PmPortfolio;
}

export async function pmListJobPacks(): Promise<PmJobPack[]> {
  const raw = await fetchFromOperations('/api/v1/job-packs', 'GET');
  return asArray<PmJobPack>(raw);
}

export async function pmGetProjectPacks(projectId: string): Promise<PmProjectPackCatalog> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/packs`,
    'GET',
  )) as PmProjectPackCatalog;
}

export async function pmPreviewProjectPack(
  projectId: string,
  packCode: string,
  intent: 'apply' | 'detach' = 'apply',
  mode: 'skip' | 'update' = 'skip',
): Promise<PmPackPreview> {
  const qs = new URLSearchParams({ intent, mode });
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/packs/${encodeURIComponent(packCode)}/preview?${qs.toString()}`,
    'GET',
  )) as PmPackPreview;
}

export async function pmApplyProjectPack(
  projectId: string,
  packCode: string,
  mode: 'skip' | 'update' = 'skip',
): Promise<PmApplyPackResult> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/packs/${encodeURIComponent(packCode)}?mode=${encodeURIComponent(mode)}`,
    'POST',
  )) as PmApplyPackResult;
}

export async function pmDetachProjectPack(projectId: string, packCode: string): Promise<PmApplyPackResult> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/packs/${encodeURIComponent(packCode)}`,
    'DELETE',
  )) as PmApplyPackResult;
}

export async function pmGetProject(id: string): Promise<PmProjectDetail> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(id)}`,
    'GET',
  )) as PmProjectDetail;
}

export async function pmListProjectDecisions(projectId: string): Promise<PmDecision[]> {
  const raw = await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/decisions`,
    'GET',
  );
  return asArray<PmDecision>(raw);
}

export async function pmListProjectRaid(projectId: string): Promise<PmRaidItem[]> {
  const raw = await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/raid`,
    'GET',
  );
  return asArray<PmRaidItem>(raw);
}

export async function pmCreateProject(body: PmCreateProjectRequest): Promise<PmProject> {
  return (await fetchFromOperations('/api/v1/projects', 'POST', body)) as PmProject;
}

export async function pmUpdateProject(id: string, body: PmUpdateProjectRequest): Promise<PmProject> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmProject;
}

export async function pmDeleteProject(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/projects/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmSetBaseline(id: string, note?: string | null): Promise<PmProjectDetail> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(id)}/baseline`,
    'POST',
    { note: note || null },
  )) as PmProjectDetail;
}

export async function pmCreateWbs(projectId: string, body: PmCreateWbsRequest): Promise<PmWbsItem> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/wbs`,
    'POST',
    body,
  )) as PmWbsItem;
}

export async function pmUpdateWbs(id: string, body: PmUpdateWbsRequest): Promise<PmWbsItem> {
  return (await fetchFromOperations(
    `/api/v1/wbs/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmWbsItem;
}

export async function pmDeleteWbs(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/wbs/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmCreateDependency(
  projectId: string,
  body: PmCreateDependencyRequest,
): Promise<PmDependency> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/dependencies`,
    'POST',
    body,
  )) as PmDependency;
}

export async function pmDeleteDependency(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/dependencies/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmBindWbsWorkItem(wbsId: string, workItemId: string): Promise<PmWbsItem> {
  return (await fetchFromOperations(
    `/api/v1/wbs/${encodeURIComponent(wbsId)}/work-item`,
    'POST',
    { workItemId },
  )) as PmWbsItem;
}

export async function pmUnbindWbsWorkItem(wbsId: string): Promise<PmWbsItem> {
  return (await fetchFromOperations(
    `/api/v1/wbs/${encodeURIComponent(wbsId)}/work-item`,
    'DELETE',
  )) as PmWbsItem;
}

export async function pmListProjectDocuments(
  projectId: string,
  query?: string | null,
): Promise<PmProjectDocument[]> {
  const q = query?.trim();
  const suffix = q ? `?q=${encodeURIComponent(q)}` : '';
  const raw = await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/documents${suffix}`,
    'GET',
  );
  return asArray<PmProjectDocument>(raw);
}

export async function pmListWbsEvidence(wbsId: string): Promise<PmTraceDocument[]> {
  const raw = await fetchFromOperations(
    `/api/v1/wbs/${encodeURIComponent(wbsId)}/evidence`,
    'GET',
  );
  return asArray<PmTraceDocument>(raw);
}

export async function pmBindWbsEvidence(
  wbsId: string,
  resourceId: string,
  relationType?: string | null,
): Promise<PmWbsItem> {
  return (await fetchFromOperations(
    `/api/v1/wbs/${encodeURIComponent(wbsId)}/evidence`,
    'POST',
    { resourceId, relationType: relationType || undefined },
  )) as PmWbsItem;
}

export async function pmUnbindWbsEvidence(wbsId: string, resourceId: string): Promise<PmWbsItem> {
  return (await fetchFromOperations(
    `/api/v1/wbs/${encodeURIComponent(wbsId)}/evidence/${encodeURIComponent(resourceId)}`,
    'DELETE',
  )) as PmWbsItem;
}

export async function pmListWbsReferences(wbsId: string): Promise<PmTraceDocument[]> {
  const raw = await fetchFromOperations(
    `/api/v1/wbs/${encodeURIComponent(wbsId)}/references`,
    'GET',
  );
  return asArray<PmTraceDocument>(raw);
}

export async function pmBindWbsReference(wbsId: string, resourceId: string): Promise<PmWbsItem> {
  return (await fetchFromOperations(
    `/api/v1/wbs/${encodeURIComponent(wbsId)}/references`,
    'POST',
    { resourceId },
  )) as PmWbsItem;
}

export async function pmUnbindWbsReference(wbsId: string, resourceId: string): Promise<PmWbsItem> {
  return (await fetchFromOperations(
    `/api/v1/wbs/${encodeURIComponent(wbsId)}/references/${encodeURIComponent(resourceId)}`,
    'DELETE',
  )) as PmWbsItem;
}

export async function pmSearchProjectWorkItems(
  projectId: string,
  query?: string | null,
  page?: { skip?: number; take?: number },
): Promise<PmWorkItemCandidatePage> {
  const params = new URLSearchParams();
  const q = query?.trim();
  if (q) params.set('q', q);
  if (typeof page?.skip === 'number' && page.skip >= 0) params.set('skip', String(page.skip));
  if (typeof page?.take === 'number' && page.take > 0) params.set('take', String(page.take));
  const suffix = params.size ? `?${params.toString()}` : '';
  const raw = await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/work-items${suffix}`,
    'GET',
  );
  if (Array.isArray(raw)) {
    const items = raw as PmWorkItemCandidate[];
    return { items, total: items.length, skip: page?.skip ?? 0, take: page?.take ?? items.length };
  }
  const obj = (raw ?? {}) as Partial<PmWorkItemCandidatePage> & { Items?: PmWorkItemCandidate[]; Total?: number };
  const items = obj.items ?? obj.Items ?? [];
  return {
    items,
    total: obj.total ?? obj.Total ?? items.length,
    skip: obj.skip ?? page?.skip ?? 0,
    take: obj.take ?? page?.take ?? items.length,
  };
}

export async function pmRecalcProgress(projectId: string): Promise<PmProjectDetail> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/rollup`,
    'POST',
  )) as PmProjectDetail;
}

export async function pmGetProjectStatus(projectId: string): Promise<PmProjectStatusPack> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/status`,
    'GET',
  )) as PmProjectStatusPack;
}

export async function pmCreateDecision(
  projectId: string,
  body: PmCreateDecisionRequest,
): Promise<PmDecision> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/decisions`,
    'POST',
    body,
  )) as PmDecision;
}

export async function pmUpdateDecision(id: string, body: PmUpdateDecisionRequest): Promise<PmDecision> {
  return (await fetchFromOperations(
    `/api/v1/decisions/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmDecision;
}

export async function pmDeleteDecision(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/decisions/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmCreateStageGate(
  projectId: string,
  body: PmCreateStageGateRequest,
): Promise<PmStageGate> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/stage-gates`,
    'POST',
    body,
  )) as PmStageGate;
}

export async function pmUpdateStageGate(id: string, body: PmUpdateStageGateRequest): Promise<PmStageGate> {
  return (await fetchFromOperations(
    `/api/v1/stage-gates/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmStageGate;
}

export async function pmDeleteStageGate(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/stage-gates/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmCreateRaidItem(
  projectId: string,
  body: PmCreateRaidItemRequest,
): Promise<PmRaidItem> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/raid`,
    'POST',
    body,
  )) as PmRaidItem;
}

export async function pmUpdateRaidItem(id: string, body: PmUpdateRaidItemRequest): Promise<PmRaidItem> {
  return (await fetchFromOperations(
    `/api/v1/raid/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmRaidItem;
}

export async function pmDeleteRaidItem(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/raid/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmGetProjectCapacity(projectId: string): Promise<PmProjectCapacity> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/capacity`,
    'GET',
  )) as PmProjectCapacity;
}

export async function pmCreateAssignment(
  projectId: string,
  body: PmCreateResourceAssignmentRequest,
): Promise<PmResourceAssignment> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/assignments`,
    'POST',
    body,
  )) as PmResourceAssignment;
}

export async function pmUpdateAssignment(
  id: string,
  body: PmUpdateResourceAssignmentRequest,
): Promise<PmResourceAssignment> {
  return (await fetchFromOperations(
    `/api/v1/assignments/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmResourceAssignment;
}

export async function pmDeleteAssignment(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/assignments/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmGetProjectBudget(projectId: string): Promise<PmProjectBudget> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/budget`,
    'GET',
  )) as PmProjectBudget;
}

export async function pmCreateBudgetLine(
  projectId: string,
  body: PmCreateBudgetLineRequest,
): Promise<PmBudgetLine> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/budget`,
    'POST',
    body,
  )) as PmBudgetLine;
}

export async function pmUpdateBudgetLine(id: string, body: PmUpdateBudgetLineRequest): Promise<PmBudgetLine> {
  return (await fetchFromOperations(
    `/api/v1/budget/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmBudgetLine;
}

export async function pmDeleteBudgetLine(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/budget/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmGetProjectAcks(projectId: string): Promise<PmProjectAcknowledgements> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/acks`,
    'GET',
  )) as PmProjectAcknowledgements;
}

export async function pmCreateAck(
  projectId: string,
  body: PmCreateAcknowledgementRequest,
): Promise<PmAcknowledgement> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/acks`,
    'POST',
    body,
  )) as PmAcknowledgement;
}

export async function pmUpdateAck(id: string, body: PmUpdateAcknowledgementRequest): Promise<PmAcknowledgement> {
  return (await fetchFromOperations(
    `/api/v1/acks/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmAcknowledgement;
}

export async function pmDeleteAck(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/acks/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmGetProjectObligations(projectId: string): Promise<PmProjectObligations> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/obligations`,
    'GET',
  )) as PmProjectObligations;
}

export async function pmCreateObligation(
  projectId: string,
  body: PmCreateObligationRequest,
): Promise<PmObligation> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/obligations`,
    'POST',
    body,
  )) as PmObligation;
}

export async function pmUpdateObligation(id: string, body: PmUpdateObligationRequest): Promise<PmObligation> {
  return (await fetchFromOperations(
    `/api/v1/obligations/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmObligation;
}

export async function pmDeleteObligation(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/obligations/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmGetProjectAuditPacks(projectId: string): Promise<PmProjectAuditPacks> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/audit-packs`,
    'GET',
  )) as PmProjectAuditPacks;
}

export async function pmCreateAuditPack(
  projectId: string,
  body: PmCreateAuditPackRequest,
): Promise<PmAuditPack> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/audit-packs`,
    'POST',
    body,
  )) as PmAuditPack;
}

export async function pmUpdateAuditPack(id: string, body: PmUpdateAuditPackRequest): Promise<PmAuditPack> {
  return (await fetchFromOperations(
    `/api/v1/audit-packs/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmAuditPack;
}

export async function pmDeleteAuditPack(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/audit-packs/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmGetProjectMeetings(projectId: string): Promise<PmProjectMeetings> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/meetings`,
    'GET',
  )) as PmProjectMeetings;
}

export async function pmCreateMeeting(projectId: string, body: PmCreateMeetingRequest): Promise<PmMeeting> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/meetings`,
    'POST',
    body,
  )) as PmMeeting;
}

export async function pmUpdateMeeting(id: string, body: PmUpdateMeetingRequest): Promise<PmMeeting> {
  return (await fetchFromOperations(`/api/v1/meetings/${encodeURIComponent(id)}`, 'PUT', body)) as PmMeeting;
}

export async function pmDeleteMeeting(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/meetings/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmCreateMeetingAction(
  meetingId: string,
  body: PmCreateMeetingActionRequest,
): Promise<PmMeetingAction> {
  return (await fetchFromOperations(
    `/api/v1/meetings/${encodeURIComponent(meetingId)}/actions`,
    'POST',
    body,
  )) as PmMeetingAction;
}

export async function pmUpdateMeetingAction(
  id: string,
  body: PmUpdateMeetingActionRequest,
): Promise<PmMeetingAction> {
  return (await fetchFromOperations(
    `/api/v1/meeting-actions/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmMeetingAction;
}

export async function pmDeleteMeetingAction(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/meeting-actions/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmGetProjectStakeholders(projectId: string): Promise<PmProjectStakeholders> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/stakeholders`,
    'GET',
  )) as PmProjectStakeholders;
}

export async function pmCreateStakeholder(
  projectId: string,
  body: PmCreateStakeholderRequest,
): Promise<PmStakeholder> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/stakeholders`,
    'POST',
    body,
  )) as PmStakeholder;
}

export async function pmUpdateStakeholder(id: string, body: PmUpdateStakeholderRequest): Promise<PmStakeholder> {
  return (await fetchFromOperations(
    `/api/v1/stakeholders/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmStakeholder;
}

export async function pmDeleteStakeholder(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/stakeholders/${encodeURIComponent(id)}`, 'DELETE');
}

export async function pmGetProjectProcessMaps(projectId: string): Promise<PmProjectProcessMaps> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/process-maps`,
    'GET',
  )) as PmProjectProcessMaps;
}

export async function pmCreateProcessMap(
  projectId: string,
  body: PmCreateProcessMapRequest,
): Promise<PmProcessMap> {
  return (await fetchFromOperations(
    `/api/v1/projects/${encodeURIComponent(projectId)}/process-maps`,
    'POST',
    body,
  )) as PmProcessMap;
}

export async function pmUpdateProcessMap(id: string, body: PmUpdateProcessMapRequest): Promise<PmProcessMap> {
  return (await fetchFromOperations(
    `/api/v1/process-maps/${encodeURIComponent(id)}`,
    'PUT',
    body,
  )) as PmProcessMap;
}

export async function pmDeleteProcessMap(id: string): Promise<void> {
  await fetchFromOperations(`/api/v1/process-maps/${encodeURIComponent(id)}`, 'DELETE');
}

export function pmDateInput(value?: string | null): string {
  if (!value) return '';
  return String(value).slice(0, 10);
}

export function pmFormatDate(value?: string | null, locale = 'tr'): string {
  const iso = pmDateInput(value);
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  if (!match) return iso;
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  if (Number.isNaN(date.getTime())) return iso;
  const tag = locale.toLowerCase().startsWith('en') ? 'en-GB' : 'tr-TR';
  return new Intl.DateTimeFormat(tag, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date);
}

export function pmDatePayload(value?: string | null): string | null {
  const trimmed = value?.trim();
  if (!trimmed) return null;
  if (trimmed.length === 10) return `${trimmed}T00:00:00.000Z`;
  return trimmed;
}
