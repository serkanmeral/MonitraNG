<script setup lang="ts">
import { computed, nextTick, ref } from 'vue';
import {
  Handle,
  MarkerType,
  Position,
  VueFlow,
  useVueFlow,
  type Connection,
  type Node,
} from '@vue-flow/core';
import { Background } from '@vue-flow/background';
import { Controls } from '@vue-flow/controls';
import { MiniMap } from '@vue-flow/minimap';
import { useAppI18n } from '@/composables/useAppI18n';

type FlowNodeKind = 'source' | 'filter' | 'logic' | 'aggregate' | 'sequence' | 'output';
type FlowVisualStyle = 'node-red' | 'cards' | 'minimal';

interface FlowNodeData {
  label: string;
  subtitle: string;
  icon: string;
  color: string;
  kind: FlowNodeKind;
  expression?: string;
}

interface PaletteItem extends FlowNodeData {
  description: string;
}

const { t } = useAppI18n();
const { addEdges, addNodes, fitView, screenToFlowCoordinate } = useVueFlow();

const visualStyle = ref<FlowVisualStyle>('node-red');
const search = ref('');
const selectedNodeId = ref<string | null>(null);
const nodeCounter = ref(10);

const palette = computed<PaletteItem[]>(() => [
  {
    kind: 'source',
    label: t('alarmCenter.flowLab.nodes.eventSource'),
    subtitle: t('alarmCenter.flowLab.nodes.eventSourceSubtitle'),
    description: t('alarmCenter.flowLab.nodes.eventSourceDescription'),
    icon: 'mdi-database-arrow-right-outline',
    color: '#2f80ed',
  },
  {
    kind: 'filter',
    label: t('alarmCenter.flowLab.nodes.filter'),
    subtitle: t('alarmCenter.flowLab.nodes.filterSubtitle'),
    description: t('alarmCenter.flowLab.nodes.filterDescription'),
    icon: 'mdi-filter-outline',
    color: '#8e44ad',
  },
  {
    kind: 'logic',
    label: t('alarmCenter.flowLab.nodes.logic'),
    subtitle: t('alarmCenter.flowLab.nodes.logicSubtitle'),
    description: t('alarmCenter.flowLab.nodes.logicDescription'),
    icon: 'mdi-source-branch',
    color: '#f2994a',
  },
  {
    kind: 'aggregate',
    label: t('alarmCenter.flowLab.nodes.aggregate'),
    subtitle: t('alarmCenter.flowLab.nodes.aggregateSubtitle'),
    description: t('alarmCenter.flowLab.nodes.aggregateDescription'),
    icon: 'mdi-chart-timeline-variant',
    color: '#00a896',
  },
  {
    kind: 'sequence',
    label: t('alarmCenter.flowLab.nodes.sequence'),
    subtitle: t('alarmCenter.flowLab.nodes.sequenceSubtitle'),
    description: t('alarmCenter.flowLab.nodes.sequenceDescription'),
    icon: 'mdi-format-list-numbered',
    color: '#5b5f97',
  },
  {
    kind: 'output',
    label: t('alarmCenter.flowLab.nodes.alarm'),
    subtitle: t('alarmCenter.flowLab.nodes.alarmSubtitle'),
    description: t('alarmCenter.flowLab.nodes.alarmDescription'),
    icon: 'mdi-bell-ring-outline',
    color: '#d64545',
  },
]);

const filteredPalette = computed(() => {
  const query = search.value.trim().toLocaleLowerCase();
  if (!query) return palette.value;
  return palette.value.filter(item =>
    `${item.label} ${item.subtitle} ${item.description}`.toLocaleLowerCase().includes(query),
  );
});

