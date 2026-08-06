export type ScenarioLifecycleStatus = 'draft' | 'validated' | 'published' | 'archived';
export type ScenarioOrigin = 'user' | 'product';
export type ScenarioSourceKind =
  | 'observation'
  | 'scheduled-staleness'
  | 'scheduled-query'
  | 'meta-correlation';
export type ScenarioLogic = 'and' | 'or' | 'not';
export type ScenarioOperator = 'eq' | 'neq' | 'gt' | 'gte' | 'lt' | 'lte' | 'contains' | 'exists';
export type ScenarioAggregationFunction = 'count' | 'sum' | 'avg' | 'min' | 'max';

export interface ScenarioSource {
  kind: ScenarioSourceKind;
  observationKind?: string;
  matchKey: string;
  /** Optional multi-key match; when present, observation.key may match any entry. */
  matchKeys?: string[];
  query?: string;
  schedule?: string;
  scheduleDefinition?: ScenarioSchedule;
  dependsOnScenarioIds: string[];
  maxChainDepth: number;
}

export interface ScenarioSchedule {
  expression: string;
  timeZone: string;
  maxLookbackSeconds: number;
}

export interface ScenarioCondition {
  logic?: ScenarioLogic;
  children: ScenarioCondition[];
  field?: string;
  operator?: ScenarioOperator | string;
  value?: unknown;
  sustainedForSeconds: number;
}

export interface ScenarioAggregation {
  function: ScenarioAggregationFunction | string;
  field?: string;
  operator: ScenarioOperator | string;
  threshold: number;
}

export interface ScenarioWindow {
  durationSeconds: number;
  stalenessSeconds: number;
}

export interface ScenarioSequenceStep {
  matchKey: string;
  condition?: ScenarioCondition;
  minCount: number;
  withinSeconds: number;
}

export interface ScenarioSequence {
  steps: ScenarioSequenceStep[];
}

export interface ScenarioDedup {
  keyTemplate: string;
  cooldownSeconds: number;
}

export interface ScenarioHysteresis {
  raiseThreshold: number;
  clearThreshold: number;
  minimumStateSeconds: number;
}

/** The single canonical model shared by wizard, visual editor, JSON preview and API. */
export interface ScenarioDefinitionV2 {
  schemaVersion: 2;
  source: ScenarioSource;
  condition?: ScenarioCondition;
  aggregation?: ScenarioAggregation;
  groupBy: string[];
  window?: ScenarioWindow;
  sequence?: ScenarioSequence;
  dedup: ScenarioDedup;
  hysteresis?: ScenarioHysteresis;
  metadata: Record<string, string>;
}

export type ScenarioNodeType =
  | 'source'
  | 'condition'
  | 'filter'
  | 'aggregation'
  | 'threshold'
  | 'sequence'
  | 'decision'
  | 'alarm-output'
  | 'stop-output'
  | 'debug-output';

export interface ScenarioDebug {
  mode: 'complete' | 'path';
  path?: string;
  active: boolean;
}

export interface ScenarioNodeConfig {
  source?: ScenarioSource;
  condition?: ScenarioCondition;
  aggregation?: ScenarioAggregation;
  window?: ScenarioWindow;
  sequence?: ScenarioSequence;
  groupBy: string[];
  dedup?: ScenarioDedup;
  severity?: number;
  settleAfterSeconds: number;
  debug?: ScenarioDebug;
}

export interface ScenarioNodeLayout {
  x: number;
  y: number;
  label?: string;
}

export interface ScenarioGraphNode {
  id: string;
  type: ScenarioNodeType;
  config: ScenarioNodeConfig;
  layout?: ScenarioNodeLayout;
}

export interface ScenarioGraphEdge {
  id: string;
  from: string;
  to: string;
  fromPort: string;
  toPort: string;
}

export interface ScenarioDefinitionV3 {
  schemaVersion: 3;
  graph: {
    nodes: ScenarioGraphNode[];
    edges: ScenarioGraphEdge[];
  };
  /** V2 legacy fields remain at root for lossless migration and mixed-version readers. */
  source?: ScenarioSource;
  condition?: ScenarioCondition;
  aggregation?: ScenarioAggregation;
  groupBy?: string[];
  window?: ScenarioWindow;
  sequence?: ScenarioSequence;
  dedup?: ScenarioDedup;
  hysteresis?: ScenarioHysteresis;
  metadata?: Record<string, string>;
}

export type ScenarioDefinition = ScenarioDefinitionV2 | ScenarioDefinitionV3;

export interface ScenarioDiagnostic {
  code: string;
  message: string;
  path?: string;
  severity: string;
}

export interface ScenarioValidationSnapshot {
  isValid: boolean;
  diagnostics: ScenarioDiagnostic[];
  validatedAt: string;
}

