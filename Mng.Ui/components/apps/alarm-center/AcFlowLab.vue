<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue';
import {
  Handle,
  MarkerType,
  Position,
  VueFlow,
  useVueFlow,
  type Connection,
  type Edge,
  type Node,
} from '@vue-flow/core';
import { Background } from '@vue-flow/background';
import { Controls } from '@vue-flow/controls';
import { MiniMap } from '@vue-flow/minimap';
import { useAppI18n } from '@/composables/useAppI18n';
import { useResizableTreePanel } from '@/composables/useResizableTreePanel';
import { useScenarioStudioApi } from '@/composables/useScenarioStudioApi';
import {
  createEmptyScenarioDefinitionV3,
  type ScenarioAuditEntry,
  type ScenarioCatalogItem,
  type ScenarioPreviewResponse,
  type ScenarioSampleObservation,
  type ScenarioNodeType,
  type ScenarioVersion,
} from '@/types/apps/scenario';
import {
  cloneScenarioValue,
  ensureScenarioV3,
  isWizardCompatible,
  scenarioToVueFlow,
  traceGraph,
  vueFlowToScenario,
} from '@/utils/alarm/scenarioFlowMapper';
import AcScenarioCatalogTree from '@/components/apps/alarm-center/AcScenarioCatalogTree.vue';
import AcScenarioSourceInspector from '@/components/apps/alarm-center/AcScenarioSourceInspector.vue';
import {
  coerceSimpleSourceState,
  defaultSimpleSourceState,
  eventCodeFilterCondition,
  hostFilterCondition,
  inferSimpleSourceState,
  managedEventCodeFilterId,
  managedHostFilterId,
  managedMetricConditionId,
  managedOsFilterId,
  metricConditionSpec,
  normalizeSimpleHosts,
  simpleSourceSubtitle,
  sourceTypeForPlatform,
  type SimpleMetricComparison,
  type SimpleSourceState,
} from '@/utils/alarm/scenarioSimpleSource';

const { t } = useAppI18n();
const api = useScenarioStudioApi();
const { addEdges, fitView, removeNodes, screenToFlowCoordinate } = useVueFlow();
const {
  treeWidth: libraryWidth,
  treeCollapsed: libraryCollapsed,
  toggleTreeCollapse: toggleLibraryCollapse,
} = useResizableTreePanel('siem-scenario-studio-library-v3', {
  minWidth: 260,
  maxWidth: 400,
  defaultWidth: 300,
});
const {
  treeCollapsed: paletteCollapsed,
  toggleTreeCollapse: togglePaletteCollapse,
} = useResizableTreePanel('siem-scenario-studio-palette-v3', {
  minWidth: 200,
  maxWidth: 280,
  defaultWidth: 220,
});

const catalog = ref<ScenarioCatalogItem[]>([]);
const current = ref<ScenarioVersion | null>(null);
const nodes = ref<Node[]>([]);
const edges = ref<Edge[]>([]);
const selectedNodeId = ref<string | null>(null);
const search = ref('');
const message = ref('');
const localError = ref('');
const catalogLoaded = ref(false);
const showWizard = ref(false);
const showAudit = ref(false);
const showSimulation = ref(false);
const auditEntries = ref<ScenarioAuditEntry[]>([]);
const simulationResult = ref<ScenarioPreviewResponse | null>(null);
const simulationJson = ref(JSON.stringify([{
  kind: 'event',
  key: 'login_failed',
  dimensions: { userId: 'admin', srcIp: '192.168.1.50' },
  timestamp: new Date().toISOString(),
}], null, 2));
const traceNodeIds = ref<string[]>([]);
const traceEdgeIds = ref<string[]>([]);

const selectedNode = computed<any>(() =>
  nodes.value.find(node => node.id === selectedNodeId.value) ?? null,
);
const canEdit = computed(() =>
  !current.value || (current.value.status === 'draft' && !current.value.isReadOnly),
);
const canPublish = computed(() =>
  current.value?.status === 'validated'
  && current.value.validation?.isValid === true
  && !current.value.isReadOnly,
);
const canStartEdit = computed(() =>
  !!current.value
  && !current.value.isReadOnly
  && current.value.status !== 'draft',
);
const productTemplates = computed(() => filterCatalog('product'));
const userScenarios = computed(() => filterCatalog('user'));
const currentDefinition = computed(() =>
  vueFlowToScenario(nodes.value, edges.value, current.value
    ? ensureScenarioV3(current.value.definition, current.value.severity)
    : undefined),
);
const wizardCompatible = computed(() => isWizardCompatible(currentDefinition.value));
const sourceNode = computed<any>(() => nodes.value.find(node => node.data.nodeType === 'source'));
const aggregationNode = computed<any>(() =>
  nodes.value.find(node => ['aggregation', 'threshold'].includes(node.data.nodeType)),
);
const alarmNode = computed<any>(() => nodes.value.find(node => node.data.nodeType === 'alarm-output'));

type PaletteGroupId = 'events' | 'functions' | 'outputs';

const paletteGroupCollapsed = ref<Record<PaletteGroupId, boolean>>({
  events: false,
  functions: false,
  outputs: false,
});

const paletteGroups = computed(() => [
  {
    id: 'events' as const,
    title: t('alarmCenter.flowLab.nodeGroups.events'),
    items: [
      nodeTemplate('source', t('alarmCenter.flowLab.nodes.eventSource'), 'mdi-database-arrow-right-outline', '#2f80ed'),
    ],
  },
  {
    id: 'functions' as const,
    title: t('alarmCenter.flowLab.nodeGroups.functions'),
    items: [
      // Advanced building blocks stay available; simple source covers the common path.
      nodeTemplate('condition', t('alarmCenter.scenarioStudio.steps.condition'), 'mdi-code-braces', '#f2994a'),
      nodeTemplate('filter', t('alarmCenter.flowLab.nodes.filter'), 'mdi-filter-outline', '#9c6ade'),
      nodeTemplate('aggregation', t('alarmCenter.flowLab.nodes.aggregate'), 'mdi-chart-timeline-variant', '#00a896'),
      nodeTemplate('threshold', t('alarmCenter.flowLab.nodes.threshold'), 'mdi-gauge', '#00a896'),
      nodeTemplate('sequence', t('alarmCenter.flowLab.nodes.sequence'), 'mdi-format-list-numbered', '#5b5f97'),
      nodeTemplate('decision', t('alarmCenter.flowLab.nodes.decision'), 'mdi-source-branch', '#f2994a'),
    ],
  },
  {
    id: 'outputs' as const,
    title: t('alarmCenter.flowLab.nodeGroups.outputs'),
    items: [
      nodeTemplate('alarm-output', t('alarmCenter.flowLab.nodes.alarm'), 'mdi-bell-ring-outline', '#d64545'),
      nodeTemplate('stop-output', t('alarmCenter.flowLab.nodes.stop'), 'mdi-stop-circle-outline', '#687386'),
      nodeTemplate('debug-output', t('alarmCenter.flowLab.nodes.debug'), 'mdi-bug-outline', '#c9a227'),
    ],
  },
]);

function togglePaletteGroup(groupId: PaletteGroupId) {
  paletteGroupCollapsed.value[groupId] = !paletteGroupCollapsed.value[groupId];
}

const debugModeItems = computed(() => [
  { value: 'complete', title: t('alarmCenter.scenarioStudio.debug.modeComplete') },
  { value: 'path', title: t('alarmCenter.scenarioStudio.debug.modePath') },
]);

