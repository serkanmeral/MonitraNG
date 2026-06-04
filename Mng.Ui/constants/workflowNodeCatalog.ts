export interface WorkflowNodeCatalogItem {
  type: string;
  category: 'trigger' | 'flow' | 'integration' | 'operation' | 'security';
  labelKey: string;
  defaultConfig: Record<string, unknown>;
}

export const WORKFLOW_NODE_CATALOG: WorkflowNodeCatalogItem[] = [
  { type: 'manual.trigger', category: 'trigger', labelKey: 'manualTrigger', defaultConfig: {} },
  { type: 'if', category: 'flow', labelKey: 'if', defaultConfig: { expression: 'true' } },
  { type: 'write.log', category: 'flow', labelKey: 'writeLog', defaultConfig: { message: '' } },
  { type: 'http.request', category: 'integration', labelKey: 'httpRequest', defaultConfig: { method: 'GET', url: '' } },
  { type: 'approval.wait', category: 'flow', labelKey: 'approvalWait', defaultConfig: { approverGroup: 'SecurityAdmins' } },
  { type: 'delay.wait', category: 'flow', labelKey: 'delayWait', defaultConfig: { delaySeconds: 60 } },
  { type: 'parallel.fork', category: 'flow', labelKey: 'parallelFork', defaultConfig: { branches: ['a', 'b'] } },
  { type: 'parallel.join', category: 'flow', labelKey: 'parallelJoin', defaultConfig: {} },
  { type: 'workitem.create', category: 'operation', labelKey: 'workitemCreate', defaultConfig: { workspaceId: '', title: '' } },
  { type: 'workitem.transition', category: 'operation', labelKey: 'workitemTransition', defaultConfig: { workItemId: '', transitionKey: '' } },
  { type: 'workitem.update', category: 'operation', labelKey: 'workitemUpdate', defaultConfig: { workItemId: '', patch: {} } },
  { type: 'engine.command', category: 'security', labelKey: 'engineCommand', defaultConfig: { engineId: '', command: '' } },
  { type: 'block.ip', category: 'security', labelKey: 'blockIp', defaultConfig: { ip: '', engineId: '' } },
];

export const WORKFLOW_EDGE_KEY_PRESETS = ['default', 'approved', 'rejected', 'true', 'false'] as const;

export const WORKFLOW_TRIGGER_TYPES = [
  { value: 'event', labelKey: 'event' },
  { value: 'schedule', labelKey: 'schedule' },
] as const;

export const WORKFLOW_EVENT_TYPES = [
  'alarm.raised',
  'alarm.updated',
  'alarm.resolved',
  'oc.workitem.created',
  'oc.workitem.updated',
] as const;

export type SaveWorkflowVersionPayload = {
  entryNodeId: string;
  nodes: { id: string; type: string; config?: Record<string, unknown> }[];
  edges: { fromNodeId: string; toNodeId: string; edgeKey: string }[];
  triggers: {
    type: string;
    config?: Record<string, unknown>;
    filterExpression?: string | null;
    enabled?: boolean;
  }[];
};

export function createStarterWorkflowGraph(): SaveWorkflowVersionPayload {
  return {
    entryNodeId: 'manual_1',
    nodes: [
      { id: 'manual_1', type: 'manual.trigger', config: {} },
      { id: 'log_1', type: 'write.log', config: { message: 'Workflow started' } },
    ],
    edges: [{ fromNodeId: 'manual_1', toNodeId: 'log_1', edgeKey: 'default' }],
    triggers: [],
  };
}
