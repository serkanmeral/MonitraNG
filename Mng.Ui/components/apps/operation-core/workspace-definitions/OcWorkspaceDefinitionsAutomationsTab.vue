<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useOcWorkspaceCatalogInject } from '@/composables/useOcWorkspaceCatalog';
import OcWorkspaceAutomationDialog, {
  type OcAutomationFormModel,
} from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceAutomationDialog.vue';
import OcWorkspaceAutomationSimulateDialog from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceAutomationSimulateDialog.vue';
import {
  ocCreateWorkspaceAutomation,
  ocDeleteWorkspaceAutomation,
  ocExtractDgErrorMessage,
  ocListAutomationsForWorkspace,
  ocUpdateWorkspaceAutomation,
} from '@/services/operationCoreService';
import { ocListStateFlowsForWorkspace } from '@/services/operationCore/flows';
import type { OpBoard, OpWorkItemType, OpWorkspaceAutomation } from '@/types/apps/operationCore';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();
const catalog = useOcWorkspaceCatalogInject();

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);

const automations = ref<OpWorkspaceAutomation[]>([]);
const boards = ref<OpBoard[]>([]);
const types = ref<OpWorkItemType[]>([]);
const transitionKeys = ref<string[]>([]);

const dialog = ref(false);
const editId = ref<string | null>(null);
const dialogRef = ref<InstanceType<typeof OcWorkspaceAutomationDialog> | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<OpWorkspaceAutomation | null>(null);
const simulateDialog = ref(false);
const simulateTarget = ref<OpWorkspaceAutomation | null>(null);

const boardItems = computed(() =>
  boards.value.map((b) => ({ value: b.__dataId, title: b.name }))
);
const typeItems = computed(() =>
  types.value.map((ty) => ({ value: ty.__dataId, title: ty.name }))
);
const transitionItems = computed(() =>
  transitionKeys.value.map((key) => ({ value: key, title: key }))
);

const boardNameById = computed(() => new Map(boards.value.map((b) => [b.__dataId, b.name])));
const typeNameById = computed(() => new Map(types.value.map((ty) => [ty.__dataId, ty.name])));

const activeCount = computed(() => automations.value.filter((a) => a.isActive).length);

