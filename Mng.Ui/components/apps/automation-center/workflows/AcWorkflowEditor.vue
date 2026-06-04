<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type {
  SaveWorkflowVersionRequest,
  WorkflowDefinitionDocument,
  WorkflowInstanceSummary,
  WorkflowNodeDefinition,
  WorkflowRunDetail,
  WorkflowVersionDocument,
} from '@/types/apps/workflowDefinition';
import {
  WORKFLOW_EDGE_KEY_PRESETS,
  WORKFLOW_EVENT_TYPES,
  WORKFLOW_NODE_CATALOG,
  WORKFLOW_TRIGGER_TYPES,
  createStarterWorkflowGraph,
} from '@/constants/workflowNodeCatalog';
import {
  workflowDefinitionGet,
  workflowDefinitionUpdate,
  workflowGetRun,
  workflowListRuns,
  workflowStartRun,
  workflowVersionCreateDraft,
  workflowVersionList,
  workflowVersionPublish,
  workflowVersionUpdateDraft,
} from '@/services/workflowService';

const props = defineProps<{ workflowId: string }>();

const { t } = useAppI18n();

const loading = ref(true);
const saving = ref(false);
const publishing = ref(false);
const running = ref(false);
const errorLocal = ref<string | null>(null);
const infoLocal = ref<string | null>(null);

const definition = ref<WorkflowDefinitionDocument | null>(null);
const versions = ref<WorkflowVersionDocument[]>([]);
const selectedVersionId = ref<string | null>(null);
const runs = ref<WorkflowInstanceSummary[]>([]);
const runDetail = ref<WorkflowRunDetail | null>(null);

const metaForm = ref({ name: '', category: '' });
const graphForm = ref<SaveWorkflowVersionRequest>(createStarterWorkflowGraph());

const activeTab = ref('nodes');

const nodeDialogOpen = ref(false);
const editingNodeIndex = ref<number | null>(null);
const nodeForm = ref({ id: '', type: 'manual.trigger', configJson: '{}' });

const edgeDialogOpen = ref(false);
const editingEdgeIndex = ref<number | null>(null);
const edgeForm = ref({ fromNodeId: '', toNodeId: '', edgeKey: 'default' });

const triggerDialogOpen = ref(false);
const editingTriggerIndex = ref<number | null>(null);
const triggerForm = ref({
  type: 'event',
  eventType: 'alarm.raised',
  filterExpression: '',
  enabled: true,
});

const nodeTypeItems = computed(() =>
  WORKFLOW_NODE_CATALOG.map((n) => ({
    title: t(`automationCenter.workflows.nodeTypes.${n.labelKey}`),
    value: n.type,
  })),
);

const selectedVersion = computed(() =>
  versions.value.find((v) => v.id === selectedVersionId.value) ?? null,
);

const isDraft = computed(() => normalizeStatus(selectedVersion.value?.status) === 'Draft');

const nodeIdItems = computed(() =>
  graphForm.value.nodes.map((n) => ({ title: n.id, value: n.id })),
);

function normalizeStatus(status: WorkflowVersionDocument['status'] | undefined): string {
  if (status === 0 || status === 'Draft') return 'Draft';
  if (status === 1 || status === 'Published') return 'Published';
  if (status === 2 || status === 'Archived') return 'Archived';
  return 'Unknown';
}

function statusColor(status: string): string {
  if (status === 'Draft') return 'warning';
  if (status === 'Published') return 'success';
  return 'grey';
}

function instanceStatusLabel(status: WorkflowInstanceSummary['status']): string {
  const map: Record<string, string> = {
    0: 'Running',
    1: 'Waiting',
    2: 'Completed',
    3: 'Failed',
    4: 'Cancelled',
    Running: 'Running',
    Waiting: 'Waiting',
    Completed: 'Completed',
    Failed: 'Failed',
    Cancelled: 'Cancelled',
  };
  return map[String(status)] ?? String(status);
}