const nodes = ref<any[]>([
  {
    id: 'source-1',
    type: 'source',
    position: { x: 60, y: 190 },
    data: {
      kind: 'source',
      label: 'FortiGate Events',
      subtitle: 'event · denied_flow',
      expression: 'matchKey = denied_flow',
      icon: 'mdi-shield-search-outline',
      color: '#2f80ed',
    },
  },
  {
    id: 'filter-2',
    type: 'filter',
    position: { x: 330, y: 190 },
    data: {
      kind: 'filter',
      label: t('alarmCenter.flowLab.sample.filterLabel'),
      subtitle: 'dstIp exists',
      expression: 'dimensions.dstIp exists',
      icon: 'mdi-filter-outline',
      color: '#8e44ad',
    },
  },
  {
    id: 'aggregate-3',
    type: 'aggregate',
    position: { x: 600, y: 190 },
    data: {
      kind: 'aggregate',
      label: t('alarmCenter.flowLab.sample.aggregateLabel'),
      subtitle: 'count ≥ 50 · 5 min',
      expression: 'count >= 50 / 5m / groupBy(dstIp)',
      icon: 'mdi-chart-timeline-variant',
      color: '#00a896',
    },
  },
  {
    id: 'output-4',
    type: 'output',
    position: { x: 870, y: 190 },
    data: {
      kind: 'output',
      label: t('alarmCenter.flowLab.sample.outputLabel'),
      subtitle: t('alarmCenter.flowLab.sample.outputSubtitle'),
      expression: 'severity = 7',
      icon: 'mdi-bell-ring-outline',
      color: '#d64545',
    },
  },
]);

const edges = ref<any[]>([
  {
    id: 'e-source-filter',
    source: 'source-1',
    target: 'filter-2',
    type: 'smoothstep',
    animated: true,
    markerEnd: MarkerType.ArrowClosed,
  },
  {
    id: 'e-filter-aggregate',
    source: 'filter-2',
    target: 'aggregate-3',
    type: 'smoothstep',
    animated: true,
    markerEnd: MarkerType.ArrowClosed,
  },
  {
    id: 'e-aggregate-output',
    source: 'aggregate-3',
    target: 'output-4',
    type: 'smoothstep',
    animated: true,
    markerEnd: MarkerType.ArrowClosed,
  },
]);

const selectedNode = computed<any>(() =>
  nodes.value.find(node => node.id === selectedNodeId.value) ?? null,
);

function onConnect(connection: Connection) {
  addEdges([{
    ...connection,
    id: `edge-${Date.now()}`,
    type: 'smoothstep',
    animated: true,
    markerEnd: MarkerType.ArrowClosed,
  }]);
}

function createNode(item: PaletteItem, position: { x: number; y: number }): Node {
  nodeCounter.value += 1;
  return {
    id: `${item.kind}-${nodeCounter.value}`,
    type: item.kind,
    position,
    data: {
      kind: item.kind,
      label: item.label,
      subtitle: item.subtitle,
      expression: item.subtitle,
      icon: item.icon,
      color: item.color,
    },
  };
}

function addPaletteNode(item: PaletteItem) {
  const offset = nodes.value.length * 24;
  const node = createNode(item, { x: 160 + offset, y: 110 + offset });
  addNodes([node]);
  selectedNodeId.value = node.id;
}

function startDrag(event: DragEvent, item: PaletteItem) {
  if (!event.dataTransfer) return;
  event.dataTransfer.setData('application/monitra-flow-node', JSON.stringify(item));
  event.dataTransfer.effectAllowed = 'move';
}

function allowDrop(event: DragEvent) {
  event.preventDefault();
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
}

function dropNode(event: DragEvent) {
  event.preventDefault();
  const raw = event.dataTransfer?.getData('application/monitra-flow-node');
  if (!raw) return;

  const item = JSON.parse(raw) as PaletteItem;
  const position = screenToFlowCoordinate({ x: event.clientX, y: event.clientY });
  const node = createNode(item, position);
  addNodes([node]);
  selectedNodeId.value = node.id;
}

function selectNode(payload: { node: Node }) {
  selectedNodeId.value = payload.node.id;
}

function clearSelection() {
  selectedNodeId.value = null;
}