const tableHeaders = computed(() => [
  { title: t('operationCore.workspaceDefinitions.automations.colName'), key: 'name', sortable: true },
  { title: t('operationCore.workspaceDefinitions.automations.colTrigger'), key: 'trigger', sortable: false },
  { title: t('operationCore.workspaceDefinitions.automations.colTarget'), key: 'target', sortable: false },
  { title: t('operationCore.workspaceDefinitions.automations.colStatus'), key: 'isActive', sortable: true },
  { title: t('operationCore.workspaceDefinitions.automations.colLastRun'), key: 'lastRunAt', sortable: true },
  { title: t('operationCore.workspaceDefinitions.automations.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

function triggerSummary(row: OpWorkspaceAutomation): string {
  const tr = row.trigger;
  if (!tr || tr.kind !== 'workItemStateReached') return '—';
  const parts: string[] = [];
  if (tr.typeId) parts.push(typeNameById.value.get(tr.typeId) ?? tr.typeId);
  if (tr.transitionKey) parts.push(tr.transitionKey);
  if (tr.conditions && typeof tr.conditions === 'object') {
    const cond = tr.conditions as { items?: { field?: string; value?: string }[] };
    const first = cond.items?.[0];
    if (first?.field && first.value != null) parts.push(`${first.field}=${first.value}`);
  }
  return parts.length ? parts.join(' · ') : t('operationCore.workspaceDefinitions.automations.triggerAny');
}

function targetSummary(row: OpWorkspaceAutomation): string {
  const action = row.actions?.find((a) => a.type === 'createWorkItem');
  if (!action || action.type !== 'createWorkItem') return '—';
  const board = boardNameById.value.get(action.target.boardId) ?? action.target.boardId;
  const type = typeNameById.value.get(action.target.typeId) ?? action.target.typeId;
  return `${board} / ${type}`;
}

function workItemProfileHref(id: string): string {
  return `/apps/operation-core/work-items/${encodeURIComponent(id)}/profile?workspaceId=${encodeURIComponent(props.workspaceId)}`;
}

async function loadTransitions() {
  try {
    const flows = await ocListStateFlowsForWorkspace(props.workspaceId);
    const keys = new Set<string>();
    for (const flow of flows) {
      for (const tr of flow.transitions ?? []) {
        if (tr.transitionKey) keys.add(tr.transitionKey);
      }
    }
    transitionKeys.value = [...keys].sort();
  } catch {
    transitionKeys.value = [];
  }
}

async function loadAll() {
  if (!props.workspaceId) {
    automations.value = [];
    return;
  }
  loading.value = true;
  errorLocal.value = null;
  try {
    const [rows] = await Promise.all([
      ocListAutomationsForWorkspace(props.workspaceId),
      catalog.whenReady(),
      loadTransitions(),
    ]);
    automations.value = rows;
    boards.value = catalog.boards.value;
    types.value = catalog.types.value;
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.automations.loadError')
    );
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.workspaceId,
  () => {
    void loadAll();
  },
  { immediate: true }
);

function defaultFormModel(): OcAutomationFormModel {
  return {
    name: '',
    description: '',
    isActive: true,
    sourceTypeId: types.value.length === 1 ? types.value[0]!.__dataId : '',
    sourceBoardId: '',
    transitionKey: '',
    conditionField: '',
    conditionValue: '',
    targetBoardId: boards.value.length === 1 ? boards.value[0]!.__dataId : '',
    targetTypeId: '',
    title: 'Uygunsuzluk — {{source.key}}',
    assignee: '{{source.assignee}}',
    idempotencyMode: 'none',
    relationMode: 'parent',
    fieldMappings: [
      { target: 'parentItemId', source: 'relation', relation: 'parent' },
      { target: 'lotSerial', source: 'field', path: 'fields.lotSerial' },
      { target: 'defectDescription', source: 'field', path: 'fields.qualityNotes' },
      { target: 'ncrSource', source: 'static', value: 'final_inspection' },
    ],
  };
}

function formFromRecord(row: OpWorkspaceAutomation): OcAutomationFormModel {
  const tr =
    row.trigger?.kind === 'workItemStateReached'
      ? row.trigger
      : { kind: 'workItemStateReached' as const };
  const action = row.actions?.find((a) => a.type === 'createWorkItem');
  const create =
    action?.type === 'createWorkItem'
      ? action
      : {
          target: { boardId: '', typeId: '' },
          title: '',
          assignee: '',
          fieldMappings: [],
        };

  let conditionField = '';
  let conditionValue = '';
  if (tr.conditions && typeof tr.conditions === 'object') {
    const cond = tr.conditions as { items?: { field?: string; value?: string }[] };
    conditionField = cond.items?.[0]?.field ?? '';
    conditionValue = cond.items?.[0]?.value != null ? String(cond.items[0]!.value) : '';
  }

  return {
    name: row.name,
    description: row.description ?? '',
    isActive: row.isActive,
    sourceTypeId: tr.typeId ?? '',
    sourceBoardId: tr.boardId ?? '',
    transitionKey: tr.transitionKey ?? '',
    conditionField,
    conditionValue,
    targetBoardId: create.target.boardId,
    targetTypeId: create.target.typeId,
    title: create.title,
    assignee: create.assignee ?? '',
    idempotencyMode: row.idempotency?.mode ?? 'none',
    relationMode: row.relation?.mode ?? 'parent',
    fieldMappings: [...(create.fieldMappings ?? [])],
  };
}

function buildPayload(form: OcAutomationFormModel): Record<string, unknown> {
  const conditions =
    form.conditionField.trim() && form.conditionValue.trim()
      ? {
          op: 'and',
          items: [
            {
              field: form.conditionField.trim(),
              cmp: 'eq',
              value: form.conditionValue.trim(),
            },
          ],
        }
      : undefined;

  const trigger: Record<string, unknown> = {
    kind: 'workItemStateReached',
  };
  if (form.sourceTypeId) trigger.typeId = form.sourceTypeId;
  if (form.sourceBoardId) trigger.boardId = form.sourceBoardId;
  if (form.transitionKey) trigger.transitionKey = form.transitionKey;
  if (conditions) trigger.conditions = conditions;

  return {
    workspaceId: props.workspaceId,
    name: form.name.trim(),
    description: form.description.trim() || null,
    isActive: form.isActive,
    trigger,
    idempotency: { mode: form.idempotencyMode },
    relation: { mode: form.relationMode },
    actions: [
      {
        type: 'createWorkItem',
        order: 1,
        target: {
          boardId: form.targetBoardId,
          typeId: form.targetTypeId,
        },
        title: form.title.trim(),
        assignee: form.assignee.trim() || undefined,
        fieldMappings: form.fieldMappings.filter((m) => m.target?.trim()),
      },
    ],
  };
}

function openCreate() {
  editId.value = null;
  errorLocal.value = null;
  dialog.value = true;
  requestAnimationFrame(() => {
    dialogRef.value?.resetForm(defaultFormModel());
  });
}

function openEdit(row: OpWorkspaceAutomation) {
  editId.value = row.__dataId;
  errorLocal.value = null;
  dialog.value = true;
  requestAnimationFrame(() => {
    dialogRef.value?.resetForm(formFromRecord(row));
  });
}

function openDelete(row: OpWorkspaceAutomation) {
  deleteTarget.value = row;
  deleteDialog.value = true;
}

function openSimulate(row: OpWorkspaceAutomation) {
  simulateTarget.value = row;
  simulateDialog.value = true;
}

async function onSimulated() {
  await loadAll();
}

async function onSave(form: OcAutomationFormModel) {
  saving.value = true;
  errorLocal.value = null;
  try {
    const payload = buildPayload(form);
    if (editId.value) {
      await ocUpdateWorkspaceAutomation(editId.value, payload);
    } else {
      await ocCreateWorkspaceAutomation(payload);
    }
    dialog.value = false;
    await loadAll();
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.automations.saveError')
    );
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await ocDeleteWorkspaceAutomation(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    deleteTarget.value = null;
    await loadAll();
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.automations.deleteError')
    );
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between ga-3 mb-4">
      <div>
        <h3 class="text-h6">
          {{ t('operationCore.workspaceDefinitions.automations.pageTitle') }}
        </h3>
        <p class="text-body-2 text-medium-emphasis mb-0">
          {{ t('operationCore.workspaceDefinitions.automations.pageSubtitle') }}
        </p>
      </div>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">
        {{ t('operationCore.workspaceDefinitions.automations.addAutomation') }}
      </v-btn>
    </div>

    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <div v-if="!loading && automations.length" class="d-flex flex-wrap ga-3 mb-4">
      <v-chip color="primary" variant="tonal">
        {{ t('operationCore.workspaceDefinitions.automations.statsTotal', { count: automations.length }) }}
      </v-chip>
      <v-chip color="success" variant="tonal">
        {{ t('operationCore.workspaceDefinitions.automations.statsActive', { count: activeCount }) }}
      </v-chip>
    </div>

    <v-alert
      v-if="!loading && !automations.length"
      type="info"
      variant="tonal"
      :title="t('operationCore.workspaceDefinitions.automations.emptyTitle')"
      :text="t('operationCore.workspaceDefinitions.automations.emptyBody')"
    />

    <v-data-table
      v-if="!loading && automations.length"
      :headers="tableHeaders"
      :items="automations"
      item-value="__dataId"
      class="rounded-lg border"
    >
      <template #item.trigger="{ item }">
        <span class="text-body-2">{{ triggerSummary(item) }}</span>
      </template>
      <template #item.target="{ item }">
        <span class="text-body-2">{{ targetSummary(item) }}</span>
      </template>
      <template #item.isActive="{ item }">
        <v-chip :color="item.isActive ? 'success' : 'default'" size="small" variant="tonal">
          {{
            item.isActive
              ? t('operationCore.workspaceDefinitions.automations.activeYes')
              : t('operationCore.workspaceDefinitions.automations.activeNo')
          }}
        </v-chip>
      </template>
      <template #item.lastRunAt="{ item }">
        <div class="d-flex flex-column ga-1">
          <span class="text-body-2">
            {{
              item.lastRunAt
                ? new Date(item.lastRunAt).toLocaleString('tr-TR')
                : t('operationCore.workspaceDefinitions.automations.neverRun')
            }}
          </span>
          <NuxtLink
            v-if="item.lastCreatedWorkItemId"
            :to="workItemProfileHref(item.lastCreatedWorkItemId)"
            class="text-caption"
          >
            {{ t('operationCore.workspaceDefinitions.automations.viewLastWorkItem') }}
          </NuxtLink>
        </div>
      </template>
      <template #item.actions="{ item }">
        <v-btn
          icon="mdi-flask-outline"
          variant="text"
          size="small"
          :title="t('operationCore.workspaceDefinitions.automations.simulate.action')"
          @click="openSimulate(item)"
        />
        <v-btn icon="mdi-pencil-outline" variant="text" size="small" @click="openEdit(item)" />
        <v-btn icon="mdi-delete-outline" variant="text" size="small" color="error" @click="openDelete(item)" />
      </template>
    </v-data-table>

    <OcWorkspaceAutomationDialog
      ref="dialogRef"
      v-model="dialog"
      :edit-id="editId"
      :board-items="boardItems"
      :type-items="typeItems"
      :transition-items="transitionItems"
      :saving="saving"
      @save="onSave"
    />

    <OcWorkspaceAutomationSimulateDialog
      v-model="simulateDialog"
      :automation="simulateTarget"
      :workspace-id="workspaceId"
      :board-name-by-id="boardNameById"
      :type-name-by-id="typeNameById"
      @executed="onSimulated"
    />

    <v-dialog v-model="deleteDialog" max-width="440" persistent>
      <v-card>
        <v-card-title>{{ t('operationCore.workspaceDefinitions.automations.deleteTitle') }}</v-card-title>
        <v-card-text>
          {{ t('operationCore.workspaceDefinitions.automations.deleteBody') }}
          <strong v-if="deleteTarget">{{ deleteTarget.name }}</strong>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn color="error" :loading="deleting" @click="confirmDelete">
            {{ t('operationCore.definitions.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
