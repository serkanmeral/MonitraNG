<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ocCreateBoard,
  ocDeleteBoard,
  ocExtractDgErrorMessage,
  ocListBoardsForWorkspace,
  ocListFormsForWorkspace,
  ocListStateFlowsForWorkspace,
  ocListStatesForWorkspace,
  ocUpdateBoard,
} from '@/services/operationCoreService';
import type { OpBoard, OpBoardColumnConfig } from '@/types/apps/operationCore';
import { OC_BOARD_VIEW_TYPE_VALUES } from '@/types/apps/operationCore';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);

const boards = ref<OpBoard[]>([]);
const formItems = ref<{ value: string; title: string }[]>([]);
const flowItems = ref<{ value: string; title: string }[]>([]);
const stateItems = ref<{ value: string; title: string }[]>([]);

const cardFieldOptions = [
  { value: 'title', title: 'title' },
  { value: 'key', title: 'key' },
  { value: 'assignee', title: 'assignee' },
  { value: 'priorityId', title: 'priorityId' },
  { value: 'typeId', title: 'typeId' },
  { value: 'stateId', title: 'stateId' },
];

const viewTypeItems = computed(() =>
  OC_BOARD_VIEW_TYPE_VALUES.map((value) => ({
    value,
    title: t(`operationCore.workspaceDefinitions.boards.viewType.${value}`),
  }))
);

const dialog = ref(false);
const editId = ref<string | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<OpBoard | null>(null);

const defaultForm = () => ({
  name: '',
  viewType: 'list' as string,
  defaultFormId: '' as string,
  defaultStateFlowId: '' as string,
  visibleFields: ['title', 'assignee', 'priorityId', 'key'] as string[],
  columns: [] as OpBoardColumnConfig[],
});

const form = ref(defaultForm());