function removeSelectedNode() {
  if (!selectedNodeId.value) return;
  const id = selectedNodeId.value;
  nodes.value = nodes.value.filter(node => node.id !== id);
  edges.value = edges.value.filter(edge => edge.source !== id && edge.target !== id);
  selectedNodeId.value = null;
}

function resetExample() {
  window.location.reload();
}

async function centerFlow() {
  await nextTick();
  void fitView({ padding: 0.18, duration: 300 });
}
</script>

<template>
  <div class="flow-lab" :class="`flow-lab--${visualStyle}`">
    <div class="flow-lab__toolbar">
      <div>
        <div class="text-subtitle-1 font-weight-bold">{{ t('alarmCenter.flowLab.prototypeTitle') }}</div>
        <div class="text-caption text-medium-emphasis">{{ t('alarmCenter.flowLab.prototypeSubtitle') }}</div>
      </div>

      <v-btn-toggle v-model="visualStyle" mandatory density="compact" color="primary" variant="outlined">
        <v-btn value="node-red" size="small">{{ t('alarmCenter.flowLab.styles.nodeRed') }}</v-btn>
        <v-btn value="cards" size="small">{{ t('alarmCenter.flowLab.styles.cards') }}</v-btn>
        <v-btn value="minimal" size="small">{{ t('alarmCenter.flowLab.styles.minimal') }}</v-btn>
      </v-btn-toggle>

      <v-spacer />

      <v-btn size="small" variant="text" prepend-icon="mdi-fit-to-screen-outline" @click="centerFlow">
        {{ t('alarmCenter.flowLab.fit') }}
      </v-btn>
      <v-btn size="small" variant="text" prepend-icon="mdi-restore" @click="resetExample">
        {{ t('alarmCenter.flowLab.reset') }}
      </v-btn>
    </div>

    <div class="flow-lab__workspace">
      <aside class="flow-lab__palette">
        <div class="flow-lab__panel-title">{{ t('alarmCenter.flowLab.paletteTitle') }}</div>
        <v-text-field
          v-model="search"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          :placeholder="t('alarmCenter.flowLab.searchPlaceholder')"
          class="mb-3"
        />

        <div class="flow-lab__palette-list">
          <button
            v-for="item in filteredPalette"
            :key="item.kind"
            type="button"
            class="flow-palette-node"
            draggable="true"
            @dragstart="startDrag($event, item)"
            @dblclick="addPaletteNode(item)"
          >
            <span class="flow-palette-node__icon" :style="{ backgroundColor: item.color }">
              <v-icon :icon="item.icon" size="18" />
            </span>
            <span class="flow-palette-node__content">
              <strong>{{ item.label }}</strong>
              <small>{{ item.description }}</small>
            </span>
            <v-icon icon="mdi-drag" size="17" class="text-medium-emphasis" />
          </button>
        </div>

        <div class="flow-lab__hint">
          <v-icon icon="mdi-cursor-move" size="17" />
          <span>{{ t('alarmCenter.flowLab.dragHint') }}</span>
        </div>
      </aside>

      <main class="flow-lab__canvas" @dragover="allowDrop" @drop="dropNode">
        <VueFlow
          :nodes="nodes"
          :edges="edges"
          :min-zoom="0.25"
          :max-zoom="2"
          :default-viewport="{ zoom: 0.9, x: 20, y: 60 }"
          fit-view-on-init
          snap-to-grid
          :snap-grid="[16, 16]"
          @connect="onConnect"
          @node-click="selectNode"
          @pane-click="clearSelection"
        >
          <Background pattern-color="rgba(120, 130, 150, 0.24)" :gap="20" />
          <Controls position="bottom-left" />
          <MiniMap position="bottom-right" pannable zoomable />

          <template
            v-for="kind in ['source', 'filter', 'logic', 'aggregate', 'sequence', 'output']"
            #[`node-${kind}`]="{ data, selected }"
          >
            <div
              :key="kind"
              class="flow-node"
              :class="{ 'flow-node--selected': selected }"
              :style="{ '--node-color': data.color }"
            >
              <Handle
                v-if="kind !== 'source'"
                type="target"
                :position="Position.Left"
                class="flow-node__handle"
              />
              <div class="flow-node__accent" />
              <div class="flow-node__icon">
                <v-icon :icon="data.icon" size="20" />
              </div>
              <div class="flow-node__body">
                <strong>{{ data.label }}</strong>
                <small>{{ data.subtitle }}</small>
              </div>
              <Handle
                v-if="kind !== 'output'"
                type="source"
                :position="Position.Right"
                class="flow-node__handle"
              />
            </div>
          </template>
        </VueFlow>
      </main>

      <aside class="flow-lab__properties">
        <div class="flow-lab__panel-title">{{ t('alarmCenter.flowLab.propertiesTitle') }}</div>

        <template v-if="selectedNode">
          <div class="flow-lab__selected-type">
            <span :style="{ backgroundColor: selectedNode.data.color }">
              <v-icon :icon="selectedNode.data.icon" size="20" />
            </span>
            <div>
              <strong>{{ selectedNode.data.label }}</strong>
              <small>{{ selectedNode.id }}</small>
            </div>
          </div>

          <v-text-field
            v-model="selectedNode.data.label"
            :label="t('alarmCenter.flowLab.fields.label')"
            density="compact"
            variant="outlined"
          />
          <v-text-field
            v-model="selectedNode.data.subtitle"
            :label="t('alarmCenter.flowLab.fields.subtitle')"
            density="compact"
            variant="outlined"
          />
          <v-textarea
            v-model="selectedNode.data.expression"
            :label="t('alarmCenter.flowLab.fields.expression')"
            rows="4"
            density="compact"
            variant="outlined"
            auto-grow
          />

          <v-btn
            block
            color="error"
            variant="tonal"
            prepend-icon="mdi-delete-outline"
            @click="removeSelectedNode"
          >
            {{ t('alarmCenter.flowLab.deleteNode') }}
          </v-btn>
        </template>

        <div v-else class="flow-lab__empty-properties">
          <v-icon icon="mdi-cursor-default-click-outline" size="34" />
          <span>{{ t('alarmCenter.flowLab.selectHint') }}</span>
        </div>
      </aside>
    </div>
  </div>