const debugLines = computed(() => simulationResult.value?.debugLines ?? []);

watch(selectedNode, (node) => {
  if (node?.data?.nodeType !== 'debug-output') return;
  if (!node.data.config.debug) {
    node.data.config.debug = { mode: 'complete', path: '', active: true };
  }
});

function formatDebugPayload(payload: unknown): string {
  if (payload === undefined) return 'undefined';
  if (payload === null) return 'null';
  if (typeof payload === 'string') return payload;
  try {
    return JSON.stringify(payload, null, 2);
  } catch {
    return String(payload);
  }
}

function onDebugConfigChange() {
  const node = selectedNode.value;
  if (!node || node.data.nodeType !== 'debug-output') return;
  const debug = node.data.config.debug ?? { mode: 'complete', path: '', active: true };
  node.data.config.debug = debug;
  node.data.subtitle = !debug.active
    ? 'inactive'
    : debug.mode === 'path'
      ? (String(debug.path || '').trim() || 'path')
      : 'complete';
}

function nodeTemplate(nodeType: ScenarioNodeType, label: string, icon: string, color: string) {
  const condition = { children: [], field: 'value', operator: 'gte', value: 1, sustainedForSeconds: 0 };
  const configs = {
    source: { source: {
        kind: 'observation',
        observationKind: 'event',
        matchKey: 'rdp.logon',
        dependsOnScenarioIds: [],
        maxChainDepth: 5,
      }, groupBy: [], settleAfterSeconds: 0 },
    condition: { condition: cloneScenarioValue(condition), groupBy: [], settleAfterSeconds: 0 },
    filter: { condition: cloneScenarioValue(condition), groupBy: [], settleAfterSeconds: 0 },
    aggregation: {
      aggregation: { function: 'count', operator: 'gte', threshold: 5 },
      window: { durationSeconds: 300, stalenessSeconds: 0 },
      groupBy: [],
      settleAfterSeconds: 0,
    },
    threshold: {
      aggregation: { function: 'count', operator: 'gte', threshold: 5 },
      window: { durationSeconds: 300, stalenessSeconds: 0 },
      groupBy: [],
      settleAfterSeconds: 0,
    },
    sequence: {
      sequence: {
        steps: [
          { matchKey: 'event_start', minCount: 1, withinSeconds: 300 },
          { matchKey: 'event_finish', minCount: 1, withinSeconds: 300 },
        ],
      },
      groupBy: [],
      settleAfterSeconds: 0,
    },
    decision: { condition: cloneScenarioValue(condition), groupBy: [], settleAfterSeconds: 0 },
    'alarm-output': {
      severity: 5,
      dedup: { keyTemplate: '{ruleId}:{key}', cooldownSeconds: 300 },
      groupBy: [],
      settleAfterSeconds: 0,
    },
    'stop-output': { groupBy: [], settleAfterSeconds: 0 },
    'debug-output': {
      debug: { mode: 'complete', path: '', active: true },
      groupBy: [],
      settleAfterSeconds: 0,
    },
  };
  return { nodeType, label, icon, color, config: configs[nodeType] };
}

function filterCatalog(origin: 'user' | 'product') {
  const query = search.value.trim().toLocaleLowerCase();
  return catalog.value.filter(item =>
    item.origin === origin
    && (!query || `${item.name} ${item.scenarioId} ${item.templateId ?? ''}`.toLocaleLowerCase().includes(query)),
  );
}

function displayError(cause: any) {
  localError.value = api.error.value || cause?.message || t('alarmCenter.scenarioStudio.errors.request');
}

async function refreshCatalog() {
  localError.value = '';
  try {
    catalog.value = await api.listScenarios(true);
  } catch (cause) {
    displayError(cause);
  } finally {
    catalogLoaded.value = true;
  }
}

async function openCatalogItem(item: ScenarioCatalogItem) {
  const version = item.origin === 'product'
    ? item.latestVersion
    : item.draftVersion ?? item.publishedVersion ?? item.latestVersion;
  try {
    loadVersion(await api.getScenario(item.scenarioId, version));
  } catch (cause) {
    displayError(cause);
  }
}

async function cloneTemplate(item = current.value) {
  if (!item) return;
  try {
    const scenarioId = 'scenarioId' in item ? item.scenarioId : item.scenarioId;
    const version = 'latestVersion' in item ? item.latestVersion : item.version;
    loadVersion(await api.cloneTemplateToDraft(scenarioId, version));
    await refreshCatalog();
    message.value = t('alarmCenter.scenarioStudio.messages.cloned');
  } catch (cause) {
    displayError(cause);
  }
}

function loadVersion(version: ScenarioVersion) {
  current.value = version;
  const mapped = scenarioToVueFlow(version.definition, version.severity);
  nodes.value = mapped.nodes;
  edges.value = mapped.edges;
  hydrateSimpleSourceNodes();
  selectedNodeId.value = null;
  clearTrace();
  void nextTick(() => fitView({ padding: 0.18, duration: 250 }));
}

function newScenario() {
  current.value = null;
  const mapped = scenarioToVueFlow(createEmptyScenarioDefinitionV3(), 5);
  nodes.value = mapped.nodes;
  edges.value = mapped.edges;
  hydrateSimpleSourceNodes();
  selectedNodeId.value = null;
  clearTrace();
  message.value = '';
  localError.value = '';
  void nextTick(() => fitView({ padding: 0.2, duration: 0 }));
}

function requestBody() {
  const alarm = alarmNode.value;
  return {
    name: current.value?.name || t('alarmCenter.scenarioStudio.catalog.newScenario'),
    severity: Number(alarm?.data.config.severity ?? current.value?.severity ?? 5),
    enabled: Boolean(current.value?.enabled ?? false),
    definition: currentDefinition.value,
  };
}

async function saveDraft() {
  if (!canEdit.value) return;
  try {
    const saved = current.value
      ? await api.updateDraft(current.value.scenarioId, current.value.version, requestBody())
      : await api.createDraft(requestBody());
    loadVersion(saved);
    await refreshCatalog();
    message.value = t('alarmCenter.scenarioStudio.messages.saved');
  } catch (cause) {
    displayError(cause);
  }
}

async function validateDraft() {
  if (!current.value || current.value.isReadOnly) return;
  try {
    const validation = await api.validate(current.value.scenarioId, current.value.version);
    current.value = { ...current.value, status: validation.isValid ? 'validated' : 'draft', validation };
    message.value = t('alarmCenter.scenarioStudio.messages.validated');
  } catch (cause) {
    displayError(cause);
  }
}

async function startEditVersion() {
  if (!current.value || current.value.isReadOnly) return;
  if (current.value.status === 'draft') return;
  try {
    loadVersion(await api.createNextDraft(current.value.scenarioId, requestBody()));
    await refreshCatalog();
    message.value = t('alarmCenter.scenarioStudio.messages.nextDraft');
  } catch (cause) {
    displayError(cause);
  }
}

async function publishDraft() {
  if (!current.value) return;
  try {
    loadVersion(await api.publish(current.value.scenarioId, current.value.version));
    await refreshCatalog();
    message.value = t('alarmCenter.scenarioStudio.messages.published');
  } catch (cause) {
    displayError(cause);
  }
}

async function rollbackVersion() {
  if (!current.value) return;
  try {
    loadVersion(await api.rollback(current.value.scenarioId, current.value.version));
    await refreshCatalog();
    message.value = t('alarmCenter.scenarioStudio.messages.rolledBack');
  } catch (cause) {
    displayError(cause);
  }
}