const tableHeaders = computed(() => [
  { title: t('operationCore.workspaceDefinitions.boards.colName'), key: 'name', sortable: true },
  { title: t('operationCore.workspaceDefinitions.boards.colViewType'), key: 'viewType', sortable: false },
  { title: t('operationCore.workspaceDefinitions.boards.colForm'), key: 'defaultFormId', sortable: false },
  { title: t('operationCore.workspaceDefinitions.boards.colColumns'), key: 'columns', sortable: false },
  { title: t('operationCore.workspaceDefinitions.boards.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

function relName(items: { value: string; title: string }[], id: string | null | undefined) {
  if (!id) return '—';
  return items.find((i) => i.value === id)?.title ?? id;
}

function buildPayload() {
  const columns = form.value.columns
    .filter((c) => c.stateId)
    .map((c) => ({
      stateId: c.stateId,
      title: c.title?.trim() || null,
      queryKey: c.queryKey?.trim() || 'wi_board_column',
    }));

  return {
    name: form.value.name.trim(),
    workspaceId: props.workspaceId,
    viewType: form.value.viewType || 'list',
    defaultFormId: form.value.defaultFormId || null,
    defaultStateFlowId: form.value.defaultStateFlowId || null,
    visibleFields: form.value.visibleFields.length ? form.value.visibleFields : ['title', 'key'],
    config: { columns },
  };
}

async function loadAll() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    const [boardRows, forms, flows, states] = await Promise.all([
      ocListBoardsForWorkspace(props.workspaceId),
      ocListFormsForWorkspace(props.workspaceId),
      ocListStateFlowsForWorkspace(props.workspaceId),
      ocListStatesForWorkspace(props.workspaceId, { fallbackAll: true }),
    ]);
    boards.value = boardRows;
    formItems.value = forms.map((f) => ({ value: f.__dataId, title: f.name }));
    flowItems.value = flows.map((f) => ({ value: f.__dataId, title: f.name }));
    stateItems.value = states.map((s) => ({ value: s.__dataId, title: s.name }));
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

function openCreate() {
  editId.value = null;
  const next = defaultForm();
  if (formItems.value[0]) next.defaultFormId = formItems.value[0].value;
  if (flowItems.value[0]) next.defaultStateFlowId = flowItems.value[0].value;
  if (stateItems.value.length >= 3) {
    next.columns = stateItems.value.slice(0, 3).map((s, i) => ({
      stateId: s.value,
      title: s.title,
      queryKey: 'wi_board_column',
    }));
  } else if (stateItems.value[0]) {
    next.columns = [
      {
        stateId: stateItems.value[0].value,
        title: stateItems.value[0].title,
        queryKey: 'wi_board_column',
      },
    ];
  }
  form.value = next;
  dialog.value = true;
}

function openEdit(row: OpBoard) {
  editId.value = row.__dataId;
  form.value = {
    name: row.name,
    viewType: row.viewType ?? 'list',
    defaultFormId: row.defaultFormId ?? '',
    defaultStateFlowId: row.defaultStateFlowId ?? '',
    visibleFields: row.visibleFields.length ? [...row.visibleFields] : defaultForm().visibleFields,
    columns: row.columns.length ? row.columns.map((c) => ({ ...c })) : [],
  };
  dialog.value = true;
}

function openDelete(row: OpBoard) {
  deleteTarget.value = row;
  deleteDialog.value = true;
}

function addColumn() {
  const stateId = stateItems.value[form.value.columns.length % stateItems.value.length]?.value ?? '';
  form.value.columns.push({
    stateId,
    title: '',
    queryKey: 'wi_board_column',
  });
}

function removeColumn(index: number) {
  form.value.columns.splice(index, 1);
}

async function submitForm() {
  if (!form.value.name.trim()) return;
  if (form.value.columns.length === 0) {
    errorLocal.value = t('operationCore.workspaceDefinitions.boards.columnsRequired');
    return;
  }

  saving.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    const body = buildPayload();
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

    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('operationCore.workspaceDefinitions.boards.subtitle') }}
      </p>
      <v-btn color="primary" rounded="lg" class="text-none" @click="openCreate">
        <v-icon icon="mdi-plus" start />
        {{ t('operationCore.workspaceDefinitions.boards.newBoard') }}
      </v-btn>
    </div>

    <div v-if="loading" class="d-flex justify-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <v-card v-else variant="outlined" rounded="lg">
      <v-data-table :headers="tableHeaders" :items="boards" class="oc-ws-boards-table">
        <template #[`item.viewType`]="{ item }">
          <v-chip size="small" variant="tonal" rounded="lg" class="text-none">
            {{ t(`operationCore.workspaceDefinitions.boards.viewType.${item.viewType || 'list'}`) }}
          </v-chip>
        </template>
        <template #[`item.defaultFormId`]="{ item }">
          {{ relName(formItems, item.defaultFormId) }}
        </template>
        <template #[`item.columns`]="{ item }">
          <v-chip size="small" variant="tonal" rounded="lg">
            {{ item.columns.length }}
          </v-chip>
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

    <v-dialog v-model="dialog" max-width="920" scrollable>
      <v-card rounded="xl">
        <v-card-title class="text-h6">
          {{
            editId
              ? t('operationCore.workspaceDefinitions.boards.editBoard')
              : t('operationCore.workspaceDefinitions.boards.newBoard')
          }}
        </v-card-title>
        <v-card-text>
          <v-text-field
            v-model="form.name"
            :label="t('operationCore.workspaceDefinitions.boards.fieldName')"
            density="comfortable"
            required
          />
          <v-row dense class="mt-1">
            <v-col cols="12" md="6">
              <v-select
                v-model="form.viewType"
                :items="viewTypeItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.workspaceDefinitions.boards.fieldViewType')"
                density="comfortable"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-select
                v-model="form.defaultFormId"
                :items="formItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.workspaceDefinitions.boards.fieldDefaultForm')"
                density="comfortable"
                clearable
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-select
                v-model="form.defaultStateFlowId"
                :items="flowItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.workspaceDefinitions.boards.fieldDefaultFlow')"
                density="comfortable"
                clearable
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-select
                v-model="form.visibleFields"
                :items="cardFieldOptions"
                item-title="title"
                item-value="value"
                :label="t('operationCore.workspaceDefinitions.boards.fieldVisibleFields')"
                density="comfortable"
                multiple
                chips
                closable-chips
              />
            </v-col>
          </v-row>

          <v-divider class="my-5" />
          <div class="d-flex align-center justify-space-between mb-3">
            <h4 class="text-subtitle-2 font-weight-medium">
              {{ t('operationCore.workspaceDefinitions.boards.columnsTitle') }}
            </h4>
            <v-btn size="small" variant="tonal" rounded="lg" class="text-none" @click="addColumn">
              <v-icon icon="mdi-plus" start />
              {{ t('operationCore.workspaceDefinitions.boards.addColumn') }}
            </v-btn>
          </div>
          <p class="text-caption text-medium-emphasis mb-3">
            {{ t('operationCore.workspaceDefinitions.boards.columnsHint') }}
          </p>

          <v-alert v-if="form.columns.length === 0" type="info" variant="tonal" density="compact">
            {{ t('operationCore.workspaceDefinitions.boards.noColumns') }}
          </v-alert>

          <v-card
            v-for="(col, idx) in form.columns"
            :key="idx"
            variant="outlined"
            rounded="lg"
            class="mb-3 pa-3"
          >
            <div class="d-flex align-center justify-space-between mb-2">
              <span class="text-caption text-medium-emphasis">#{{ idx + 1 }}</span>
              <v-btn icon variant="text" size="x-small" color="error" @click="removeColumn(idx)">
                <v-icon icon="mdi-close" />
              </v-btn>
            </div>
            <v-row dense>
              <v-col cols="12" md="5">
                <v-select
                  v-model="col.stateId"
                  :items="stateItems"
                  item-title="title"
                  item-value="value"
                  :label="t('operationCore.workspaceDefinitions.boards.fieldColumnState')"
                  density="compact"
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field
                  v-model="col.title"
                  :label="t('operationCore.workspaceDefinitions.boards.fieldColumnTitle')"
                  density="compact"
                />
              </v-col>
              <v-col cols="12" md="3">
                <v-text-field
                  :model-value="col.queryKey ?? 'wi_board_column'"
                  :label="t('operationCore.workspaceDefinitions.boards.fieldQueryKey')"
                  density="compact"
                  hint="wi_board_column"
                  persistent-hint
                  @update:model-value="(v) => (col.queryKey = v || 'wi_board_column')"
                />
              </v-col>
            </v-row>
          </v-card>
        </v-card-text>
        <v-card-actions class="px-4 pb-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="dialog = false">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            rounded="lg"
            class="text-none"
            :loading="saving"
            :disabled="!form.name.trim()"
            @click="submitForm"
          >
            {{ t('operationCore.definitions.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

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
