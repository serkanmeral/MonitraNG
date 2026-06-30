<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useOcWorkspaceMetadataCacheReload } from '@/composables/useOcWorkspaceMetadataCacheReload';
import {
  ocCreateStateFlow,
  ocDeleteStateFlow,

  ocGetWorkspace,
  ocListPoolFieldsForWorkspace,
  ocListStateFlowsForWorkspace,
  ocListStatesForWorkspace,
  ocUpdateStateFlow,
  ocUpdateWorkspace,
} from '@/services/operationCoreService';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import { OC_FORM_LAYOUT_CORE_FIELD_KEYS } from '@/utils/ocFieldDefinitions';
import { resolveOcFieldDisplayLabel } from '@/utils/ocFormFieldLabels';
import type { OpField, OpState, OpStateFlow, OpStateFlowTransition } from '@/types/apps/operationCore';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const metaCache = useOcWorkspaceMetadataCacheReload(() => props.workspaceId);

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);

const flows = ref<OpStateFlow[]>([]);
const states = ref<OpState[]>([]);
const poolFields = ref<OpField[]>([]);
const workspaceDefaultFlowId = ref<string | null>(null);

const dialog = ref(false);
const editId = ref<string | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<OpStateFlow | null>(null);

const defaultForm = () => ({
  name: '',
  description: '',
  initialStateId: '' as string,
  isDefault: false,
  isActive: true,
  sortOrder: '' as string,
  transitions: [] as OpStateFlowTransition[],
});

const form = ref(defaultForm());

const stateItems = computed(() =>
  states.value.map((s) => ({
    value: s.__dataId,
    title: s.name,
    subtitle: s.category,
  }))
);

const poolLabelByKey = computed(() => {
  const map = new Map<string, string>();
  for (const f of poolFields.value) {
    if (f.key) map.set(f.key, f.label ?? f.key);
  }
  return map;
});

const fieldKeyItems = computed(() => {
  const keys = [...OC_FORM_LAYOUT_CORE_FIELD_KEYS, ...poolFields.value.map((f) => f.key)];
  const seen = new Set<string>();
  const items: { value: string; title: string }[] = [];
  for (const key of keys) {
    if (!key || seen.has(key)) continue;
    seen.add(key);
    items.push({
      value: key,
      title: resolveOcFieldDisplayLabel(key, {
        poolLabel: poolLabelByKey.value.get(key) ?? null,
        translate: t,
      }),
    });
  }
  return items.sort((a, b) => a.title.localeCompare(b.title));
});