async function loadAudit() {
  if (!current.value) return;
  try {
    auditEntries.value = await api.audit(current.value.scenarioId);
    showAudit.value = true;
  } catch (cause) {
    displayError(cause);
  }
}

async function simulate() {
  try {
    const samples = JSON.parse(simulationJson.value) as ScenarioSampleObservation[];
    if (!Array.isArray(samples)) throw new Error(t('alarmCenter.scenarioStudio.errors.samplesArray'));
    simulationResult.value = await api.simulate(
      { definition: currentDefinition.value, samples },
      current.value?.scenarioId,
      current.value?.version,
    );
    const trace = traceGraph(
      currentDefinition.value,
      simulationResult.value.nodeTrace ?? [],
      simulationResult.value.executionOrder ?? [],
    );
    traceNodeIds.value = trace.nodeIds;
    traceEdgeIds.value = trace.edgeIds;
    for (const edge of edges.value) {
      if (!traceEdgeIds.value.includes(edge.id)) continue;
      edge.animated = true;
      edge.style = { stroke: '#f2c94c', strokeWidth: 4 };
    }
    message.value = t('alarmCenter.scenarioStudio.messages.simulated');
  } catch (cause) {
    displayError(cause);
  }
}

function clearTrace() {
  traceNodeIds.value = [];
  traceEdgeIds.value = [];
  for (const edge of edges.value) {
    edge.style = undefined;
    edge.animated = !edge.sourceHandle;
  }
  simulationResult.value = null;
}

async function toggleLibrary() {
  toggleLibraryCollapse();
  await nextTick();
  void fitView({ padding: 0.18, duration: 200 });
}

async function togglePalette() {
  togglePaletteCollapse();
  await nextTick();
  void fitView({ padding: 0.18, duration: 200 });
}

function createNode(item: ReturnType<typeof nodeTemplate>, position: { x: number; y: number }): Node {
  const id = `${item.nodeType}-${crypto.randomUUID()}`;
  const simple = item.nodeType === 'source' ? defaultSimpleSourceState() : undefined;
  return {
    id,
    type: item.nodeType,
    position,
    data: {
      nodeType: item.nodeType,
      label: item.label,
      subtitle: item.nodeType === 'source'
        ? simpleSourceSubtitle(simple!, item.config.source.matchKey, t)
        : '',
      icon: item.icon,
      color: item.color,
      config: cloneScenarioValue(item.config),
      ...(simple ? { simple } : {}),
    },
  };
}

function addPaletteNode(item: ReturnType<typeof nodeTemplate>) {
  if (!canEdit.value) return;
  const node = createNode(item, { x: 120 + nodes.value.length * 25, y: 120 + nodes.value.length * 12 });
  nodes.value = [...nodes.value, node];
  selectedNodeId.value = node.id;
  if (item.nodeType === 'source' && node.data.simple) {
    syncSimpleSourceScope(
      node.id,
      node.data.simple as SimpleSourceState,
      sourceTypeForPlatform(node.data.simple.platform, node.data.simple.channel),
    );
  }
}

function startDrag(event: DragEvent, item: ReturnType<typeof nodeTemplate>) {
  event.dataTransfer?.setData('application/monitra-flow-node', JSON.stringify(item));
}

function dropNode(event: DragEvent) {
  event.preventDefault();
  if (!canEdit.value) return;
  const raw = event.dataTransfer?.getData('application/monitra-flow-node');
  if (!raw) return;
  const item = JSON.parse(raw);
  const node = createNode(item, screenToFlowCoordinate({ x: event.clientX, y: event.clientY }));
  nodes.value = [...nodes.value, node];
  selectedNodeId.value = node.id;
  if (item.nodeType === 'source' && node.data.simple) {
    syncSimpleSourceScope(
      node.id,
      node.data.simple as SimpleSourceState,
      sourceTypeForPlatform(node.data.simple.platform, node.data.simple.channel),
    );
  }
}

function onConnect(connection: Connection) {
  if (!canEdit.value) return;
  const isTrue = connection.sourceHandle === 'true';
  const isFalse = connection.sourceHandle === 'false';
  addEdges([{
    ...connection,
    id: `edge-${crypto.randomUUID()}`,
    label: isTrue ? 'True' : isFalse ? 'False' : undefined,
    type: 'smoothstep',
    markerEnd: MarkerType.ArrowClosed,
    data: {
      fromPort: connection.sourceHandle ?? 'next',
      toPort: connection.targetHandle ?? 'in',
    },
  }]);
}

function removeSelectedNode() {
  if (!selectedNodeId.value || !canEdit.value) return;
  const id = selectedNodeId.value;
  const managed = [
    managedOsFilterId(id),
    managedHostFilterId(id),
    managedEventCodeFilterId(id),
    managedMetricConditionId(id),
  ];
  removeNodes([id, ...managed]);
  selectedNodeId.value = null;
}

function addSequenceStep() {
  selectedNode.value?.data.config.sequence.steps.push({ matchKey: '', minCount: 1, withinSeconds: 300 });
}

function setGroupBy(value: unknown) {
  if (!selectedNode.value) return;
  selectedNode.value.data.config.groupBy = String(value ?? '')
    .split(',')
    .map(item => item.trim())
    .filter(Boolean);
}

function isScopeFilterNode(node: Node | undefined): boolean {
  if (!node) return false;
  if (node.data?.managedScope) return true;
  const field = String(node.data?.config?.condition?.field ?? '');
  return field === 'dimensions.sourceHost'
    || field === 'dimensions.sourceType'
    || field === 'dimensions.eventCode'
    || field === 'sourceHost'
    || field === 'sourceType'
    || field === 'eventCode';
}

function createManagedCompareNode(
  id: string,
  nodeType: 'filter' | 'condition',
  label: string,
  icon: string,
  color: string,
  field: string,
  operator: string,
  value: unknown,
  subtitle: string,
  position: { x: number; y: number },
): Node {
  return {
    id,
    type: nodeType,
    position,
    data: {
      nodeType,
      label,
      subtitle,
      icon,
      color,
      managedScope: true,
      config: {
        condition: {
          children: [],
          field,
          operator,
          value,
          sustainedForSeconds: 0,
        },
        groupBy: [],
        settleAfterSeconds: 0,
      },
    },
  };
}

function createManagedFilterNode(
  id: string,
  label: string,
  field: string,
  operator: string,
  value: unknown,
  subtitle: string,
  position: { x: number; y: number },
): Node {
  return createManagedCompareNode(
    id,
    'filter',
    label,
    'mdi-filter-outline',
    '#9c6ade',
    field,
    operator,
    value,
    subtitle,
    position,
  );
}

function parseHostFilterValue(value: unknown): string[] {
  if (Array.isArray(value)) return normalizeSimpleHosts(value);
  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (!trimmed) return [];
    if (trimmed.includes(',')) {
      return normalizeSimpleHosts(trimmed.split(','));
    }
    return [trimmed];
  }
  if (value == null) return [];
  return normalizeSimpleHosts([String(value)]);
}

function formatManagedFilterValue(value: unknown): string {
  if (Array.isArray(value)) return value.map(String).join(', ');
  if (value == null) return '';
  return String(value);
}

