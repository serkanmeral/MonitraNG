import type {
  DecideWorkflowApprovalRequest,
  WorkflowApprovalDecisionResult,
  WorkflowApprovalStatus,
  WorkflowApprovalSummary,
} from '@/types/apps/workflow';
import type {
  CreateWorkflowDefinitionRequest,
  SaveWorkflowVersionRequest,
  StartWorkflowRunRequest,
  UpdateWorkflowDefinitionRequest,
  WorkflowDefinitionDocument,
  WorkflowDefinitionSummary,
  WorkflowInstanceSummary,
  WorkflowRunDetail,
  WorkflowRunStartResult,
  WorkflowVersionDocument,
} from '@/types/apps/workflowDefinition';
import { useAuthStore } from '@/stores/auth';

function domainHeaders(): Record<string, string> {
  const auth = useAuthStore();
  const headers: Record<string, string> = {};
  if (auth.domainName) {
    headers['X-Domain-Name'] = auth.domainName;
  }
  return headers;
}

function buildQuery(params: Record<string, string | number | undefined>): string {
  const q = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue;
    q.set(key, String(value));
  }
  const s = q.toString();
  return s ? `?${s}` : '';
}

export async function workflowListApprovals(
  status?: WorkflowApprovalStatus,
  skip = 0,
  limit = 50,
): Promise<WorkflowApprovalSummary[]> {
  const qs = buildQuery({ status, skip, limit });
  return await $fetch<WorkflowApprovalSummary[]>(`/api/workflow/v1/approvals${qs}`, {
    method: 'GET',
    headers: domainHeaders(),
  });
}

export async function workflowDecideApproval(
  approvalId: string,
  request: DecideWorkflowApprovalRequest,
): Promise<WorkflowApprovalDecisionResult> {
  return await $fetch<WorkflowApprovalDecisionResult>(
    `/api/workflow/v1/approvals/${encodeURIComponent(approvalId)}/decide`,
    {
      method: 'POST',
      headers: domainHeaders(),
      body: request,
    },
  );
}

export async function workflowDefinitionList(): Promise<WorkflowDefinitionSummary[]> {
  return await $fetch<WorkflowDefinitionSummary[]>('/api/workflow/v1/definitions', {
    method: 'GET',
    headers: domainHeaders(),
  });
}

export async function workflowDefinitionGet(workflowId: string): Promise<WorkflowDefinitionDocument> {
  return await $fetch<WorkflowDefinitionDocument>(
    `/api/workflow/v1/definitions/${encodeURIComponent(workflowId)}`,
    { method: 'GET', headers: domainHeaders() },
  );
}

export async function workflowDefinitionCreate(
  body: CreateWorkflowDefinitionRequest,
): Promise<WorkflowDefinitionDocument> {
  return await $fetch<WorkflowDefinitionDocument>('/api/workflow/v1/definitions', {
    method: 'POST',
    headers: domainHeaders(),
    body,
  });
}

export async function workflowDefinitionUpdate(
  workflowId: string,
  body: UpdateWorkflowDefinitionRequest,
): Promise<WorkflowDefinitionDocument> {
  return await $fetch<WorkflowDefinitionDocument>(
    `/api/workflow/v1/definitions/${encodeURIComponent(workflowId)}`,
    { method: 'PUT', headers: domainHeaders(), body },
  );
}

export async function workflowVersionList(workflowId: string): Promise<WorkflowVersionDocument[]> {
  return await $fetch<WorkflowVersionDocument[]>(
    `/api/workflow/v1/definitions/${encodeURIComponent(workflowId)}/versions`,
    { method: 'GET', headers: domainHeaders() },
  );
}

export async function workflowVersionGet(versionId: string): Promise<WorkflowVersionDocument> {
  return await $fetch<WorkflowVersionDocument>(
    `/api/workflow/v1/versions/${encodeURIComponent(versionId)}`,
    { method: 'GET', headers: domainHeaders() },
  );
}

export async function workflowVersionCreateDraft(
  workflowId: string,
  body: SaveWorkflowVersionRequest,
): Promise<WorkflowVersionDocument> {
  return await $fetch<WorkflowVersionDocument>(
    `/api/workflow/v1/definitions/${encodeURIComponent(workflowId)}/versions`,
    { method: 'POST', headers: domainHeaders(), body },
  );
}

export async function workflowVersionUpdateDraft(
  versionId: string,
  body: SaveWorkflowVersionRequest,
): Promise<WorkflowVersionDocument> {
  return await $fetch<WorkflowVersionDocument>(
    `/api/workflow/v1/versions/${encodeURIComponent(versionId)}`,
    { method: 'PUT', headers: domainHeaders(), body },
  );
}

export async function workflowVersionPublish(versionId: string): Promise<WorkflowVersionDocument> {
  return await $fetch<WorkflowVersionDocument>(
    `/api/workflow/v1/versions/${encodeURIComponent(versionId)}/publish`,
    { method: 'POST', headers: domainHeaders() },
  );
}

export async function workflowStartRun(body: StartWorkflowRunRequest): Promise<WorkflowRunStartResult> {
  return await $fetch<WorkflowRunStartResult>('/api/workflow/v1/runs', {
    method: 'POST',
    headers: domainHeaders(),
    body,
  });
}

export async function workflowListRuns(
  workflowId: string,
  limit = 10,
): Promise<WorkflowInstanceSummary[]> {
  const qs = buildQuery({ workflowId, limit });
  return await $fetch<WorkflowInstanceSummary[]>(`/api/workflow/v1/runs${qs}`, {
    method: 'GET',
    headers: domainHeaders(),
  });
}

export async function workflowGetRun(instanceId: string): Promise<WorkflowRunDetail> {
  return await $fetch<WorkflowRunDetail>(
    `/api/workflow/v1/runs/${encodeURIComponent(instanceId)}`,
    { method: 'GET', headers: domainHeaders() },
  );
}
