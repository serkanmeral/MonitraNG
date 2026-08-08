import { isProxy, toRaw } from 'vue';
import { MarkerType, type Edge, type Node } from '@vue-flow/core';
import type {
  ScenarioDefinition,
  ScenarioDefinitionV2,
  ScenarioDefinitionV3,
  ScenarioGraphEdge,
  ScenarioGraphNode,
  ScenarioNodeConfig,
  ScenarioNodeType,
} from '@/types/apps/scenario';
import { alarmOutputSubtitle, normalizeAlarmDedup } from '@/utils/alarm/alarmOutputMerge';

export interface ScenarioVueFlow {
  nodes: Node[];
  edges: Edge[];
  definition: ScenarioDefinitionV3;
}

const visuals: Record<ScenarioNodeType, { color: string; icon: string }> = {
  source: { color: '#2f80ed', icon: 'mdi-database-arrow-right-outline' },
  condition: { color: '#f2994a', icon: 'mdi-code-braces' },
  filter: { color: '#9c6ade', icon: 'mdi-filter-outline' },
  aggregation: { color: '#00a896', icon: 'mdi-chart-timeline-variant' },
  threshold: { color: '#00a896', icon: 'mdi-chart-timeline-variant' },
  sequence: { color: '#5b5f97', icon: 'mdi-format-list-numbered' },
  decision: { color: '#f2994a', icon: 'mdi-source-branch' },
  'alarm-output': { color: '#d64545', icon: 'mdi-bell-ring-outline' },
  'stop-output': { color: '#687386', icon: 'mdi-stop-circle-outline' },
  'debug-output': { color: '#c9a227', icon: 'mdi-bug-outline' },
};

/** Recursively unwraps Vue proxies without relying on structuredClone. */
export function cloneScenarioValue<T>(value: T): T {
  const raw = isProxy(value) ? toRaw(value) : value;
  if (Array.isArray(raw)) return raw.map(item => cloneScenarioValue(item)) as T;
  if (raw && typeof raw === 'object') {
    const result: Record<string, unknown> = {};
    for (const [key, item] of Object.entries(raw as Record<string, unknown>)) {
      if (item !== undefined) result[key] = cloneScenarioValue(item);
    }
    return result as T;
  }
  return raw;
}

function normalizeConfig(config?: Partial<ScenarioNodeConfig>): ScenarioNodeConfig {
  const cloned = cloneScenarioValue(config ?? {});
  return {
    ...cloned,
    groupBy: cloneScenarioValue(config?.groupBy ?? []),
    settleAfterSeconds: Number(config?.settleAfterSeconds ?? 0),
    dedup: config?.dedup || cloned.dedup
      ? normalizeAlarmDedup(config?.dedup ?? cloned.dedup)
      : undefined,
  };
}

function subtitle(node: ScenarioGraphNode): string {
  const config = node.config;
  if (config.source) return config.source.matchKey;
  if (config.condition) {
    return `${config.condition.field ?? 'value'} ${config.condition.operator ?? ''} ${String(config.condition.value ?? '')}`;
  }
  if (config.aggregation) {
    return `${config.aggregation.function} ${config.aggregation.operator} ${config.aggregation.threshold}`;
  }
  if (config.sequence) return config.sequence.steps.map(step => step.matchKey).join(' → ');
  if (node.type === 'alarm-output') {
    return alarmOutputSubtitle({
      groupBy: config.groupBy ?? [],
      settleAfterSeconds: config.settleAfterSeconds ?? 0,
      severity: config.severity,
      dedup: config.dedup,
    });
  }
  if (node.type === 'debug-output') {
    const debug = config.debug;
    if (!debug?.active) return 'inactive';
    if (debug.mode === 'path') return debug.path?.trim() || 'path';
    return 'complete';
  }
  return '';
}