/** Collect host/os/metric from the scope filter chain right after a source node. */
function readScopeFromSource(sourceNodeId: string): {
  hosts: string[];
  sourceType: string | null;
  metric: SimpleMetricComparison | null;
} {
  let hosts: string[] = [];
  let sourceType: string | null = null;
  let metric: SimpleMetricComparison | null = null;
  let cursor = sourceNodeId;
  const seen = new Set<string>();

  while (!seen.has(cursor)) {
    seen.add(cursor);
    const nextEdge = edges.value.find(edge =>
      edge.source === cursor
      && (edge.sourceHandle == null || edge.sourceHandle === 'true' || edge.sourceHandle === 'next'));
    if (!nextEdge) break;
    const nextNode = nodes.value.find(node => node.id === nextEdge.target);
    if (!isScopeFilterNode(nextNode)) break;
    const field = String(nextNode?.data?.config?.condition?.field ?? '');
    const operator = String(nextNode?.data?.config?.condition?.operator ?? 'gte');
    const value = nextNode?.data?.config?.condition?.value;
    if (field.endsWith('sourceHost')) {
      hosts = parseHostFilterValue(value);
    }
    if (field.endsWith('sourceType')) {
      const text = String(value ?? '').trim();
      if (text) sourceType = text;
    }
    if (field === 'value' && nextNode?.data?.managedScope) {
      const threshold = Number(value);
      metric = {
        key: '',
        operator: (['gt', 'gte', 'lt', 'lte', 'eq', 'neq'].includes(operator)
          ? operator
          : 'gte') as SimpleMetricComparison['operator'],
        threshold: Number.isFinite(threshold) ? threshold : 90,
      };
    }
    cursor = nextEdge.target;
  }

  return { hosts, sourceType, metric };
}

function collectScopeChainIds(sourceNodeId: string): string[] {
  const ids: string[] = [];
  let cursor = sourceNodeId;
  const seen = new Set<string>();

  while (!seen.has(cursor)) {
    seen.add(cursor);
    const nextEdges = edges.value.filter(edge =>
      edge.source === cursor
      && (edge.sourceHandle == null || edge.sourceHandle === 'true' || edge.sourceHandle === 'next'));
    const scopeEdges = nextEdges.filter(edge =>
      isScopeFilterNode(nodes.value.find(node => node.id === edge.target)));
    if (!scopeEdges.length) break;
    for (const edge of scopeEdges) {
      ids.push(edge.target);
      cursor = edge.target;
    }
  }

  return ids;
}

function syncSimpleSourceScope(
  sourceNodeId: string,
  simple: SimpleSourceState,
  sourceType: string | null,
) {
  const sourceNode = nodes.value.find(node => node.id === sourceNodeId);
  if (!sourceNode) return;

  const osId = managedOsFilterId(sourceNodeId);
  const hostId = managedHostFilterId(sourceNodeId);
  const eventCodeId = managedEventCodeFilterId(sourceNodeId);
  const metricId = managedMetricConditionId(sourceNodeId);
  const hostCondition = hostFilterCondition(simple.hosts);
  const eventCondition = eventCodeFilterCondition(simple.events ?? []);
  const metricSpec = simple.channel === 'metric' ? metricConditionSpec(simple.metric) : null;
  const legacyScopeIds = collectScopeChainIds(sourceNodeId);
  const removeIds = new Set<string>([osId, hostId, eventCodeId, metricId, ...legacyScopeIds]);

  // Exit targets: from source or any scope node → non-scope node
  const exitTargets = edges.value
    .filter((edge) => {
      const fromSource = edge.source === sourceNodeId;
      const fromScope = removeIds.has(edge.source);
      if (!fromSource && !fromScope) return false;
      return !removeIds.has(edge.target);
    })
    .map(edge => ({
      target: edge.target,
      targetHandle: edge.targetHandle,
      data: edge.data,
    }));

  nodes.value = nodes.value.filter(node => !removeIds.has(node.id));
  edges.value = edges.value.filter(edge =>
    !removeIds.has(edge.source)
    && !removeIds.has(edge.target)
    && edge.source !== sourceNodeId);

  const chain: string[] = [sourceNodeId];
  const baseX = sourceNode.position.x + 260;
  const baseY = sourceNode.position.y;

  if (sourceType) {
    nodes.value = [
      ...nodes.value,
      createManagedFilterNode(
        osId,
        t('alarmCenter.scenarioStudio.simpleSource.osFilterLabel'),
        'dimensions.sourceType',
        'eq',
        sourceType,
        `sourceType = ${sourceType}`,
        { x: baseX, y: baseY - 40 },
      ),
    ];
    chain.push(osId);
  }

  if (eventCondition) {
    nodes.value = [
      ...nodes.value,
      createManagedFilterNode(
        eventCodeId,
        t('alarmCenter.scenarioStudio.eventSelector.eventCodeFilterLabel'),
        eventCondition.field,
        eventCondition.operator,
        eventCondition.value,
        eventCondition.subtitle,
        { x: baseX + (chain.length - 1) * 240, y: baseY },
      ),
    ];
    chain.push(eventCodeId);
  }

  if (hostCondition) {
    nodes.value = [
      ...nodes.value,
      createManagedFilterNode(
        hostId,
        t('alarmCenter.scenarioStudio.simpleSource.hostFilterLabel'),
        hostCondition.field,
        hostCondition.operator,
        hostCondition.value,
        hostCondition.subtitle,
        { x: baseX + (chain.length - 1) * 240, y: baseY + 40 },
      ),
    ];
    chain.push(hostId);
  }

  if (metricSpec) {
    nodes.value = [
      ...nodes.value,
      createManagedCompareNode(
        metricId,
        'condition',
        t('alarmCenter.scenarioStudio.simpleSource.metricConditionLabel'),
        'mdi-gauge',
        '#00a896',
        metricSpec.field,
        metricSpec.operator,
        metricSpec.value,
        metricSpec.subtitle,
        { x: baseX + (chain.length - 1) * 240, y: baseY + 80 },
      ),
    ];
    chain.push(metricId);
  }

  const newEdges: Edge[] = [];
  for (let i = 0; i < chain.length - 1; i += 1) {
    newEdges.push({
      id: `e-${chain[i]}-${chain[i + 1]}`,
      source: chain[i],
      target: chain[i + 1],
      sourceHandle: i === 0 ? undefined : 'true',
      type: 'smoothstep',
      markerEnd: MarkerType.ArrowClosed,
      animated: i === 0,
      data: { fromPort: i === 0 ? 'next' : 'true', toPort: 'in', managedScope: true },
    });
  }

  const tail = chain[chain.length - 1];
  const uniqueTargets = [...new Map(exitTargets.map(item => [item.target, item])).values()];
  for (const target of uniqueTargets) {
    newEdges.push({
      id: `e-${tail}-${target.target}`,
      source: tail,
      target: target.target,
      sourceHandle: tail === sourceNodeId ? undefined : 'true',
      targetHandle: target.targetHandle,
      type: 'smoothstep',
      markerEnd: MarkerType.ArrowClosed,
      animated: tail === sourceNodeId,
      data: {
        ...(target.data ?? {}),
        fromPort: tail === sourceNodeId ? 'next' : 'true',
        toPort: 'in',
      },
    });
  }

  edges.value = [...edges.value, ...newEdges];
}

function onSimpleSourceChange(payload: {
  source: any;
  simple: SimpleSourceState;
  subtitle: string;
  sourceType: string | null;
}) {
  if (!selectedNode.value || selectedNode.value.data.nodeType !== 'source') return;
  selectedNode.value.data.config.source = payload.source;
  selectedNode.value.data.simple = coerceSimpleSourceState(payload.simple);
  selectedNode.value.data.subtitle = payload.subtitle;
  selectedNode.value.data.label = selectedNode.value.data.label || t('alarmCenter.flowLab.nodes.eventSource');
  syncSimpleSourceScope(selectedNode.value.id, selectedNode.value.data.simple, payload.sourceType);
}