</template>

<style>
@import '@vue-flow/core/dist/style.css';
@import '@vue-flow/core/dist/theme-default.css';
@import '@vue-flow/controls/dist/style.css';
@import '@vue-flow/minimap/dist/style.css';
</style>

<style scoped>
.flow-lab {
  overflow: hidden;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px;
  background: rgb(var(--v-theme-surface));
}

.flow-lab__toolbar {
  min-height: 66px;
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 10px 14px;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.flow-lab__workspace {
  display: grid;
  grid-template-columns: 250px minmax(420px, 1fr) 270px;
  height: calc(100vh - 245px);
  min-height: 570px;
}

.flow-lab__palette,
.flow-lab__properties {
  padding: 14px;
  overflow: auto;
  background: rgb(var(--v-theme-surface));
}

.flow-lab__palette {
  border-right: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.flow-lab__properties {
  border-left: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.flow-lab__panel-title {
  margin-bottom: 12px;
  font-size: 0.78rem;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: rgba(var(--v-theme-on-surface), 0.66);
}

.flow-lab__palette-list {
  display: grid;
  gap: 9px;
}

.flow-palette-node {
  width: 100%;
  display: grid;
  grid-template-columns: 34px 1fr 18px;
  align-items: center;
  gap: 9px;
  padding: 9px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
  background: rgb(var(--v-theme-surface));
  color: rgb(var(--v-theme-on-surface));
  text-align: left;
  cursor: grab;
  transition: border-color 120ms ease, transform 120ms ease, box-shadow 120ms ease;
}

.flow-palette-node:hover {
  border-color: rgb(var(--v-theme-primary));
  transform: translateY(-1px);
  box-shadow: 0 4px 14px rgba(20, 30, 55, 0.1);
}

.flow-palette-node:active {
  cursor: grabbing;
}

.flow-palette-node__icon {
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  border-radius: 6px;
  color: white;
}

.flow-palette-node__content {
  min-width: 0;
  display: grid;
}

.flow-palette-node__content strong {
  font-size: 0.8rem;
}

.flow-palette-node__content small {
  overflow: hidden;
  color: rgba(var(--v-theme-on-surface), 0.58);
  font-size: 0.68rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.flow-lab__hint {
  display: flex;
  gap: 7px;
  margin-top: 16px;
  padding: 9px;
  border-radius: 7px;
  background: rgba(var(--v-theme-primary), 0.07);
  color: rgba(var(--v-theme-on-surface), 0.64);
  font-size: 0.72rem;
}

.flow-lab__canvas {
  position: relative;
  min-width: 0;
  background:
    radial-gradient(circle at 20% 20%, rgba(var(--v-theme-primary), 0.04), transparent 32%),
    rgb(var(--v-theme-background));
}

.flow-node {
  --node-color: #2f80ed;
  width: 210px;
  min-height: 58px;
  display: grid;
  grid-template-columns: 5px 38px 1fr;
  align-items: center;
  overflow: hidden;
  border: 1px solid color-mix(in srgb, var(--node-color) 60%, #d5dbe5);
  border-radius: 5px;
  background: rgb(var(--v-theme-surface));
  box-shadow: 0 3px 10px rgba(18, 28, 45, 0.13);
}

.flow-node--selected {
  outline: 3px solid color-mix(in srgb, var(--node-color) 22%, transparent);
  border-color: var(--node-color);
}

.flow-node__accent {
  height: 100%;
  background: var(--node-color);
}

.flow-node__icon {
  display: grid;
  place-items: center;
  color: var(--node-color);
}

.flow-node__body {
  min-width: 0;
  display: grid;
  padding: 9px 10px 9px 0;
}

.flow-node__body strong {
  overflow: hidden;
  font-size: 0.79rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.flow-node__body small {
  overflow: hidden;
  color: rgba(var(--v-theme-on-surface), 0.58);
  font-size: 0.67rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.flow-node__handle {
  width: 11px;
  height: 11px;
  border: 2px solid rgb(var(--v-theme-surface));
  background: var(--node-color);
}

.flow-lab--cards .flow-node {
  min-height: 76px;
  grid-template-columns: 8px 48px 1fr;
  border: 0;
  border-radius: 12px;
  box-shadow: 0 8px 24px rgba(18, 28, 45, 0.18);
}

.flow-lab--cards .flow-node__icon {
  width: 34px;
  height: 34px;
  border-radius: 9px;
  background: color-mix(in srgb, var(--node-color) 14%, transparent);
}

.flow-lab--minimal .flow-node {
  min-height: 44px;
  grid-template-columns: 3px 30px 1fr;
  border-radius: 3px;
  box-shadow: none;
}

.flow-lab--minimal .flow-node__body small {
  display: none;
}

.flow-lab__selected-type {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 18px;
}

.flow-lab__selected-type > span {
  width: 38px;
  height: 38px;
  display: grid;
  place-items: center;
  border-radius: 8px;
  color: white;
}

.flow-lab__selected-type > div {
  min-width: 0;
  display: grid;
}

.flow-lab__selected-type small {
  color: rgba(var(--v-theme-on-surface), 0.55);
}

.flow-lab__empty-properties {
  min-height: 220px;
  display: grid;
  place-items: center;
  align-content: center;
  gap: 12px;
  color: rgba(var(--v-theme-on-surface), 0.45);
  text-align: center;
  font-size: 0.8rem;
}

:deep(.vue-flow__edge-path) {
  stroke: rgba(var(--v-theme-on-surface), 0.52);
  stroke-width: 2;
}

:deep(.vue-flow__minimap) {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 7px;
  background: rgb(var(--v-theme-surface));
}

@media (max-width: 1100px) {
  .flow-lab__workspace {
    grid-template-columns: 220px minmax(420px, 1fr);
  }

  .flow-lab__properties {
    display: none;
  }
}
</style>
