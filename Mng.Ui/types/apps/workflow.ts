export type WorkflowApprovalStatus = 'Pending' | 'Approved' | 'Rejected';

export interface WorkflowApprovalSummary {
  id: string;
  instanceId: string;
  workflowId: string;
  nodeId: string;
  approverTarget: string;
  status: WorkflowApprovalStatus;
  decidedBy?: string | null;
  comment?: string | null;
  createdAt: string;
  decidedAt?: string | null;
}

export interface DecideWorkflowApprovalRequest {
  approved: boolean;
  comment?: string;
  decidedBy?: string;
}

export interface WorkflowApprovalDecisionResult {
  approvalId: string;
  instanceId: string;
  status: WorkflowApprovalStatus;
}