function hydrateSimpleSourceNodes() {
  nodes.value = nodes.value.map((node) => {
    if (node.data?.nodeType !== 'source') return node;
    const scope = readScopeFromSource(node.id);
    const hosts = scope.hosts.length
      ? scope.hosts
      : normalizeSimpleHosts(node.data.simple?.hosts ?? node.data.simple?.host ?? []);
    const inferred = node.data.simple ?? inferSimpleSourceState(node.data.config?.source, hosts);
    const matchKey = String(node.data.config?.source?.matchKey ?? '');
    let metric = inferred.metric ?? null;
    if (inferred.channel === 'metric') {
      metric = {
        key: matchKey || metric?.key || 'cpu_usage',
        operator: scope.metric?.operator || metric?.operator || 'gte',
        threshold: scope.metric?.threshold ?? metric?.threshold ?? 90,
      };
    }
    const simple = coerceSimpleSourceState({
      ...inferred,
      hosts,
      metric,
    });
    return {
      ...node,
      data: {
        ...node.data,
        simple,
        subtitle: simpleSourceSubtitle(simple, matchKey, t),
      },
    };
  });
}

onMounted(() => {
  libraryCollapsed.value = false;
  paletteCollapsed.value = false;
  newScenario();
  void refreshCatalog();
  void nextTick(() => fitView({ padding: 0.2, duration: 0 }));
});
</script>