function loadGraphFromVersion(version: WorkflowVersionDocument) {
  graphForm.value = {
    entryNodeId: version.entryNodeId,
    nodes: version.nodes.map((n) => ({
      id: n.id,
      type: n.type,
      config: n.config ? { ...n.config } : {},
    })),
    edges: version.edges.map((e) => ({ ...e })),
    triggers: version.triggers.map((tr) => ({
      type: tr.type,
      config: tr.config ? { ...tr.config } : {},
      filterExpression: tr.filterExpression ?? undefined,
      enabled: tr.enabled ?? true,
    })),
  };
}

async function loadAll() {
  loading.value = true;
  errorLocal.value = null;
  try {
    definition.value = await workflowDefinitionGet(props.workflowId);
    metaForm.value = {
      name: definition.value.name,
      category: definition.value.category ?? '',
    };
    versions.value = await workflowVersionList(props.workflowId);
    const draft = versions.value.find((v) => normalizeStatus(v.status) === 'Draft');
    const published = versions.value.find((v) => normalizeStatus(v.status) === 'Published');
    const pick = draft ?? published ?? versions.value[0] ?? null;
    selectedVersionId.value = pick?.id ?? null;
    if (pick) loadGraphFromVersion(pick);
    runs.value = await workflowListRuns(props.workflowId, 8);
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('automationCenter.workflows.loadError');
  } finally {
    loading.value = false;
  }
}

watch(selectedVersionId, (id) => {
  const version = versions.value.find((v) => v.id === id);
  if (version) loadGraphFromVersion(version);
});

async function saveMeta() {
  if (!definition.value) return;
  saving.value = true;
  errorLocal.value = null;
  try {
    definition.value = await workflowDefinitionUpdate(props.workflowId, {
      name: metaForm.value.name.trim(),
      category: metaForm.value.category.trim() || undefined,
    });
    infoLocal.value = t('automationCenter.workflows.metaSaved');
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('automationCenter.workflows.saveError');
  } finally {
    saving.value = false;
  }
}

async function saveDraft() {
  if (!selectedVersionId.value || !isDraft.value) return;
  saving.value = true;
  errorLocal.value = null;
  try {
    const updated = await workflowVersionUpdateDraft(selectedVersionId.value, graphForm.value);
    replaceVersion(updated);
    infoLocal.value = t('automationCenter.workflows.draftSaved');
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('automationCenter.workflows.saveError');
  } finally {
    saving.value = false;
  }
}

async function createDraft(fromCurrent = false) {
  saving.value = true;
  errorLocal.value = null;
  try {
    const payload = fromCurrent && graphForm.value.nodes.length > 0
      ? graphForm.value
      : createStarterWorkflowGraph();
    const created = await workflowVersionCreateDraft(props.workflowId, payload);
    versions.value = await workflowVersionList(props.workflowId);
    selectedVersionId.value = created.id;
    loadGraphFromVersion(created);
    infoLocal.value = t('automationCenter.workflows.draftCreated');
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('automationCenter.workflows.saveError');
  } finally {
    saving.value = false;
  }
}

async function publishDraft() {
  if (!selectedVersionId.value || !isDraft.value) return;
  publishing.value = true;
  errorLocal.value = null;
  try {
    await saveDraft();
    const published = await workflowVersionPublish(selectedVersionId.value);
    versions.value = await workflowVersionList(props.workflowId);
    selectedVersionId.value = published.id;
    loadGraphFromVersion(published);
    definition.value = await workflowDefinitionGet(props.workflowId);
    infoLocal.value = t('automationCenter.workflows.published');
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('automationCenter.workflows.publishError');
  } finally {
    publishing.value = false;
  }
}

async function testRun() {
  if (!definition.value) return;
  running.value = true;
  errorLocal.value = null;
  runDetail.value = null;
  try {
    const versionId = definition.value.currentVersionId;
    if (!versionId) throw new Error(t('automationCenter.workflows.noPublishedVersion'));
    const result = await workflowStartRun({
      workflowId: props.workflowId,
      workflowVersionId: versionId,
      triggerType: 'manual',
      triggerData: { value: 1 },
    });
    infoLocal.value = t('automationCenter.workflows.runQueued', { id: result.instanceId });
    await refreshRuns();
    if (runs.value[0]?.id) {
      runDetail.value = await workflowGetRun(runs.value[0].id);
    }
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('automationCenter.workflows.runError');
  } finally {
    running.value = false;
  }
}

