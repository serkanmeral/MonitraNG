<script setup lang="ts">
import { ref, computed, watch, onMounted, nextTick } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcWorkspaceBoardDialog from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceBoardDialog.vue';
import {
  ocCreateBoard,
  ocDeleteBoard,
  ocExtractDgErrorMessage,
  ocGetWorkspace,
  ocListBoardsForWorkspace,
  ocListDashboardsForWorkspace,
  ocListFormsForWorkspace,
  ocListPoolFieldsForWorkspace,
  ocListPrioritiesForWorkspace,
  ocListProfilesForWorkspace,
  ocListStateFlowsForWorkspace,
  ocListStatesForWorkspace,
  ocListWorkItemTypesForWorkspace,
  ocUpdateBoard,
} from '@/services/operationCoreService';
import { useGroupStore } from '@/stores/apps/group';
import type { OpBoard, OpField, OpPriority, OpState, OpStateFlow, OpWorkItemType } from '@/types/apps/operationCore';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();
const groupStore = useGroupStore();

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);

const boards = ref<OpBoard[]>([]);
const stateFlows = ref<OpStateFlow[]>([]);
const formItems = ref<{ value: string; title: string }[]>([]);
const dashboardItems = ref<{ value: string; title: string }[]>([]);
const flowItems = ref<{ value: string; title: string }[]>([]);
const stateItems = ref<{ value: string; title: string }[]>([]);
const stateCatalog = ref<OpState[]>([]);
const priorityCatalog = ref<OpPriority[]>([]);
const typeCatalog = ref<OpWorkItemType[]>([]);
const fieldCatalog = ref<OpField[]>([]);
const profileItems = ref<{ value: string; title: string }[]>([]);
const typeItems = ref<{ value: string; title: string }[]>([]);
const priorityItems = ref<{ value: string; title: string }[]>([]);
const enabledStateIds = ref<string[]>([]);

const groupItems = computed(() =>
  (groupStore.groups || []).map((g) => ({
    title: g.name,
    value: g.id || g.groupId,
  }))
);

const stateTitleById = computed(
  () => new Map(stateItems.value.map((s) => [s.value, s.title]))
);

const dialog = ref(false);
const dialogRef = ref<InstanceType<typeof OcWorkspaceBoardDialog> | null>(null);
const editId = ref<string | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<OpBoard | null>(null);