<template>
  <div class="flow-lab-root">
    <v-alert v-if="localError" type="error" variant="tonal" closable class="mb-3" @click:close="localError = ''">
      {{ localError }}
    </v-alert>
    <v-alert v-if="message" type="success" variant="tonal" closable class="mb-3" @click:close="message = ''">
      {{ message }}
    </v-alert>

    <div class="flow-lab">
      <aside
        v-if="!libraryCollapsed"
        class="flow-library"
        :style="{ width: `${libraryWidth}px`, flex: `0 0 ${libraryWidth}px` }"
      >
        <div class="panel-header">
          <strong>{{ t('alarmCenter.scenarioStudio.catalog.title') }}</strong>
          <div class="panel-header__actions">
            <v-btn icon="mdi-refresh" size="x-small" variant="text" :loading="api.pending.value" @click="refreshCatalog" />
            <v-btn
              icon="mdi-chevron-left"
              size="x-small"
              variant="text"
              :title="t('alarmCenter.flowLab.collapseLibrary')"
              @click="toggleLibrary"
            />
          </div>
        </div>
        <div class="panel-search">
          <v-text-field
            v-model="search"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            prepend-inner-icon="mdi-magnify"
            :placeholder="t('alarmCenter.scenarioStudio.catalog.search')"
          />
        </div>
        <div class="panel-body">
          <div v-if="api.pending.value && !catalogLoaded" class="panel-loading">
            <v-progress-circular indeterminate size="28" />
          </div>
          <div v-else class="catalog-scroll">
            <AcScenarioCatalogTree
              :product-items="productTemplates"
              :user-items="userScenarios"
              :active-scenario-id="current?.scenarioId ?? null"
              :catalog-loaded="catalogLoaded"
              @open="openCatalogItem"
              @clone="cloneTemplate"
              @create-scenario="newScenario"
            />
          </div>
        </div>
      </aside>

      <aside v-if="!paletteCollapsed" class="node-library">
        <div class="panel-header">
          <strong>{{ t('alarmCenter.flowLab.paletteTitle') }}</strong>
          <v-btn
            icon="mdi-chevron-left"
            size="x-small"
            variant="text"
            :title="t('alarmCenter.flowLab.collapseNodeList')"
            @click="togglePalette"
          />
        </div>
        <div class="node-library-scroll">
          <div v-for="group in paletteGroups" :key="group.id" class="palette-group">
            <button
              type="button"
              class="palette-group__header"
              :aria-expanded="!paletteGroupCollapsed[group.id]"
              @click="togglePaletteGroup(group.id)"
            >
              <v-icon
                :icon="paletteGroupCollapsed[group.id] ? 'mdi-chevron-right' : 'mdi-chevron-down'"
                size="18"
              />
              <strong>{{ group.title }}</strong>
            </button>
            <div v-show="!paletteGroupCollapsed[group.id]" class="palette-group__items">
              <button
                v-for="item in group.items"
                :key="item.nodeType"
                type="button"
                class="palette-card"
                draggable="true"
                :disabled="!canEdit"
                @dragstart="startDrag($event, item)"
                @dblclick="addPaletteNode(item)"
              >
                <span :style="{ backgroundColor: item.color }"><v-icon :icon="item.icon" size="18" /></span>
                <strong>{{ item.label }}</strong>
              </button>
            </div>
          </div>
        </div>
      </aside>

      <section class="flow-context">
        <header class="flow-toolbar">
          <v-btn v-if="libraryCollapsed" icon="mdi-folder-multiple-outline" size="small" variant="text"
            :title="t('alarmCenter.flowLab.expandLibrary')" @click="toggleLibrary" />
          <v-btn v-if="paletteCollapsed" icon="mdi-shape-outline" size="small" variant="text"
            :title="t('alarmCenter.flowLab.expandNodeList')" @click="togglePalette" />
          <div class="flow-name">
            <strong>{{ current?.name || t('alarmCenter.scenarioStudio.catalog.newScenario') }}</strong>
            <small v-if="current">v{{ current.version }} · {{ current.status }}</small>
          </div>
          <v-chip v-if="current?.isReadOnly" size="small" prepend-icon="mdi-lock">
            {{ t('alarmCenter.scenarioStudio.catalog.readOnly') }}
          </v-chip>
          <v-spacer />
          <v-btn
            v-if="canStartEdit"
            size="small"
            color="primary"
            variant="tonal"
            prepend-icon="mdi-pencil"
            @click="startEditVersion"
          >
            {{ t('alarmCenter.scenarioStudio.editDraft') }}
          </v-btn>
          <v-btn v-if="current?.isReadOnly" size="small" color="primary" variant="tonal"
            prepend-icon="mdi-content-copy" @click="cloneTemplate()">
            {{ t('alarmCenter.scenarioStudio.catalog.clone') }}
          </v-btn>
          <v-btn size="small" variant="text" prepend-icon="mdi-form-select" @click="showWizard = true">
            {{ t('alarmCenter.scenarioStudio.modeWizard') }}
          </v-btn>
          <v-btn size="small" variant="text" prepend-icon="mdi-fit-to-screen-outline"
            @click="fitView({ padding: 0.18, duration: 250 })">
            {{ t('alarmCenter.flowLab.fit') }}
          </v-btn>
          <v-btn size="small" variant="text" prepend-icon="mdi-flask-outline"
            @click="showSimulation = !showSimulation">
            {{ t('alarmCenter.scenarioStudio.simulationTitle') }}
          </v-btn>
          <v-menu>
            <template #activator="{ props }">
              <v-btn v-bind="props" size="small" variant="tonal" prepend-icon="mdi-dots-horizontal">
                {{ t('alarmCenter.scenarioStudio.actions') }}
              </v-btn>
            </template>
            <v-list density="compact">
              <v-list-item
                v-if="canStartEdit"
                prepend-icon="mdi-pencil"
                :title="t('alarmCenter.scenarioStudio.editDraft')"
                @click="startEditVersion"
              />
              <v-list-item prepend-icon="mdi-content-save" :disabled="!canEdit" :title="t('alarmCenter.scenarioStudio.saveDraft')" @click="saveDraft" />
              <v-list-item prepend-icon="mdi-check-decagram" :disabled="!current || current.isReadOnly" :title="t('alarmCenter.scenarioStudio.validate')" @click="validateDraft" />
              <v-list-item prepend-icon="mdi-rocket-launch" :disabled="!canPublish" :title="t('alarmCenter.scenarioStudio.publish')" @click="publishDraft" />
              <v-list-item prepend-icon="mdi-restore" :disabled="current?.status !== 'published'" :title="t('alarmCenter.scenarioStudio.rollback')" @click="rollbackVersion" />
              <v-list-item prepend-icon="mdi-history" :disabled="!current" :title="t('alarmCenter.scenarioStudio.audit')" @click="loadAudit" />
            </v-list>
          </v-menu>
        </header>

        <main class="flow-canvas" @dragover.prevent @drop="dropNode">
          <VueFlow
            class="flow-vue"
            :nodes="nodes"
            :edges="edges"
            :nodes-draggable="canEdit"
            :nodes-connectable="canEdit"
            fit-view-on-init
            snap-to-grid
            :snap-grid="[16, 16]"
            @update:nodes="nodes = $event"
            @update:edges="edges = $event"
            @connect="onConnect"
            @node-click="selectedNodeId = $event.node.id"
            @pane-click="selectedNodeId = null"
          >
            <Background pattern-color="rgba(120,130,150,.22)" :gap="20" />
            <Controls position="bottom-left" />
            <MiniMap
              position="bottom-right"
              pannable
              zoomable
              mask-color="rgba(10, 14, 22, 0.72)"
              node-color="#4b5568"
              node-stroke-color="#94a3b8"
            />
            <template v-for="nodeType in ['source','condition','filter','aggregation','threshold','sequence','decision','alarm-output','stop-output','debug-output']"
              #[`node-${nodeType}`]="{ data, selected, id }">
              <div :key="nodeType" class="flow-node"
                :class="{
                  selected,
                  traced: traceNodeIds.includes(id),
                  decision: !['source','alarm-output','stop-output','debug-output'].includes(nodeType),
                  managed: Boolean(data.managedScope),
                }"
                :style="{ '--node-color': data.color }">
                <Handle v-if="nodeType !== 'source'" type="target" :position="Position.Left" />
                <span class="accent" />
                <v-icon :icon="data.icon" />
                <span><strong>{{ data.label }}</strong><small>{{ data.subtitle }}</small></span>
                <template v-if="!['source','alarm-output','stop-output','debug-output'].includes(nodeType)">
                  <Handle id="true" type="source" :position="Position.Right" :style="{ top: '32%', background: '#24a148' }" />
                  <Handle id="false" type="source" :position="Position.Right" :style="{ top: '72%', background: '#d64545' }" />
                </template>
                <Handle v-else-if="nodeType === 'source'" type="source" :position="Position.Right" />
              </div>
            </template>
          </VueFlow>

          <aside v-if="selectedNode" class="node-inspector">
            <div class="d-flex align-center mb-3">
              <strong>{{ t('alarmCenter.flowLab.propertiesTitle') }}</strong><v-spacer />
              <v-btn icon="mdi-close" size="x-small" variant="text" @click="selectedNodeId = null" />
            </div>
            <v-text-field v-model="selectedNode.data.label" :disabled="!canEdit"
              :label="t('alarmCenter.flowLab.fields.label')" density="compact" />
            <template v-if="selectedNode.data.nodeType === 'source'">
              <AcScenarioSourceInspector
                :source="selectedNode.data.config.source"
                :simple="selectedNode.data.simple ?? null"
                :disabled="!canEdit"
                @change="onSimpleSourceChange"
              />
            </template>
            <template v-else-if="selectedNode.data.managedScope">
              <v-alert type="info" variant="tonal" density="compact" class="mb-3">
                {{ t('alarmCenter.scenarioStudio.simpleSource.managedFilterHint') }}
              </v-alert>
              <v-text-field
                :model-value="selectedNode.data.config.condition.field"
                :label="t('alarmCenter.scenarioStudio.field')"
                density="compact"
                disabled
              />
              <v-text-field
                :model-value="formatManagedFilterValue(selectedNode.data.config.condition.value)"
                :label="t('alarmCenter.scenarioStudio.value')"
                density="compact"
                disabled
              />
              <v-text-field
                :model-value="selectedNode.data.config.condition.operator"
                :label="t('alarmCenter.scenarioStudio.operator')"
                density="compact"
                disabled
                class="mt-2"
              />
            </template>
            <template v-else-if="['condition','filter','decision'].includes(selectedNode.data.nodeType)">
              <v-text-field v-model="selectedNode.data.config.condition.field" :disabled="!canEdit"
                :label="t('alarmCenter.scenarioStudio.field')" density="compact" />
              <v-select v-model="selectedNode.data.config.condition.operator" :disabled="!canEdit"
                :items="['eq','neq','gt','gte','lt','lte','contains','exists']"
                :label="t('alarmCenter.scenarioStudio.operator')" density="compact" />
              <v-text-field v-model="selectedNode.data.config.condition.value" :disabled="!canEdit"
                :label="t('alarmCenter.scenarioStudio.value')" density="compact" />
              <v-text-field v-model.number="selectedNode.data.config.condition.sustainedForSeconds" type="number"
                :disabled="!canEdit" :label="t('alarmCenter.scenarioStudio.sustainedSeconds')" density="compact" />
            </template>
            <template v-if="['aggregation','threshold'].includes(selectedNode.data.nodeType)">
              <v-select v-model="selectedNode.data.config.aggregation.function" :disabled="!canEdit"
                :items="['count','sum','avg','min','max']" :label="t('alarmCenter.scenarioStudio.aggregation')" density="compact" />
              <v-text-field v-model="selectedNode.data.config.aggregation.field" :disabled="!canEdit"
                :label="t('alarmCenter.scenarioStudio.field')" density="compact" />
              <v-select v-model="selectedNode.data.config.aggregation.operator" :disabled="!canEdit"
                :items="['eq','neq','gt','gte','lt','lte']" :label="t('alarmCenter.scenarioStudio.operator')" density="compact" />
              <v-text-field v-model.number="selectedNode.data.config.aggregation.threshold" type="number" :disabled="!canEdit"
                :label="t('alarmCenter.scenarioStudio.count')" density="compact" />
              <v-text-field :model-value="selectedNode.data.config.window.durationSeconds / 60" type="number" :disabled="!canEdit"
                :label="t('alarmCenter.scenarioStudio.windowMinutes')" density="compact"
                @update:model-value="selectedNode.data.config.window.durationSeconds = Number($event) * 60" />
              <v-text-field :model-value="selectedNode.data.config.groupBy.join(', ')" :disabled="!canEdit"
                :label="t('alarmCenter.scenarioStudio.groupBy')" density="compact" @update:model-value="setGroupBy" />
              <v-text-field v-model.number="selectedNode.data.config.settleAfterSeconds" type="number" min="0"
                :disabled="!canEdit" :label="t('alarmCenter.scenarioStudio.settleAfterSeconds')" density="compact" />
            </template>
            <template v-if="selectedNode.data.nodeType === 'sequence'">
              <div v-for="(step, index) in selectedNode.data.config.sequence.steps" :key="index" class="sequence-step">
                <v-text-field v-model="step.matchKey" :disabled="!canEdit" :label="`${t('alarmCenter.scenarioStudio.sequenceStep')} ${index + 1}`" density="compact" />
                <v-text-field v-model.number="step.minCount" type="number" min="1" :disabled="!canEdit" :label="t('alarmCenter.scenarioStudio.count')" density="compact" />
                <v-text-field :model-value="step.withinSeconds / 60" type="number" :disabled="!canEdit"
                  :label="t('alarmCenter.scenarioStudio.windowMinutes')" density="compact"
                  @update:model-value="step.withinSeconds = Number($event) * 60" />
              </div>
              <v-btn size="small" block variant="tonal" :disabled="!canEdit" @click="addSequenceStep">
                {{ t('alarmCenter.scenarioStudio.addStep') }}
              </v-btn>
              <v-text-field :model-value="selectedNode.data.config.groupBy.join(', ')" :disabled="!canEdit"
                :label="t('alarmCenter.scenarioStudio.groupBy')" density="compact" @update:model-value="setGroupBy" />
              <v-text-field v-model.number="selectedNode.data.config.settleAfterSeconds" type="number" min="0"
                :disabled="!canEdit" :label="t('alarmCenter.scenarioStudio.settleAfterSeconds')" density="compact" />
            </template>
            <template v-if="selectedNode.data.nodeType === 'alarm-output'">
              <v-text-field v-model.number="selectedNode.data.config.severity" type="number" min="1" max="10"
                :disabled="!canEdit" :label="t('alarmCenter.scenarioStudio.severity')" density="compact" />
              <v-text-field v-model="selectedNode.data.config.dedup.keyTemplate" :disabled="!canEdit"
                :label="t('alarmCenter.scenarioStudio.dedupTemplate')" density="compact" />
              <v-text-field :model-value="selectedNode.data.config.dedup.cooldownSeconds / 60" type="number"
                :disabled="!canEdit" :label="t('alarmCenter.scenarioStudio.cooldownMinutes')" density="compact"
                @update:model-value="selectedNode.data.config.dedup.cooldownSeconds = Number($event) * 60" />
              <v-text-field :model-value="selectedNode.data.config.groupBy.join(', ')" :disabled="!canEdit"
                :label="t('alarmCenter.scenarioStudio.groupBy')" density="compact" @update:model-value="setGroupBy" />
            </template>
            <template v-if="selectedNode.data.nodeType === 'debug-output'">
              <v-alert type="info" variant="tonal" density="compact" class="mb-3">
                {{ t('alarmCenter.scenarioStudio.debug.simOnlyHint') }}
              </v-alert>
              <v-switch
                v-model="selectedNode.data.config.debug.active"
                :disabled="!canEdit"
                density="compact"
                color="primary"
                hide-details
                class="mb-2"
                :label="t('alarmCenter.scenarioStudio.debug.active')"
                @update:model-value="onDebugConfigChange"
              />
              <v-select
                v-model="selectedNode.data.config.debug.mode"
                :disabled="!canEdit"
                :items="debugModeItems"
                item-title="title"
                item-value="value"
                :label="t('alarmCenter.scenarioStudio.debug.mode')"
                density="compact"
                @update:model-value="onDebugConfigChange"
              />
              <v-text-field
                v-if="selectedNode.data.config.debug.mode === 'path'"
                v-model="selectedNode.data.config.debug.path"
                :disabled="!canEdit"
                :label="t('alarmCenter.scenarioStudio.debug.path')"
                :hint="t('alarmCenter.scenarioStudio.debug.pathHint')"
                persistent-hint
                density="compact"
                @update:model-value="onDebugConfigChange"
              />
            </template>
            <v-btn block color="error" variant="tonal" prepend-icon="mdi-delete" :disabled="!canEdit" @click="removeSelectedNode">
              {{ t('alarmCenter.flowLab.deleteNode') }}
            </v-btn>
          </aside>

          <aside v-if="showSimulation" class="simulation-panel">
            <div class="d-flex align-center">
              <strong>{{ t('alarmCenter.scenarioStudio.simulationTitle') }}</strong><v-spacer />
              <v-btn icon="mdi-close" size="x-small" variant="text"
                @click="showSimulation = false; clearTrace()" />
            </div>
            <v-textarea v-model="simulationJson" rows="3" density="compact" hide-details class="mt-2" />
            <v-btn block color="primary" size="small" class="mt-2" prepend-icon="mdi-play" @click="simulate">
              {{ t('alarmCenter.scenarioStudio.simulate') }}
            </v-btn>
            <small v-if="simulationResult">
              {{ simulationResult.matches.filter(item => item.matched).length }} / {{ simulationResult.matches.length }}
              {{ t('alarmCenter.scenarioStudio.matched') }}
            </small>
            <div v-if="debugLines.length" class="debug-sidebar mt-3">
              <strong>{{ t('alarmCenter.scenarioStudio.debug.panelTitle') }}</strong>
              <div
                v-for="(line, index) in debugLines"
                :key="`${line.nodeId}-${line.sampleIndex}-${index}`"
                class="debug-line"
              >
                <div class="debug-line__meta">
                  <span>{{ line.label || line.nodeId }}</span>
                  <small>
                    #{{ line.sampleIndex + 1 }}
                    · {{ line.mode === 'path' ? (line.path || 'path') : 'complete' }}
                  </small>
                </div>
                <pre>{{ formatDebugPayload(line.payload) }}</pre>
              </div>
            </div>
            <small v-else-if="simulationResult" class="text-medium-emphasis d-block mt-2">
              {{ t('alarmCenter.scenarioStudio.debug.empty') }}
            </small>
          </aside>
        </main>
      </section>
    </div>

    <v-navigation-drawer v-model="showWizard" location="right" temporary width="420">
      <v-toolbar :title="t('alarmCenter.scenarioStudio.modeWizard')" density="compact" />
      <div class="pa-4">
        <v-alert v-if="!wizardCompatible" type="warning" variant="tonal">
          {{ t('alarmCenter.scenarioStudio.errors.notSimple') }}
        </v-alert>
        <template v-else>
          <v-text-field v-if="sourceNode" v-model="sourceNode.data.config.source.matchKey" :disabled="!canEdit"
            :label="t('alarmCenter.scenarioStudio.matchKey')" />
          <template v-if="aggregationNode">
            <v-text-field v-model.number="aggregationNode.data.config.aggregation.threshold" type="number"
              :disabled="!canEdit" :label="t('alarmCenter.scenarioStudio.count')" />
            <v-text-field :model-value="aggregationNode.data.config.window.durationSeconds / 60" type="number"
              :disabled="!canEdit" :label="t('alarmCenter.scenarioStudio.windowMinutes')"
              @update:model-value="aggregationNode.data.config.window.durationSeconds = Number($event) * 60" />
          </template>
          <template v-if="alarmNode">
            <v-slider v-model="alarmNode.data.config.severity" min="1" max="10" step="1"
              :disabled="!canEdit" thumb-label />
            <v-text-field :model-value="alarmNode.data.config.dedup.cooldownSeconds / 60" type="number"
              :disabled="!canEdit" :label="t('alarmCenter.scenarioStudio.cooldownMinutes')"
              @update:model-value="alarmNode.data.config.dedup.cooldownSeconds = Number($event) * 60" />
          </template>
        </template>
      </div>
    </v-navigation-drawer>

    <v-dialog v-model="showAudit" max-width="680">
      <v-card>
        <v-card-title>{{ t('alarmCenter.scenarioStudio.auditTitle') }}</v-card-title>
        <v-list>
          <v-list-item v-for="entry in auditEntries" :key="entry.id"
            :title="`${entry.action} · v${entry.version}`"
            :subtitle="new Date(entry.timestamp).toLocaleString()" />
          <v-list-item v-if="!auditEntries.length" :title="t('alarmCenter.scenarioStudio.auditEmpty')" />
        </v-list>
      </v-card>
    </v-dialog>
  </div>
