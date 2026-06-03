import type {
  DecideWorkflowApprovalRequest,
  WorkflowApprovalDecisionResult,
  WorkflowApprovalStatus,
  WorkflowApprovalSummary,
} from '@/types/apps/workflow';
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