export interface ScenarioVersion {
  id: string;
  scenarioId: string;
  domainId: string;
  domainName: string;
  version: number;
  status: ScenarioLifecycleStatus;
  name: string;
  enabled: boolean;
  severity: number;
  definition: ScenarioDefinition;
  origin: ScenarioOrigin;
  isReadOnly: boolean;
  templateId?: string;
  packageId?: string;
  packageVersion?: string;
  validation?: ScenarioValidationSnapshot;
  createdAt: string;
  updatedAt: string;
  publishedAt?: string;
}

export interface ScenarioCatalogItem {
  scenarioId: string;
  name: string;
  latestVersion: number;
  latestStatus: ScenarioLifecycleStatus;
  publishedVersion?: number;
  draftVersion?: number;
  enabled: boolean;
  severity: number;
  origin: ScenarioOrigin;
  isReadOnly: boolean;
  templateId?: string;
  packageId?: string;
  packageVersion?: string;
  updatedAt: string;
}

export interface ScenarioAuditEntry {
  id: string;
  scenarioId: string;
  domainName: string;
  version: number;
  action: string;
  timestamp: string;
}

export interface CreateScenarioDraftRequest {
  name: string;
  severity: number;
  enabled: boolean;
  definition: ScenarioDefinition;
}

export type UpdateScenarioDraftRequest = Partial<CreateScenarioDraftRequest>;

export interface ScenarioSampleObservation {
  kind: string;
  key: string;
  value?: number;
  dimensions: Record<string, unknown>;
  timestamp: string;
}

export interface ScenarioPreviewRequest {
  definition?: ScenarioDefinition;
  samples?: ScenarioSampleObservation[];
  from?: string;
  to?: string;
}

export interface ScenarioPreviewMatch {
  sampleIndex: number;
  matched: boolean;
  explanation: string;
  groupKey: string;
  dedupKey: string;
}

export interface ScenarioPreviewResponse {
  supported: boolean;
  diagnostics: ScenarioDiagnostic[];
  matches: ScenarioPreviewMatch[];
  groupCounts: Record<string, number>;
  dedupKeys: string[];
  nodeTrace: ScenarioPreviewNodeTrace[];
  debugLines?: ScenarioPreviewDebugLine[];
  executionOrder: string[];
  nextEvaluationAt?: string | null;
}

export interface ScenarioPreviewDebugLine {
  sampleIndex: number;
  nodeId: string;
  label: string;
  mode: string;
  path?: string | null;
  payload?: unknown;
  at: string;
}

export interface ScenarioPreviewNodeTrace {
  sampleIndex: number;
  nodeId: string;
  nodeType: ScenarioNodeType | string;
  status: string;
  outcome?: boolean | null;
  nextEvaluationAt?: string | null;
}

export type ScenarioEditorMode = 'wizard' | 'advanced';
export type ScenarioBehavior = 'threshold' | 'correlation' | 'staleness' | 'sequence';

export function createEmptyCondition(): ScenarioCondition {
  return {
    children: [],
    field: 'value',
    operator: 'gte',
    value: 1,
    sustainedForSeconds: 0,
  };
}

export function createScenarioDefinition(behavior: ScenarioBehavior = 'correlation'): ScenarioDefinitionV2 {
  const definition: ScenarioDefinitionV2 = {
    schemaVersion: 2,
    source: {
      kind: behavior === 'staleness' ? 'scheduled-staleness' : 'observation',
      observationKind: behavior === 'threshold' ? 'metric' : 'event',
      matchKey: behavior === 'threshold' ? 'cpu_usage' : 'login_failed',
      dependsOnScenarioIds: [],
      maxChainDepth: 5,
    },
    condition: behavior === 'threshold' ? createEmptyCondition() : undefined,
    aggregation:
      behavior === 'correlation'
        ? { function: 'count', operator: 'gte', threshold: 5 }
        : undefined,
    groupBy: [],
    window:
      behavior === 'correlation'
        ? { durationSeconds: 300, stalenessSeconds: 0 }
        : behavior === 'staleness'
          ? { durationSeconds: 300, stalenessSeconds: 1800 }
          : undefined,
    sequence:
      behavior === 'sequence'
        ? {
            steps: [
              { matchKey: 'login_failed', minCount: 3, withinSeconds: 600 },
              { matchKey: 'login_success', minCount: 1, withinSeconds: 900 },
            ],
          }
        : undefined,
    dedup: {
      keyTemplate: behavior === 'correlation' || behavior === 'sequence'
        ? '{ruleId}:{groupKey}'
        : '{ruleId}:{key}',
      cooldownSeconds: 300,
    },
    metadata: {},
  };
  return definition;
}

/** Blank V3 canvas for the flow editor (nodes are added from the palette). */
export function createEmptyScenarioDefinitionV3(): ScenarioDefinitionV3 {
  return {
    schemaVersion: 3,
    graph: { nodes: [], edges: [] },
    groupBy: [],
    metadata: {},
  };
}