</template>

<style>
@import '@vue-flow/core/dist/style.css';
@import '@vue-flow/core/dist/theme-default.css';
@import '@vue-flow/controls/dist/style.css';
@import '@vue-flow/minimap/dist/style.css';
</style>

<style scoped>
.flow-lab-root {
  width: 100%;
  min-width: 0;
}

.flow-lab {
  display: flex;
  width: 100%;
  height: calc(100vh - 220px);
  min-height: 580px;
  overflow: hidden;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px;
  background: rgb(var(--v-theme-surface));
}

.flow-library,
.node-library {
  display: grid;
  grid-template-rows: auto auto minmax(0, 1fr);
  height: 100%;
  min-height: 0;
  min-width: 0;
  overflow: hidden;
  border-right: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgb(var(--v-theme-surface));
}

.node-library {
  flex: 0 0 220px;
  width: 220px;
  grid-template-rows: auto minmax(0, 1fr);
}

.panel-header,
.panel-search {
  min-width: 0;
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  min-height: 48px;
  padding: 8px 10px;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.panel-header__actions {
  display: flex;
  align-items: center;
  gap: 2px;
}

.panel-search {
  padding: 8px 10px 4px;
}

.panel-search :deep(.v-input),
.panel-search :deep(.v-field) {
  flex: none;
}

.panel-body {
  min-height: 0;
  min-width: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.panel-loading {
  flex: 1;
  display: grid;
  place-items: center;
}

.catalog-scroll,
.node-library-scroll {
  min-height: 0;
  height: 100%;
  overflow-x: hidden;
  overflow-y: auto;
  padding: 6px 8px;
}

.palette-group {
  margin-bottom: 6px;
}

.palette-group__header {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 4px;
  margin-bottom: 4px;
  padding: 6px 4px;
  border: 0;
  border-radius: 6px;
  background: transparent;
  color: inherit;
  text-align: left;
  cursor: pointer;
}

.palette-group__header:hover {
  background: rgba(var(--v-theme-on-surface), 0.04);
}

.palette-group__header strong {
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 0.02em;
  text-transform: uppercase;
  opacity: 0.72;
}

.palette-group__items {
  padding-left: 2px;
}

.palette-card {
  width: 100%;
  display: grid;
  grid-template-columns: 34px 1fr;
  align-items: center;
  gap: 8px;
  margin-bottom: 7px;
  padding: 8px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
  background: transparent;
  color: inherit;
  text-align: left;
  cursor: grab;
}

.palette-card:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.palette-card span {
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  border-radius: 7px;
  color: white;
}

.palette-card strong {
  font-size: 0.75rem;
}

.flow-context {
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  min-width: 0;
  min-height: 0;
  overflow: hidden;
}

.flow-toolbar {
  display: flex;
  flex: 0 0 auto;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  min-height: 52px;
  padding: 8px 12px;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.flow-name {
  display: grid;
  min-width: 140px;
}

.flow-name small {
  font-size: 0.65rem;
  opacity: 0.6;
}

.flow-canvas {
  position: relative;
  flex: 1 1 auto;
  min-width: 0;
  min-height: 0;
  overflow: hidden;
  background: rgb(var(--v-theme-background));
}

.flow-vue,
.flow-canvas :deep(.vue-flow) {
  position: absolute !important;
  inset: 0;
  width: 100% !important;
  height: 100% !important;
}

.flow-canvas :deep(.vue-flow__minimap) {
  background: #111827;
  border: 1px solid rgba(148, 163, 184, 0.35);
  border-radius: 8px;
  overflow: hidden;
}

.flow-node {
  --node-color: #2f80ed;
  width: 220px;
  min-height: 74px;
  display: grid;
  grid-template-columns: 7px 42px 1fr;
  align-items: center;
  overflow: visible;
  border-radius: 11px;
  background: rgb(var(--v-theme-surface));
  box-shadow: 0 8px 24px rgba(18, 28, 45, 0.17);
}

.flow-node .accent {
  height: 100%;
  border-radius: 11px 0 0 11px;
  background: var(--node-color);
}

.flow-node > i {
  color: var(--node-color);
}

.flow-node > span:last-of-type {
  min-width: 0;
  display: grid;
  padding-right: 12px;
}

.flow-node strong,
.flow-node small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.flow-node small {
  font-size: 0.65rem;
  opacity: 0.6;
}

.flow-node.selected {
  outline: 3px solid color-mix(in srgb, var(--node-color) 30%, transparent);
}

.flow-node.traced {
  outline: 4px solid #f2c94c;
  box-shadow: 0 0 22px rgba(242, 201, 76, 0.8);
}

.flow-node.managed {
  opacity: 0.88;
  border-style: dashed;
}

.node-inspector,
.simulation-panel {
  position: absolute;
  z-index: 10;
  right: 12px;
  width: min(290px, calc(100% - 24px));
  padding: 12px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 10px;
  background: rgb(var(--v-theme-surface));
  box-shadow: 0 10px 30px rgba(20, 30, 55, 0.2);
}

.node-inspector {
  top: 12px;
  bottom: 12px;
  overflow: auto;
}

.simulation-panel {
  left: 12px;
  right: auto;
  bottom: 12px;
  width: min(360px, calc(100% - 24px));
  max-height: min(420px, calc(100% - 24px));
  overflow: auto;
}

.simulation-panel small {
  display: block;
  margin-top: 6px;
}

.debug-sidebar {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.debug-sidebar > strong {
  font-size: 0.8rem;
}

.debug-line {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
  padding: 8px;
  background: rgba(var(--v-theme-on-surface), 0.03);
}

.debug-line__meta {
  display: flex;
  flex-direction: column;
  gap: 2px;
  margin-bottom: 6px;
  font-size: 0.75rem;
  font-weight: 600;
}

.debug-line__meta small {
  margin-top: 0;
  font-weight: 400;
  opacity: 0.7;
}

.debug-line pre {
  margin: 0;
  max-height: 140px;
  overflow: auto;
  font-size: 0.7rem;
  white-space: pre-wrap;
  word-break: break-word;
}

.sequence-step {
  padding: 8px;
  margin-bottom: 8px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
}

:deep(.vue-flow__edge-path) {
  stroke-width: 2;
}

:deep(.vue-flow__edge.selected .vue-flow__edge-path) {
  stroke: #f2c94c;
  stroke-width: 4;
}

@media (max-width: 1100px) {
  .flow-toolbar {
    gap: 4px;
    padding: 6px 8px;
  }

  .flow-toolbar .v-btn {
    min-width: 36px;
  }

  .flow-toolbar .v-btn :deep(.v-btn__content) {
    font-size: 0;
  }

  .flow-node {
    width: 200px;
  }
}

@media (max-width: 760px) {
  .flow-lab {
    min-height: 520px;
  }

  .node-library {
    display: none;
  }

  .flow-name {
    max-width: 180px;
  }

  .node-inspector {
    top: 8px;
    right: 8px;
    bottom: 8px;
  }
}
</style>