function projectV2(definition: ScenarioDefinitionV2, severity = 5): ScenarioDefinitionV3 {
  const nodes: ScenarioGraphNode[] = [];
  const edges: ScenarioGraphEdge[] = [];
  let x = 60;
  const append = (node: ScenarioGraphNode) => {
    const previous = nodes.at(-1);
    nodes.push(node);
    if (previous) {
      const fromPort = [
        'condition',
        'filter',
        'aggregation',
        'threshold',
        'sequence',
        'decision',
      ].includes(previous.type)
        ? 'true'
        : 'next';
      edges.push({
        id: `edge-${previous.id}-${node.id}`,
        from: previous.id,
        to: node.id,
        fromPort,
        toPort: 'in',
      });
    }
    x += 280;
  };

  append({
    id: 'source',
    type: 'source',
    config: normalizeConfig({ source: cloneScenarioValue(definition.source) }),
    layout: { x, y: 180, label: 'Source' },
  });
  if (definition.condition) {
    append({
      id: 'condition',
      type: 'condition',
      config: normalizeConfig({ condition: cloneScenarioValue(definition.condition) }),
      layout: { x, y: 180, label: 'Condition' },
    });
  }
  if (definition.aggregation) {
    append({
      id: 'threshold',
      type: 'threshold',
      config: normalizeConfig({
        aggregation: cloneScenarioValue(definition.aggregation),
        window: cloneScenarioValue(definition.window ?? { durationSeconds: 300, stalenessSeconds: 0 }),
        groupBy: cloneScenarioValue(definition.groupBy),
      }),
      layout: { x, y: 180, label: 'Threshold' },
    });
  }
  if (definition.sequence) {
    append({
      id: 'sequence',
      type: 'sequence',
      config: normalizeConfig({
        sequence: cloneScenarioValue(definition.sequence),
        groupBy: cloneScenarioValue(definition.groupBy),
      }),
      layout: { x, y: 180, label: 'Sequence' },
    });
  }
  append({
    id: 'alarm',
    type: 'alarm-output',
    config: normalizeConfig({ severity, dedup: cloneScenarioValue(definition.dedup) }),
    layout: { x, y: 180, label: 'Alarm' },
  });

  return {
    ...cloneScenarioValue(definition),
    schemaVersion: 3,
    graph: { nodes, edges },
  };
}

function clampSeverity(value: unknown, fallback = 5): number {
  const n = Number(value);
  if (!Number.isFinite(n)) return fallback;
  return Math.min(10, Math.max(1, Math.round(n)));
}

/** Ensures every alarm-output node has an explicit severity (version field is fallback). */
export function syncAlarmOutputSeverity(
  definition: ScenarioDefinitionV3,
  severity = 5,
): ScenarioDefinitionV3 {
  const next = cloneScenarioValue(definition);
  const fallback = clampSeverity(severity, 5);
  for (const node of next.graph.nodes) {
    if (node.type !== 'alarm-output') continue;
    node.config = normalizeConfig({
      ...node.config,
      severity: clampSeverity(node.config.severity ?? fallback, fallback),
    });
  }
  return next;
}

export function ensureScenarioV3(
  definition: ScenarioDefinition,
  severity = 5,
): ScenarioDefinitionV3 {
  const base = definition.schemaVersion === 3
    ? cloneScenarioValue(definition)
    : projectV2(definition, severity);
  return syncAlarmOutputSeverity(base, severity);
}

export function scenarioToVueFlow(
  definition: ScenarioDefinition,
  severity = 5,
): ScenarioVueFlow {
  const v3 = ensureScenarioV3(definition, severity);
  return {
    definition: v3,
    nodes: v3.graph.nodes.map((node, index) => {
      const config = normalizeConfig(node.config);
      return {
        id: node.id,
        type: node.type,
        position: {
          x: Number(node.layout?.x ?? 60 + index * 280),
          y: Number(node.layout?.y ?? 180),
        },
        data: {
          nodeType: node.type,
          label: node.layout?.label ?? node.id,
          subtitle: subtitle({ ...node, config }),
          config,
          ...visuals[node.type],
        },
      };
    }),
    edges: v3.graph.edges.map(edge => ({
      id: edge.id,
      source: edge.from,
      target: edge.to,
      sourceHandle: edge.fromPort === 'next' ? undefined : edge.fromPort,
      targetHandle: edge.toPort === 'in' ? undefined : edge.toPort,
      label: edge.fromPort === 'true' ? 'True' : edge.fromPort === 'false' ? 'False' : undefined,
      type: 'smoothstep',
      markerEnd: MarkerType.ArrowClosed,
      animated: edge.fromPort === 'next',
      data: { fromPort: edge.fromPort, toPort: edge.toPort },
    })),
  };
}