async function refreshRuns() {
  runs.value = await workflowListRuns(props.workflowId, 8);
}

async function inspectRun(row: WorkflowInstanceSummary) {
  runDetail.value = await workflowGetRun(row.id);
}

function replaceVersion(updated: WorkflowVersionDocument) {
  const idx = versions.value.findIndex((v) => v.id === updated.id);
  if (idx >= 0) versions.value[idx] = updated;
  else versions.value.unshift(updated);
}

function parseConfigJson(): Record<string, unknown> {
  try {
    const parsed = JSON.parse(nodeForm.value.configJson || '{}');
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : {};
  } catch {
    throw new Error(t('automationCenter.workflows.invalidJson'));
  }
}

function openNodeCreate() {
  editingNodeIndex.value = null;
  nodeForm.value = { id: `node_${graphForm.value.nodes.length + 1}`, type: 'write.log', configJson: '{\n  "message": ""\n}' };
  nodeDialogOpen.value = true;
}

function openNodeEdit(index: number) {
  const node = graphForm.value.nodes[index];
  editingNodeIndex.value = index;
  nodeForm.value = {
    id: node.id,
    type: node.type,
    configJson: JSON.stringify(node.config ?? {}, null, 2),
  };
  nodeDialogOpen.value = true;
}

function saveNode() {
  try {
    const config = parseConfigJson();
    const node: WorkflowNodeDefinition = {
      id: nodeForm.value.id.trim(),
      type: nodeForm.value.type,
      config,
    };
    if (!node.id) return;
    if (editingNodeIndex.value == null) {
      graphForm.value.nodes.push(node);
    } else {
      graphForm.value.nodes[editingNodeIndex.value] = node;
    }
    nodeDialogOpen.value = false;
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('automationCenter.workflows.invalidJson');
  }
}

function removeNode(index: number) {
  const id = graphForm.value.nodes[index]?.id;
  graphForm.value.nodes.splice(index, 1);
  if (id) {
    graphForm.value.edges = graphForm.value.edges.filter((e) => e.fromNodeId !== id && e.toNodeId !== id);
    if (graphForm.value.entryNodeId === id) graphForm.value.entryNodeId = graphForm.value.nodes[0]?.id ?? '';
  }
}

function openEdgeCreate() {
  editingEdgeIndex.value = null;
  edgeForm.value = {
    fromNodeId: graphForm.value.nodes[0]?.id ?? '',
    toNodeId: graphForm.value.nodes[1]?.id ?? graphForm.value.nodes[0]?.id ?? '',
    edgeKey: 'default',
  };
  edgeDialogOpen.value = true;
}

function openEdgeEdit(index: number) {
  const edge = graphForm.value.edges[index];
  editingEdgeIndex.value = index;
  edgeForm.value = { ...edge };
  edgeDialogOpen.value = true;
}

function saveEdge() {
  const edge = { ...edgeForm.value };
  if (!edge.fromNodeId || !edge.toNodeId) return;
  if (editingEdgeIndex.value == null) graphForm.value.edges.push(edge);
  else graphForm.value.edges[editingEdgeIndex.value] = edge;
  edgeDialogOpen.value = false;
}

function removeEdge(index: number) {
  graphForm.value.edges.splice(index, 1);
}

function openTriggerCreate() {
  editingTriggerIndex.value = null;
  triggerForm.value = { type: 'event', eventType: 'alarm.raised', filterExpression: '', enabled: true };
  triggerDialogOpen.value = true;
}

function openTriggerEdit(index: number) {
  const tr = graphForm.value.triggers[index];
  editingTriggerIndex.value = index;
  triggerForm.value = {
    type: tr.type,
    eventType: String(tr.config?.eventType ?? 'alarm.raised'),
    filterExpression: tr.filterExpression ?? '',
    enabled: tr.enabled ?? true,
  };
  triggerDialogOpen.value = true;
}