const tableHeaders = computed(() => [
  { title: t('operationCore.workspaceDefinitions.boards.colName'), key: 'name', sortable: true },
  { title: t('operationCore.workspaceDefinitions.boards.colViewType'), key: 'viewType', sortable: false },
  { title: t('operationCore.workspaceDefinitions.boards.colColumns'), key: 'columns', sortable: false },
  { title: t('operationCore.workspaceDefinitions.boards.colFlow'), key: 'defaultStateFlowId', sortable: false },
  { title: t('operationCore.workspaceDefinitions.boards.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

function relName(items: { value: string; title: string }[], id: string | null | undefined) {
  if (!id) return '—';
  return items.find((i) => i.value === id)?.title ?? id;
}

function columnLabels(board: OpBoard): string[] {
  return board.columns
    .filter((c) => c.stateId)
    .map((c) => c.title?.trim() || stateTitleById.value.get(c.stateId) || c.stateId);
}

async function loadAll() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    const [boardRows, forms, dashboardRows, flows, states, profiles, types, priorities, poolFields, ws] = await Promise.all([
      ocListBoardsForWorkspace(props.workspaceId),
      ocListFormsForWorkspace(props.workspaceId),
      ocListDashboardsForWorkspace(props.workspaceId),
      ocListStateFlowsForWorkspace(props.workspaceId),
      ocListStatesForWorkspace(props.workspaceId, { fallbackAll: true }),
      ocListProfilesForWorkspace(props.workspaceId),
      ocListWorkItemTypesForWorkspace(props.workspaceId, { fallbackAll: true }),
      ocListPrioritiesForWorkspace(props.workspaceId, { fallbackAll: true }),
      ocListPoolFieldsForWorkspace(props.workspaceId),
      ocGetWorkspace(props.workspaceId),
    ]);
    boards.value = boardRows;
    stateFlows.value = flows;
    formItems.value = forms.map((f) => ({ value: f.__dataId, title: f.name }));
    dashboardItems.value = dashboardRows.map((d) => ({ value: d.id, title: d.name }));
    flowItems.value = flows.map((f) => ({ value: f.__dataId, title: f.name }));
    stateItems.value = states.map((s) => ({ value: s.__dataId, title: s.name }));
    stateCatalog.value = states;
    priorityCatalog.value = priorities;
    typeCatalog.value = types;
    fieldCatalog.value = poolFields;
    profileItems.value = profiles.map((p) => ({ value: p.__dataId, title: p.name }));
    typeItems.value = types.map((ty) => ({ value: ty.__dataId, title: ty.name }));
    priorityItems.value = priorities.map((p) => ({ value: p.__dataId, title: p.name }));
    enabledStateIds.value = ws?.enabledStateIds ?? [];
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.boards.loadError')
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

async function openCreate() {
  editId.value = null;
  dialog.value = true;
  await nextTick();
  dialogRef.value?.setFormFromBoard(null);
}

async function openEdit(row: OpBoard) {
  editId.value = row.__dataId;
  dialog.value = true;
  await nextTick();
  dialogRef.value?.setFormFromBoard(row);
}

function openDelete(row: OpBoard) {
  deleteTarget.value = row;
  deleteDialog.value = true;
}

async function onDialogSave(body: Record<string, unknown>) {
  saving.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    if (editId.value) {
      await ocUpdateBoard(editId.value, body);
    } else {
      await ocCreateBoard(body);
    }
    dialog.value = false;
    await loadAll();
    successLocal.value = t('operationCore.workspaceDefinitions.saveSuccess');
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.boards.saveError')
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
    await ocDeleteBoard(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    deleteTarget.value = null;
    await loadAll();
    successLocal.value = t('operationCore.workspaceDefinitions.boards.deleteSuccess');
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.boards.deleteError')
    );
    deleteDialog.value = false;
  } finally {
    deleting.value = false;
  }
}

onMounted(() => {
  if (!groupStore.groups?.length) {
    void groupStore.fetchGroups();
  }
});
</script>

<template>
  <div class="oc-ws-boards-tab pa-4 pa-md-6">
    <v-alert
      v-if="errorLocal"
      type="error"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="errorLocal = null"
    >
      {{ errorLocal }}
    </v-alert>
    <v-alert
      v-if="successLocal"
      type="success"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="successLocal = null"
    >
      {{ successLocal }}
    </v-alert>

    <v-card variant="outlined" rounded="lg" class="mb-4 pa-4 pa-md-5 oc-board-tab-hero">
      <div class="d-flex flex-wrap align-start justify-space-between gap-3">
        <div class="flex-grow-1 min-width-0">
          <h3 class="text-subtitle-1 font-weight-bold mb-2">
            {{ t('operationCore.workspaceDefinitions.boards.pageTitle') }}
          </h3>
          <p class="text-body-2 text-medium-emphasis mb-3">
            {{ t('operationCore.workspaceDefinitions.boards.pageIntro') }}
          </p>
          <ol class="text-body-2 text-medium-emphasis ps-4 mb-0 oc-board-tab-steps">
            <li>{{ t('operationCore.workspaceDefinitions.boards.pageStep1') }}</li>
            <li>{{ t('operationCore.workspaceDefinitions.boards.pageStep2') }}</li>
            <li>{{ t('operationCore.workspaceDefinitions.boards.pageStep3') }}</li>
          </ol>
        </div>
        <v-btn color="primary" rounded="lg" class="text-none flex-shrink-0" @click="openCreate">
          <v-icon icon="mdi-plus" start />
          {{ t('operationCore.workspaceDefinitions.boards.newBoard') }}
        </v-btn>
      </div>
    </v-card>

    <div v-if="loading" class="d-flex justify-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <v-card v-else-if="boards.length === 0" variant="outlined" rounded="lg" class="pa-8 text-center">
      <v-icon icon="mdi-view-dashboard-outline" size="48" color="primary" class="mb-3 opacity-70" />
      <p class="text-body-1 mb-4">{{ t('operationCore.workspaceDefinitions.boards.emptyList') }}</p>
      <v-btn color="primary" rounded="lg" class="text-none" @click="openCreate">
        {{ t('operationCore.workspaceDefinitions.boards.newBoard') }}
      </v-btn>
    </v-card>

    <v-card v-else variant="outlined" rounded="lg">
      <v-data-table :headers="tableHeaders" :items="boards" class="oc-ws-boards-table">
        <template #[`item.viewType`]="{ item }">
          <v-chip size="small" variant="tonal" rounded="lg" class="text-none">
            {{ t(`operationCore.workspaceDefinitions.boards.viewType.${item.viewType || 'list'}`) }}
          </v-chip>
        </template>
        <template #[`item.columns`]="{ item }">
          <div class="d-flex flex-wrap gap-1 py-1">
            <v-chip
              v-for="(label, idx) in columnLabels(item)"
              :key="`${item.__dataId}-${idx}`"
              size="x-small"
              variant="outlined"
              rounded="lg"
            >
              {{ label }}
            </v-chip>
            <span v-if="columnLabels(item).length === 0" class="text-medium-emphasis">—</span>
          </div>
        </template>
        <template #[`item.defaultStateFlowId`]="{ item }">
          {{ relName(flowItems, item.defaultStateFlowId) }}
        </template>
        <template #[`item.actions`]="{ item }">
          <v-btn icon variant="text" size="small" @click="openEdit(item)">
            <v-icon icon="mdi-pencil-outline" />
          </v-btn>
          <v-btn icon variant="text" size="small" color="error" @click="openDelete(item)">
            <v-icon icon="mdi-delete-outline" />
          </v-btn>
        </template>
      </v-data-table>
    </v-card>

    <OcWorkspaceBoardDialog
      ref="dialogRef"
      v-model="dialog"
      :edit-id="editId"
      :workspace-id="workspaceId"
      :state-flows="stateFlows"
      :form-items="formItems"
      :dashboard-items="dashboardItems"
      :flow-items="flowItems"
      :state-items="stateItems"
      :state-catalog="stateCatalog"
      :priority-catalog="priorityCatalog"
      :type-catalog="typeCatalog"
      :field-catalog="fieldCatalog"
      :profile-items="profileItems"
      :type-items="typeItems"
      :priority-items="priorityItems"
      :group-items="groupItems"
      :enabled-state-ids="enabledStateIds"
      :saving="saving"
      @save="onDialogSave"
    />

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title>{{ t('operationCore.workspaceDefinitions.boards.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('operationCore.workspaceDefinitions.boards.deleteBody') }}</v-card-text>
        <v-card-actions class="px-4 pb-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialog = false">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn
            color="error"
            variant="flat"
            rounded="lg"
            class="text-none"
            :loading="deleting"
            @click="confirmDelete"
          >
            {{ t('operationCore.definitions.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.oc-board-tab-hero {
  background: linear-gradient(135deg, rgba(var(--v-theme-primary), 0.06) 0%, transparent 60%);
}

.oc-board-tab-steps li + li {
  margin-top: 0.25rem;
}
</style>