const tableHeaders = computed(() => [
  { title: t('operationCore.workspaceDefinitions.flows.colName'), key: 'name', sortable: true },
  { title: t('operationCore.workspaceDefinitions.flows.colInitialState'), key: 'initialStateId', sortable: false },
  { title: t('operationCore.workspaceDefinitions.flows.colTransitions'), key: 'transitions', sortable: false },
  { title: t('operationCore.workspaceDefinitions.flows.colDefault'), key: 'isDefault', sortable: false },
  { title: t('operationCore.workspaceDefinitions.flows.colActive'), key: 'isActive', sortable: false },
  { title: t('operationCore.workspaceDefinitions.flows.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

function stateName(stateId: string | null | undefined) {
  if (!stateId) return '—';
  return states.value.find((s) => s.__dataId === stateId)?.name ?? stateId;
}

function buildPayload(): Record<string, unknown> {
  const sortRaw = form.value.sortOrder.trim();
  const sortOrder = sortRaw === '' ? null : Number(sortRaw);
  const transitions = form.value.transitions.map((tr, idx) => ({
    transitionKey: tr.transitionKey.trim(),
    fromStateId: tr.fromStateId,
    toStateId: tr.toStateId,
    label: tr.label?.trim() || null,
    order: tr.order != null && Number.isFinite(tr.order) ? tr.order : idx,
    requiredFields: Array.isArray(tr.requiredFields) ? [...tr.requiredFields] : [],
    permissions: { groups: Array.isArray(tr.permissionGroups) ? [...tr.permissionGroups] : [] },
  }));

  return {
    name: form.value.name.trim(),
    workspaceId: props.workspaceId,
    description: form.value.description.trim() || null,
    initialStateId: form.value.initialStateId,
    isDefault: form.value.isDefault,
    isActive: form.value.isActive,
    sortOrder: Number.isFinite(sortOrder) ? sortOrder : null,
    transitions,
  };
}

async function loadAll() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    const [flowRows, stateRows, fieldRows, ws] = await Promise.all([
      ocListStateFlowsForWorkspace(props.workspaceId),
      ocListStatesForWorkspace(props.workspaceId, { fallbackAll: true }),
      ocListPoolFieldsForWorkspace(props.workspaceId),
      ocGetWorkspace(props.workspaceId),
    ]);
    flows.value = flowRows;
    states.value = stateRows;
    poolFields.value = fieldRows;
    workspaceDefaultFlowId.value = ws?.defaultStateFlowId ?? null;
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.flows.loadError');
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
  const initial =
    states.value.find((s) => s.isInitial)?.__dataId ??
    states.value[0]?.__dataId ??
    '';
  form.value = {
    ...defaultForm(),
    initialStateId: initial,
  };
  dialog.value = true;
}

function openEdit(row: OpStateFlow) {
  editId.value = row.__dataId;
  form.value = {
    name: row.name,
    description: row.description ?? '',
    initialStateId: row.initialStateId,
    isDefault: row.isDefault ?? false,
    isActive: row.isActive !== false,
    sortOrder: row.sortOrder != null ? String(row.sortOrder) : '',
    transitions: row.transitions.map((tr) => ({
      ...tr,
      requiredFields: Array.isArray(tr.requiredFields) ? [...tr.requiredFields] : [],
      permissionGroups: Array.isArray(tr.permissionGroups) ? [...tr.permissionGroups] : [],
    })),
  };
  dialog.value = true;
}

function openDelete(row: OpStateFlow) {
  deleteTarget.value = row;
  deleteDialog.value = true;
}

function addTransition() {
  const order = form.value.transitions.length;
  form.value.transitions.push({
    transitionKey: `transition_${order + 1}`,
    fromStateId: form.value.initialStateId || states.value[0]?.__dataId || '',
    toStateId: states.value[1]?.__dataId ?? states.value[0]?.__dataId ?? '',
    label: '',
    order,
    requiredFields: [],
    permissionGroups: [],
  });
}

function removeTransition(index: number) {
  form.value.transitions.splice(index, 1);
}

async function syncWorkspaceDefaultFlow(flowId: string) {
  await ocUpdateWorkspace(props.workspaceId, { defaultStateFlowId: flowId });
  workspaceDefaultFlowId.value = flowId;
}

async function submitForm() {
  if (!form.value.name.trim() || !form.value.initialStateId) return;
  if (form.value.transitions.some((tr) => !tr.transitionKey.trim() || !tr.fromStateId || !tr.toStateId)) {
    errorLocal.value = t('operationCore.workspaceDefinitions.flows.transitionInvalid');
    return;
  }

  saving.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    const body = buildPayload();
    let flowId = editId.value;

    if (flowId) {
      await ocUpdateStateFlow(flowId, body);
    } else {
      flowId = await ocCreateStateFlow(body);
      if (!flowId) {
        await loadAll();
        const created = flows.value.find((f) => f.name === form.value.name.trim());
        flowId = created?.__dataId ?? null;
      }
    }

    if (form.value.isDefault && flowId) {
      await syncWorkspaceDefaultFlow(flowId);
      for (const other of flows.value) {
        if (other.__dataId !== flowId && other.isDefault) {
          await ocUpdateStateFlow(other.__dataId, { isDefault: false });
        }
      }
    }

    dialog.value = false;
    await loadAll();
    await metaCache.applySaveSuccess(
      (msg) => {
        successLocal.value = msg;
      },
      t('operationCore.workspaceDefinitions.saveSuccess')
    );
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.flows.saveError');
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await ocDeleteStateFlow(deleteTarget.value.__dataId);
    if (workspaceDefaultFlowId.value === deleteTarget.value.__dataId) {
      await ocUpdateWorkspace(props.workspaceId, { defaultStateFlowId: null });
      workspaceDefaultFlowId.value = null;
    }
    deleteDialog.value = false;
    deleteTarget.value = null;
    await loadAll();
    await metaCache.applySaveSuccess(
      (msg) => {
        successLocal.value = msg;
      },
      t('operationCore.workspaceDefinitions.flows.deleteSuccess')
    );
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.flows.deleteError');
    deleteDialog.value = false;
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="oc-ws-flows-tab pa-4 pa-md-6">
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
      <div>
        <p class="text-body-2 text-medium-emphasis mb-0">
          {{ t('operationCore.workspaceDefinitions.flows.subtitle') }}
        </p>
        <p v-if="workspaceDefaultFlowId" class="text-caption text-medium-emphasis mb-0 mt-1">
          {{ t('operationCore.workspaceDefinitions.flows.workspaceDefault') }}
          <strong>{{
            flows.find((f) => f.__dataId === workspaceDefaultFlowId)?.name ?? workspaceDefaultFlowId
          }}</strong>
        </p>
      </div>
      <v-btn color="primary" rounded="lg" class="text-none" @click="openCreate">
        <v-icon icon="mdi-plus" start />
        {{ t('operationCore.workspaceDefinitions.flows.newFlow') }}
      </v-btn>
    </div>

    <div v-if="loading" class="d-flex justify-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <v-card v-else variant="outlined" rounded="lg">
      <v-data-table :headers="tableHeaders" :items="flows" class="oc-ws-flows-table">
        <template #[`item.initialStateId`]="{ item }">
          {{ stateName(item.initialStateId) }}
        </template>
        <template #[`item.transitions`]="{ item }">
          <v-chip size="small" variant="tonal" rounded="lg">
            {{ item.transitions.length }}
          </v-chip>
        </template>
        <template #[`item.isDefault`]="{ item }">
          <v-icon
            v-if="item.isDefault || item.__dataId === workspaceDefaultFlowId"
            icon="mdi-star"
            size="18"
            color="warning"
          />
          <span v-else class="text-medium-emphasis">—</span>
        </template>
        <template #[`item.isActive`]="{ item }">
          <v-chip
            :color="item.isActive !== false ? 'success' : 'secondary'"
            size="small"
            variant="tonal"
            rounded="lg"
            class="text-none"
          >
            {{
              item.isActive !== false
                ? t('operationCore.workspaceDefinitions.flows.activeYes')
                : t('operationCore.workspaceDefinitions.flows.activeNo')
            }}
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
              ? t('operationCore.workspaceDefinitions.flows.editFlow')
              : t('operationCore.workspaceDefinitions.flows.newFlow')
          }}
        </v-card-title>
        <v-card-text>
          <v-text-field
            v-model="form.name"
            :label="t('operationCore.workspaceDefinitions.flows.fieldName')"
            density="comfortable"
            required
          />
          <v-textarea
            v-model="form.description"
            class="mt-3"
            :label="t('operationCore.workspaceDefinitions.flows.fieldDescription')"
            rows="2"
            auto-grow
            density="comfortable"
            variant="outlined"
          />
          <v-select
            v-model="form.initialStateId"
            class="mt-3"
            :items="stateItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.workspaceDefinitions.flows.fieldInitialState')"
            :hint="t('operationCore.workspaceDefinitions.flows.initialStateHint')"
            persistent-hint
            density="comfortable"
          />
          <div class="d-flex flex-wrap gap-4 mt-3">
            <v-checkbox
              v-model="form.isDefault"
              :label="t('operationCore.workspaceDefinitions.flows.fieldDefault')"
              density="comfortable"
              hide-details
            />
            <v-checkbox
              v-model="form.isActive"
              :label="t('operationCore.workspaceDefinitions.flows.fieldActive')"
              density="comfortable"
              hide-details
            />
          </div>
          <v-text-field
            v-model="form.sortOrder"
            class="mt-3"
            type="number"
            :label="t('operationCore.workspaceDefinitions.flows.fieldSortOrder')"
            density="comfortable"
          />

          <v-divider class="my-5" />
          <div class="d-flex align-center justify-space-between mb-3">
            <h4 class="text-subtitle-2 font-weight-medium">
              {{ t('operationCore.workspaceDefinitions.flows.transitionsTitle') }}
            </h4>
            <v-btn size="small" variant="tonal" rounded="lg" class="text-none" @click="addTransition">
              <v-icon icon="mdi-plus" start />
              {{ t('operationCore.workspaceDefinitions.flows.addTransition') }}
            </v-btn>
          </div>
          <p class="text-caption text-medium-emphasis mb-3">
            {{ t('operationCore.workspaceDefinitions.flows.transitionsHint') }}
          </p>

          <v-alert v-if="form.transitions.length === 0" type="info" variant="tonal" density="compact">
            {{ t('operationCore.workspaceDefinitions.flows.noTransitions') }}
          </v-alert>

          <v-card
            v-for="(tr, idx) in form.transitions"
            :key="idx"
            variant="outlined"
            rounded="lg"
            class="mb-3 pa-3"
          >
            <div class="d-flex align-center justify-space-between mb-2">
              <span class="text-caption text-medium-emphasis">#{{ idx + 1 }}</span>
              <v-btn icon variant="text" size="x-small" color="error" @click="removeTransition(idx)">
                <v-icon icon="mdi-close" />
              </v-btn>
            </div>
            <v-row dense>
              <v-col cols="12" md="4">
                <v-text-field
                  v-model="tr.transitionKey"
                  :label="t('operationCore.workspaceDefinitions.flows.fieldTransitionKey')"
                  density="compact"
                  hide-details
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field
                  v-model="tr.label"
                  :label="t('operationCore.workspaceDefinitions.flows.fieldTransitionLabel')"
                  density="compact"
                  hide-details
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field
                  :model-value="tr.order != null ? String(tr.order) : String(idx)"
                  type="number"
                  :label="t('operationCore.workspaceDefinitions.flows.fieldTransitionOrder')"
                  density="compact"
                  hide-details
                  @update:model-value="(v) => (tr.order = v === '' ? idx : Number(v))"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-select
                  v-model="tr.fromStateId"
                  :items="stateItems"
                  item-title="title"
                  item-value="value"
                  :label="t('operationCore.workspaceDefinitions.flows.fieldFromState')"
                  density="compact"
                  hide-details
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-select
                  v-model="tr.toStateId"
                  :items="stateItems"
                  item-title="title"
                  item-value="value"
                  :label="t('operationCore.workspaceDefinitions.flows.fieldToState')"
                  density="compact"
                  hide-details
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-select
                  v-model="tr.requiredFields"
                  :items="fieldKeyItems"
                  item-title="title"
                  item-value="value"
                  :label="t('operationCore.workspaceDefinitions.flows.fieldRequiredFields')"
                  :hint="t('operationCore.workspaceDefinitions.flows.requiredFieldsHint')"
                  persistent-hint
                  multiple
                  chips
                  closable-chips
                  clearable
                  density="compact"
                />
              </v-col>
              <v-col cols="12" md="6">
                <MngDirectoryPickerField
                  v-model="tr.permissionGroups"
                  entity="group"
                  multiple
                  :label="t('operationCore.workspaceDefinitions.flows.fieldPermissionGroups')"
                  density="compact"
                  variant="outlined"
                  hide-details="auto"
                />
                <p class="text-caption text-medium-emphasis mt-1 mb-0">
                  {{ t('operationCore.workspaceDefinitions.flows.permissionGroupsHint') }}
                </p>
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
            :disabled="!form.name.trim() || !form.initialStateId"
            @click="submitForm"
          >
            {{ t('operationCore.definitions.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title>{{ t('operationCore.workspaceDefinitions.flows.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('operationCore.workspaceDefinitions.flows.deleteBody') }}</v-card-text>
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
