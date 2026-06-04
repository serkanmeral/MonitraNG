export type WorkflowVersionStatus = 'Draft' | 'Published' | 'Archived' | 0 | 1 | 2;

export type WorkflowInstanceStatus = 'Running' | 'Waiting' | 'Completed' | 'Failed' | 'Cancelled' | 0 | 1 | 2 | 3 | 4;

export interface WorkflowNodeDefinition {
  id: string;
  type: string;
  config?: Record<string, unknown>;
}

export interface WorkflowEdgeDefinition {
  fromNodeId: string;
  toNodeId: string;
  edgeKey: string;
}

export interface WorkflowTriggerDefinition {
  type: string;
  config?: Record<string, unknown>;
  filterExpression?: string | null;
  enabled?: boolean;
}

export interface WorkflowDefinitionSummary {
  id: string;
  key: string;
  name: string;
  category?: string | null;
  currentVersion: number;
  currentVersionId?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface WorkflowDefinitionDocument extends WorkflowDefinitionSummary {
  domainId?: string;
  domainName?: string;
}

export interface WorkflowVersionDocument {
  id: string;
  workflowId: string;
  version: number;
  status: WorkflowVersionStatus;
  entryNodeId: string;
  nodes: WorkflowNodeDefinition[];
  edges: WorkflowEdgeDefinition[];
  triggers: WorkflowTriggerDefinition[];
  publishedAt?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateWorkflowDefinitionRequest {
  key: string;
  name: string;
  category?: string;
}

export interface UpdateWorkflowDefinitionRequest {
  name: string;
  category?: string;
}

export interface SaveWorkflowVersionRequest {
  entryNodeId: string;
  nodes: WorkflowNodeDefinition[];
  edges: WorkflowEdgeDefinition[];
  triggers: WorkflowTriggerDefinition[];
}

export interface StartWorkflowRunRequest {
  workflowId?: string;
  workflowVersionId?: string;
  triggerType?: string;
  triggerData?: Record<string, unknown>;
}

export interface WorkflowRunStartResult {
  instanceId: string;
  correlationId: string;
  workflowVersionId: string;
  entryNodeId: string;
  status: string;
}

export interface WorkflowInstanceSummary {
  id: string;
  workflowId: string;
  workflowVersionId: string;
  status: WorkflowInstanceStatus;
  correlationId: string;
  triggerType: string;
  startedAt: string;
  finishedAt?: string | null;
}

export interface NodeExecutionSummary {
  nodeId: string;
  attempt: number;
  status: number;
  errorMessage?: string | null;
  startedAt: string;
  finishedAt?: string | null;
}

export interface WorkflowRunDetail {
  instance: WorkflowInstanceSummary;
  executions: NodeExecutionSummary[];
}