function saveTrigger() {
  const trigger = {
    type: triggerForm.value.type,
    config: triggerForm.value.type === 'event' ? { eventType: triggerForm.value.eventType } : {},
    filterExpression: triggerForm.value.filterExpression.trim() || undefined,
    enabled: triggerForm.value.enabled,
  };
  if (editingTriggerIndex.value == null) graphForm.value.triggers.push(trigger);
  else graphForm.value.triggers[editingTriggerIndex.value] = trigger;
  triggerDialogOpen.value = false;
}

function removeTrigger(index: number) {
  graphForm.value.triggers.splice(index, 1);
}

onMounted(() => {
  void loadAll();
});
</script>

<template>
  <div>
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>
    <v-alert v-if="infoLocal" type="success" variant="tonal" class="mb-4" closable @click:close="infoLocal = null">
      {{ infoLocal }}
    </v-alert>

    <v-skeleton-loader v-if="loading" type="article" />

    <template v-else-if="definition">
      <v-row class="mb-4">
        <v-col cols="12" md="4">
          <v-text-field v-model="metaForm.name" :label="t('automationCenter.workflows.fieldName')" density="compact" />
        </v-col>
        <v-col cols="12" md="4">
          <v-text-field v-model="metaForm.category" :label="t('automationCenter.workflows.fieldCategory')" density="compact" />
        </v-col>
        <v-col cols="12" md="4" class="d-flex align-center ga-2">
          <v-btn variant="tonal" :loading="saving" @click="saveMeta">{{ t('automationCenter.workflows.saveMeta') }}</v-btn>
        </v-col>
      </v-row>

      <v-card variant="outlined" class="rounded-lg mb-4 pa-4">
        <div class="d-flex flex-wrap align-center ga-2 mb-3">
          <v-select
            v-model="selectedVersionId"
            :items="versions"
            item-title="version"
            item-value="id"
            :label="t('automationCenter.workflows.versionSelect')"
            density="compact"
            style="max-width: 280px"
          >
            <template #item="{ props: itemProps, item }">
              <v-list-item v-bind="itemProps">
                <template #title>
                  v{{ item.raw.version }}
                  <v-chip size="x-small" :color="statusColor(normalizeStatus(item.raw.status))" class="ms-2">
                    {{ normalizeStatus(item.raw.status) }}
                  </v-chip>
                </template>
              </v-list-item>
            </template>
            <template #selection="{ item }">
              v{{ item.raw.version }} · {{ normalizeStatus(item.raw.status) }}
            </template>
          </v-select>

          <v-btn variant="tonal" prepend-icon="mdi-file-plus" @click="createDraft(true)">
            {{ t('automationCenter.workflows.newDraft') }}
          </v-btn>
          <v-btn color="primary" variant="flat" :disabled="!isDraft" :loading="saving" @click="saveDraft">
            {{ t('automationCenter.workflows.saveDraft') }}
          </v-btn>
          <v-btn color="success" variant="flat" :disabled="!isDraft" :loading="publishing" @click="publishDraft">
            {{ t('automationCenter.workflows.publish') }}
          </v-btn>
          <v-btn variant="outlined" :loading="running" @click="testRun">
            {{ t('automationCenter.workflows.testRun') }}
          </v-btn>
        </div>

        <v-alert v-if="!isDraft" type="info" variant="tonal" density="compact" class="mb-3">
          {{ t('automationCenter.workflows.readOnlyHint') }}
        </v-alert>

        <v-text-field
          v-model="graphForm.entryNodeId"
          :label="t('automationCenter.workflows.entryNodeId')"
          density="compact"
          class="mb-3"
          :readonly="!isDraft"
        />

        <v-tabs v-model="activeTab" density="compact" class="mb-3">
          <v-tab value="nodes">{{ t('automationCenter.workflows.tabNodes') }}</v-tab>
          <v-tab value="edges">{{ t('automationCenter.workflows.tabEdges') }}</v-tab>
          <v-tab value="triggers">{{ t('automationCenter.workflows.tabTriggers') }}</v-tab>
        </v-tabs>

        <v-window v-model="activeTab">
          <v-window-item value="nodes">
            <div class="d-flex mb-2">
              <v-btn size="small" prepend-icon="mdi-plus" :disabled="!isDraft" @click="openNodeCreate">
                {{ t('automationCenter.workflows.addNode') }}
              </v-btn>
            </div>
            <v-table density="compact">
              <thead>
                <tr>
                  <th>ID</th>
                  <th>{{ t('automationCenter.workflows.colType') }}</th>
                  <th>config</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                <tr v-for="(node, idx) in graphForm.nodes" :key="node.id">
                  <td>{{ node.id }}</td>
                  <td>{{ node.type }}</td>
                  <td class="text-caption">{{ JSON.stringify(node.config ?? {}) }}</td>
                  <td class="text-end">
                    <v-btn icon="mdi-pencil" size="x-small" variant="text" :disabled="!isDraft" @click="openNodeEdit(idx)" />
                    <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" :disabled="!isDraft" @click="removeNode(idx)" />
                  </td>
                </tr>
              </tbody>
            </v-table>
          </v-window-item>

          <v-window-item value="edges">
            <div class="d-flex mb-2">
              <v-btn size="small" prepend-icon="mdi-plus" :disabled="!isDraft" @click="openEdgeCreate">
                {{ t('automationCenter.workflows.addEdge') }}
              </v-btn>
            </div>
            <v-table density="compact">
              <thead>
                <tr>
                  <th>from</th>
                  <th>to</th>
                  <th>edgeKey</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                <tr v-for="(edge, idx) in graphForm.edges" :key="`${edge.fromNodeId}-${edge.toNodeId}-${idx}`">
                  <td>{{ edge.fromNodeId }}</td>
                  <td>{{ edge.toNodeId }}</td>
                  <td>{{ edge.edgeKey }}</td>
                  <td class="text-end">
                    <v-btn icon="mdi-pencil" size="x-small" variant="text" :disabled="!isDraft" @click="openEdgeEdit(idx)" />
                    <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" :disabled="!isDraft" @click="removeEdge(idx)" />
                  </td>
                </tr>
              </tbody>
            </v-table>
          </v-window-item>

          <v-window-item value="triggers">
            <div class="d-flex mb-2">
              <v-btn size="small" prepend-icon="mdi-plus" :disabled="!isDraft" @click="openTriggerCreate">
                {{ t('automationCenter.workflows.addTrigger') }}
              </v-btn>
            </div>
            <v-table density="compact">
              <thead>
                <tr>
                  <th>{{ t('automationCenter.workflows.colType') }}</th>
                  <th>config</th>
                  <th>filter</th>
                  <th>enabled</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                <tr v-for="(tr, idx) in graphForm.triggers" :key="idx">
                  <td>{{ tr.type }}</td>
                  <td class="text-caption">{{ JSON.stringify(tr.config ?? {}) }}</td>
                  <td>{{ tr.filterExpression || '—' }}</td>
                  <td>{{ tr.enabled !== false ? '✓' : '—' }}</td>
                  <td class="text-end">
                    <v-btn icon="mdi-pencil" size="x-small" variant="text" :disabled="!isDraft" @click="openTriggerEdit(idx)" />
                    <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" :disabled="!isDraft" @click="removeTrigger(idx)" />
                  </td>
                </tr>
              </tbody>
            </v-table>
          </v-window-item>
        </v-window>
      </v-card>

      <v-card variant="outlined" class="rounded-lg pa-4">
        <div class="d-flex align-center mb-3">
          <h3 class="text-subtitle-1 font-weight-bold">{{ t('automationCenter.workflows.runHistory') }}</h3>
          <v-spacer />
          <v-btn size="small" variant="text" @click="refreshRuns">{{ t('automationCenter.workflows.refresh') }}</v-btn>
        </div>
        <v-table density="compact">
          <thead>
            <tr>
              <th>ID</th>
              <th>{{ t('automationCenter.workflows.colStatus') }}</th>
              <th>trigger</th>
              <th>started</th>
              <th />
            </tr>
          </thead>
          <tbody>
            <tr v-for="run in runs" :key="run.id">
              <td class="text-caption">{{ run.id.slice(0, 8) }}…</td>
              <td>{{ instanceStatusLabel(run.status) }}</td>
              <td>{{ run.triggerType }}</td>
              <td>{{ new Date(run.startedAt).toLocaleString() }}</td>
              <td class="text-end">
                <v-btn size="x-small" variant="text" @click="inspectRun(run)">{{ t('automationCenter.workflows.inspectRun') }}</v-btn>
              </td>
            </tr>
          </tbody>
        </v-table>
        <div v-if="runDetail" class="mt-4">
          <div class="text-caption text-medium-emphasis mb-2">
            {{ runDetail.instance.id }} · {{ instanceStatusLabel(runDetail.instance.status) }}
          </div>
          <v-table density="compact">
            <thead>
              <tr><th>node</th><th>status</th><th>error</th></tr>
            </thead>
            <tbody>
              <tr v-for="ex in runDetail.executions" :key="`${ex.nodeId}-${ex.attempt}`">
                <td>{{ ex.nodeId }}</td>
                <td>{{ ex.status }}</td>
                <td>{{ ex.errorMessage || '—' }}</td>
              </tr>
            </tbody>
          </v-table>
        </div>
      </v-card>
    </template>

    <!-- Node dialog -->
    <v-dialog v-model="nodeDialogOpen" max-width="560" persistent>
      <v-card>
        <v-card-title>{{ editingNodeIndex == null ? t('automationCenter.workflows.addNode') : t('automationCenter.workflows.editNode') }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="nodeForm.id" label="ID" class="mb-2" />
          <v-select v-model="nodeForm.type" :items="nodeTypeItems" :label="t('automationCenter.workflows.colType')" class="mb-2" />
          <v-textarea v-model="nodeForm.configJson" label="config (JSON)" rows="8" class="font-monospace" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="nodeDialogOpen = false">{{ t('automationCenter.workflows.cancel') }}</v-btn>
          <v-btn color="primary" @click="saveNode">{{ t('automationCenter.workflows.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Edge dialog -->
    <v-dialog v-model="edgeDialogOpen" max-width="480" persistent>
      <v-card>
        <v-card-title>{{ editingEdgeIndex == null ? t('automationCenter.workflows.addEdge') : t('automationCenter.workflows.editEdge') }}</v-card-title>
        <v-card-text>
          <v-select v-model="edgeForm.fromNodeId" :items="nodeIdItems" label="fromNodeId" class="mb-2" />
          <v-select v-model="edgeForm.toNodeId" :items="nodeIdItems" label="toNodeId" class="mb-2" />
          <v-combobox v-model="edgeForm.edgeKey" :items="[...WORKFLOW_EDGE_KEY_PRESETS]" label="edgeKey" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="edgeDialogOpen = false">{{ t('automationCenter.workflows.cancel') }}</v-btn>
          <v-btn color="primary" @click="saveEdge">{{ t('automationCenter.workflows.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Trigger dialog -->
    <v-dialog v-model="triggerDialogOpen" max-width="520" persistent>
      <v-card>
        <v-card-title>{{ editingTriggerIndex == null ? t('automationCenter.workflows.addTrigger') : t('automationCenter.workflows.editTrigger') }}</v-card-title>
        <v-card-text>
          <v-select
            v-model="triggerForm.type"
            :items="WORKFLOW_TRIGGER_TYPES.map((x) => ({ title: t(`automationCenter.workflows.triggerTypes.${x.labelKey}`), value: x.value }))"
            :label="t('automationCenter.workflows.colType')"
            class="mb-2"
          />
          <v-select
            v-if="triggerForm.type === 'event'"
            v-model="triggerForm.eventType"
            :items="[...WORKFLOW_EVENT_TYPES]"
            label="eventType"
            class="mb-2"
          />
          <v-text-field v-model="triggerForm.filterExpression" label="filterExpression (Jint)" class="mb-2" />
          <v-switch v-model="triggerForm.enabled" :label="t('automationCenter.workflows.enabled')" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="triggerDialogOpen = false">{{ t('automationCenter.workflows.cancel') }}</v-btn>
          <v-btn color="primary" @click="saveTrigger">{{ t('automationCenter.workflows.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