export function vueFlowToScenario(
  nodes: Node[],
  edges: Edge[],
  previous?: ScenarioDefinitionV3,
  severity = 5,
): ScenarioDefinitionV3 {
  const graphNodes: ScenarioGraphNode[] = nodes.map(node => ({
    id: node.id,
    type: node.data.nodeType as ScenarioNodeType,
    config: normalizeConfig(cloneScenarioValue(node.data.config ?? {})),
    layout: {
      x: Number(node.position.x),
      y: Number(node.position.y),
      label: String(node.data.label || node.id),
    },
  }));
  const graphEdges: ScenarioGraphEdge[] = edges.map(edge => ({
    id: edge.id,
    from: edge.source,
    to: edge.target,
    fromPort: edge.sourceHandle ?? String(edge.data?.fromPort ?? 'next'),
    toPort: edge.targetHandle ?? String(edge.data?.toPort ?? 'in'),
  }));

  const legacy: Partial<Omit<ScenarioDefinitionV3, 'schemaVersion' | 'graph'>> = {};
  const legacyKeys = [
    'source', 'condition', 'aggregation', 'groupBy', 'window',
    'sequence', 'dedup', 'hysteresis', 'metadata',
  ] as const;
  for (const key of legacyKeys) {
    if (previous?.[key] !== undefined) {
      (legacy as Record<string, unknown>)[key] = cloneScenarioValue(previous[key]);
    }
  }
  return syncAlarmOutputSeverity(
    { ...legacy, schemaVersion: 3, graph: { nodes: graphNodes, edges: graphEdges } },
    severity,
  );
}

export function isWizardCompatible(definition: ScenarioDefinitionV3): boolean {
  const types = definition.graph.nodes.map(node => node.type);
  const incomingCounts = new Map<string, number>();
  const outgoingCounts = new Map<string, number>();
  for (const edge of definition.graph.edges) {
    incomingCounts.set(edge.to, (incomingCounts.get(edge.to) ?? 0) + 1);
    outgoingCounts.set(edge.from, (outgoingCounts.get(edge.from) ?? 0) + 1);
  }
  return types.filter(type => type === 'source').length === 1
    && types.filter(type => type === 'alarm-output').length === 1
    && definition.graph.nodes.length <= 4
    && definition.graph.edges.length === definition.graph.nodes.length - 1
    && !definition.graph.edges.some(edge => edge.fromPort === 'false')
    && [...incomingCounts.values()].every(count => count <= 1)
    && [...outgoingCounts.values()].every(count => count <= 1);
}

export function traceGraph(
  definition: ScenarioDefinitionV3,
  nodeTrace: Array<{ nodeId: string; status: string }>,
  executionOrder: string[],
): { nodeIds: string[]; edgeIds: string[] } {
  const reached = new Set(
    nodeTrace.filter(item => item.status !== 'skipped').map(item => item.nodeId),
  );
  if (!reached.size) {
    for (const nodeId of executionOrder) reached.add(nodeId);
  }
  const order = executionOrder.filter(nodeId => reached.has(nodeId));
  const adjacent = new Set(order.slice(1).map((nodeId, index) => `${order[index]}\0${nodeId}`));
  const edgeIds = definition.graph.edges
    .filter(edge =>
      reached.has(edge.from)
      && reached.has(edge.to)
      && (adjacent.has(`${edge.from}\0${edge.to}`) || !order.length),
    )
    .map(edge => edge.id);
  return { nodeIds: [...reached], edgeIds };
}
