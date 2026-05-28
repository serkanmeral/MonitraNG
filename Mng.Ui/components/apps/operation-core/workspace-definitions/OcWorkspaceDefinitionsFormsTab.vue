<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcFormPreviewDialog from '@/components/apps/operation-core/OcFormPreviewDialog.vue';
import OcWorkspaceFormLayoutEditor from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceFormLayoutEditor.vue';
import OcWorkspaceFormFieldPolicyEditor from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceFormFieldPolicyEditor.vue';
import {
  OC_FORM_LAYOUT_CORE_FIELD_KEYS,
  resolveOcCoreFieldCardinality,
} from '@/utils/ocFieldDefinitions';
import {
  DEFAULT_OC_FORM_DIALOG_MAX_WIDTH,
  ocFormDialogWidthSelectItems,
} from '@/utils/ocFormLayout';
import {
  resolveOcCoreFieldType,
  resolveOcFieldDisplayLabel,
  resolveOcFieldEditorLabel,
} from '@/utils/ocFormFieldLabels';
import {
  buildFormPreviewContextFromDraft,
  buildOcFormLayoutPayload,
  ocCreateForm,
  ocDeleteForm,
  ocExtractDgErrorMessage,
  ocListBoardsForWorkspace,
  ocListFormsForWorkspace,
  ocListPoolFieldsForWorkspace,
  ocListPrioritiesForWorkspace,
  ocListStateFlowsForWorkspace,
  ocListStatesForWorkspace,
  ocListWorkItemTypesForWorkspace,
  ocUpdateForm,
} from '@/services/operationCoreService';
import type {
  OcFormRuntimeContext,
  OpField,
  OpForm,
  OpFormFieldBehavior,
  OpFormLayoutSection,
  OpStateFlow,
} from '@/types/apps/operationCore';
const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const previewLoading = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);

const forms = ref<OpForm[]>([]);
const poolFields = ref<OpField[]>([]);
const typeItems = ref<{ value: string; title: string }[]>([]);
const flowItems = ref<{ value: string; title: string }[]>([]);
const stateFlows = ref<OpStateFlow[]>([]);
const stateItems = ref<{ value: string; title: string }[]>([]);
const priorityItems = ref<{ value: string; title: string }[]>([]);
const boardItems = ref<{ value: string; title: string }[]>([]);

const dialog = ref(false);
const previewDialog = ref(false);
const previewContext = ref<OcFormRuntimeContext | null>(null);
const previewValues = ref<Record<string, unknown>>({});
const editId = ref<string | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<OpForm | null>(null);
const editorTab = ref<'general' | 'layout' | 'fieldPolicies'>('general');

const defaultBehavior = (): OpFormFieldBehavior => ({
  visible: true,
  required: false,
  readonly: false,
  masked: false,
});

const defaultForm = () => ({
  name: '',
  description: '',
  formHeading: '',
  formIntro: '',
  dialogMaxWidth: DEFAULT_OC_FORM_DIALOG_MAX_WIDTH,
  defaultTypeId: '' as string,
  defaultStateFlowId: '' as string,
  defaultStateId: '' as string,
  defaultPriorityId: '' as string,
  isDefault: false,
  sections: [
    {
      key: 'main',
      title: '',
      cols: 12,
      fields: ['title', 'description', 'typeId', 'assignee', 'priorityId', 'boardId'] as string[],
    },
  ] as OpFormLayoutSection[],
  fieldCols: {
    title: 12,
    description: 12,
    typeId: 6,
    assignee: 6,
    priorityId: 6,
    boardId: 6,
  } as Record<string, number>,
  fieldBehaviors: {} as Record<string, OpFormFieldBehavior>,
  defaultValues: {} as Record<string, unknown>,
});

const form = ref(defaultForm());

const dialogWidthItems = computed(() =>
  ocFormDialogWidthSelectItems((px) =>
    t('operationCore.workspaceDefinitions.forms.dialogWidthPx', { px })
  )
);

const layoutFieldItems = computed(() => {
  const poolKeys = poolFields.value.map((f) => f.key).filter(Boolean);
  const allKeys = [...new Set([...OC_FORM_LAYOUT_CORE_FIELD_KEYS, ...poolKeys])];
  return allKeys.map((key) => {
    const pool = poolFields.value.find((f) => f.key === key);
    const displayLabel = resolveOcFieldDisplayLabel(key, {
      poolLabel: pool?.label,
      translate: t,
    });
    return {
      value: key,
      title: resolveOcFieldEditorLabel(key, { poolLabel: pool?.label, translate: t }),
      displayLabel,
      fieldType: pool?.fieldType ?? resolveOcCoreFieldType(key),
      cardinality: pool?.cardinality ?? resolveOcCoreFieldCardinality(key),
      relationDataset: pool?.relationDatasetName ?? null,
    };
  });
});

const allLayoutFieldKeys = computed(() => {
  const keys: string[] = [];
  const seen = new Set<string>();
  for (const s of form.value.sections) {
    for (const k of s.fields) {
      if (!seen.has(k)) {
        seen.add(k);
        keys.push(k);
      }
    }
  }
  return keys;
});

const tableHeaders = computed(() => [
  { title: t('operationCore.workspaceDefinitions.forms.colName'), key: 'name', sortable: true },
  { title: t('operationCore.workspaceDefinitions.forms.colType'), key: 'defaultTypeId', sortable: false },
  { title: t('operationCore.workspaceDefinitions.forms.colFlow'), key: 'defaultStateFlowId', sortable: false },
  { title: t('operationCore.workspaceDefinitions.forms.colFields'), key: 'layoutSections', sortable: false },
  { title: t('operationCore.workspaceDefinitions.forms.colDefault'), key: 'isDefault', sortable: false },
  { title: t('operationCore.workspaceDefinitions.forms.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

function relName(items: { value: string; title: string }[], id: string | null | undefined) {
  if (!id) return '—';
  return items.find((i) => i.value === id)?.title ?? id;
}

function fieldCount(row: OpForm) {
  const keys = new Set<string>();
  for (const s of row.layoutSections) {
    for (const f of s.fields) keys.add(f);
  }
  return keys.size;
}

function syncBehaviorsFromSections() {
  const next = { ...form.value.fieldBehaviors };
  for (const key of allLayoutFieldKeys.value) {
    if (!next[key]) {
      next[key] = {
        ...defaultBehavior(),
        required: key === 'title' || key === 'typeId',
      };
    }
  }
  for (const key of Object.keys(next)) {
    if (!allLayoutFieldKeys.value.includes(key)) delete next[key];
  }
  form.value.fieldBehaviors = next;
}

function ensureDefaultValuesKeys() {
  const next = { ...form.value.defaultValues };
  for (const key of allLayoutFieldKeys.value) {
    if (!(key in next)) next[key] = '';
  }
  for (const key of Object.keys(next)) {
    if (!allLayoutFieldKeys.value.includes(key)) delete next[key];
  }
  form.value.defaultValues = next;
}

watch(allLayoutFieldKeys, () => {
  syncBehaviorsFromSections();
  ensureDefaultValuesKeys();
});

function buildPayload() {
  const layout = buildOcFormLayoutPayload({
    formHeading: form.value.formHeading,
    formIntro: form.value.formIntro,
    dialogMaxWidth: form.value.dialogMaxWidth,
    sections: form.value.sections,
    fieldCols: form.value.fieldCols,
  });

  const fieldBehaviors: Record<string, Record<string, boolean>> = {};
  for (const key of allLayoutFieldKeys.value) {
    const b = form.value.fieldBehaviors[key] ?? defaultBehavior();
    fieldBehaviors[key] = {
      visible: b.visible,
      required: b.required,
      readonly: b.readonly,
      masked: b.masked,
    };
  }

  const defaultValues: Record<string, unknown> = {};
  for (const key of allLayoutFieldKeys.value) {
    const raw = form.value.defaultValues[key];
    if (raw !== undefined && raw !== null && String(raw).trim() !== '') {
      defaultValues[key] = raw;
    }
  }

  return {
    name: form.value.name.trim(),
    workspaceId: props.workspaceId,
    description: form.value.description.trim() || null,
    defaultTypeId: form.value.defaultTypeId || null,
    defaultStateFlowId: form.value.defaultStateFlowId || null,
    defaultStateId: form.value.defaultStateId || null,
    defaultPriorityId: form.value.defaultPriorityId || null,
    isDefault: form.value.isDefault,
    layout,
    fieldBehaviors,
    defaultValues: Object.keys(defaultValues).length ? defaultValues : null,
  };
}

async function loadAll() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    const [formRows, types, flows, states, priorities, pool, boards] = await Promise.all([
      ocListFormsForWorkspace(props.workspaceId),
      ocListWorkItemTypesForWorkspace(props.workspaceId, { fallbackAll: true }),
      ocListStateFlowsForWorkspace(props.workspaceId),
      ocListStatesForWorkspace(props.workspaceId, { fallbackAll: true }),
      ocListPrioritiesForWorkspace(props.workspaceId, { fallbackAll: true }),
      ocListPoolFieldsForWorkspace(props.workspaceId),
      ocListBoardsForWorkspace(props.workspaceId),
    ]);
    forms.value = formRows;
    poolFields.value = pool;
    typeItems.value = types.map((x) => ({ value: x.__dataId, title: x.name }));
    stateFlows.value = flows;
    flowItems.value = flows.map((x) => ({ value: x.__dataId, title: x.name }));
    stateItems.value = states.map((x) => ({ value: x.__dataId, title: x.name }));
    priorityItems.value = priorities.map((x) => ({ value: x.__dataId, title: x.name }));
    boardItems.value = boards.map((x) => ({ value: x.__dataId, title: x.name }));
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.forms.loadError')
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
  if (typeItems.value[0]) next.defaultTypeId = typeItems.value[0].value;
  if (flowItems.value[0]) next.defaultStateFlowId = flowItems.value[0].value;
  const initial = stateItems.value[0]?.value ?? '';
  if (initial) next.defaultStateId = initial;
  syncBehaviorsFromSections();
  ensureDefaultValuesKeys();
  form.value = next;
  editorTab.value = 'general';
  dialog.value = true;
}

function openEdit(row: OpForm) {
  editId.value = row.__dataId;
  form.value = {
    name: row.name,
    description: row.description ?? '',
    formHeading: row.formHeading ?? '',
    formIntro: row.formIntro ?? '',
    dialogMaxWidth: row.dialogMaxWidth ?? DEFAULT_OC_FORM_DIALOG_MAX_WIDTH,
    defaultTypeId: row.defaultTypeId ?? '',
    defaultStateFlowId: row.defaultStateFlowId ?? '',
    defaultStateId: row.defaultStateId ?? '',
    defaultPriorityId: row.defaultPriorityId ?? '',
    isDefault: row.isDefault ?? false,
    sections:
      row.layoutSections.length > 0
        ? row.layoutSections.map((s) => ({
            key: s.key,
            title: s.title ?? '',
            cols: s.cols ?? row.sectionCols[s.key] ?? 12,
            fields: [...s.fields],
          }))
        : defaultForm().sections,
    fieldCols: { ...row.fieldCols },
    fieldBehaviors: { ...row.fieldBehaviors },
    defaultValues: { ...row.defaultValues },
  };
  syncBehaviorsFromSections();
  ensureDefaultValuesKeys();
  editorTab.value = 'general';
  dialog.value = true;
}

function openDelete(row: OpForm) {
  deleteTarget.value = row;
  deleteDialog.value = true;
}

async function clearOtherDefaults(exceptId: string) {
  for (const other of forms.value) {
    if (other.__dataId !== exceptId && other.isDefault) {
      await ocUpdateForm(other.__dataId, { isDefault: false });
    }
  }
}

async function submitForm() {
  if (!form.value.name.trim()) return;
  if (form.value.sections.every((s) => s.fields.length === 0)) {
    errorLocal.value = t('operationCore.workspaceDefinitions.forms.layoutRequired');
    editorTab.value = 'layout';
    return;
  }

  saving.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    const body = buildPayload();
    let formId = editId.value;
    if (formId) {
      await ocUpdateForm(formId, body);
    } else {
      formId = await ocCreateForm(body);
    }

    if (form.value.isDefault && formId) {
      await clearOtherDefaults(formId);
    }

    dialog.value = false;
    await loadAll();
    successLocal.value = t('operationCore.workspaceDefinitions.saveSuccess');
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.forms.saveError')
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
    await ocDeleteForm(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    deleteTarget.value = null;
    await loadAll();
    successLocal.value = t('operationCore.workspaceDefinitions.forms.deleteSuccess');
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.forms.deleteError')
    );
    deleteDialog.value = false;
  } finally {
    deleting.value = false;
  }
}

function openPreview() {
  if (form.value.sections.every((s) => s.fields.length === 0)) {
    errorLocal.value = t('operationCore.workspaceDefinitions.forms.layoutRequired');
    editorTab.value = 'layout';
    return;
  }

  errorLocal.value = null;
  previewLoading.value = true;
  try {
    previewContext.value = buildFormPreviewContextFromDraft({
      workspaceId: props.workspaceId,
      formName: form.value.name.trim() || t('operationCore.workspaceDefinitions.forms.unnamedForm'),
      formHeading: form.value.formHeading,
      formIntro: form.value.formIntro,
      dialogMaxWidth: form.value.dialogMaxWidth,
      defaultTypeId: form.value.defaultTypeId || undefined,
      sections: form.value.sections,
      fieldCols: form.value.fieldCols,
      fieldBehaviors: form.value.fieldBehaviors,
      defaultValues: form.value.defaultValues,
      layoutFieldItems: layoutFieldItems.value,
      types: typeItems.value.map((x) => ({ id: x.value, name: x.title })),
      formId: editId.value,
    });

    const values: Record<string, unknown> = {};
    for (const key of allLayoutFieldKeys.value) {
      const def = form.value.defaultValues[key];
      if (def !== undefined && def !== null && String(def).trim() !== '') {
        values[key] = def;
      }
    }
    if (form.value.defaultTypeId) values.typeId = form.value.defaultTypeId;
    previewValues.value = values;
    previewDialog.value = true;
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.forms.previewError')
    );
  } finally {
    previewLoading.value = false;
  }
}
</script>

<template>
  <div class="oc-ws-forms-tab pa-4 pa-md-6">
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
        {{ t('operationCore.workspaceDefinitions.forms.subtitle') }}
      </p>
      <v-btn color="primary" rounded="lg" class="text-none" @click="openCreate">
        <v-icon icon="mdi-plus" start />
        {{ t('operationCore.workspaceDefinitions.forms.newForm') }}
      </v-btn>
    </div>

    <div v-if="loading" class="d-flex justify-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <v-card v-else variant="outlined" rounded="lg">
      <v-data-table :headers="tableHeaders" :items="forms" class="oc-ws-forms-table">
        <template #[`item.defaultTypeId`]="{ item }">
          {{ relName(typeItems, item.defaultTypeId) }}
        </template>
        <template #[`item.defaultStateFlowId`]="{ item }">
          {{ relName(flowItems, item.defaultStateFlowId) }}
        </template>
        <template #[`item.layoutSections`]="{ item }">
          <v-chip size="small" variant="tonal" rounded="lg">
            {{ fieldCount(item) }}
          </v-chip>
        </template>
        <template #[`item.isDefault`]="{ item }">
          <v-icon v-if="item.isDefault" icon="mdi-star" size="18" color="warning" />
          <span v-else class="text-medium-emphasis">—</span>
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

    <v-dialog v-model="dialog" max-width="1080" scrollable persistent>
      <v-card rounded="xl" class="oc-form-editor-dialog">
        <v-card-title class="text-h6 d-flex flex-wrap align-center gap-2 px-4 pt-4">
          <span>
            {{
              editId
                ? t('operationCore.workspaceDefinitions.forms.editForm')
                : t('operationCore.workspaceDefinitions.forms.newForm')
            }}
          </span>
          <v-spacer />
          <v-btn
            variant="tonal"
            rounded="lg"
            class="text-none"
            :loading="previewLoading"
            @click="openPreview"
          >
            <v-icon icon="mdi-eye-outline" start />
            {{ t('operationCore.workspaceDefinitions.forms.preview') }}
          </v-btn>
        </v-card-title>

        <v-tabs v-model="editorTab" class="px-4" color="primary" density="comfortable">
          <v-tab value="general" class="text-none">
            {{ t('operationCore.workspaceDefinitions.forms.tabGeneral') }}
          </v-tab>
          <v-tab value="layout" class="text-none">
            {{ t('operationCore.workspaceDefinitions.forms.tabLayout') }}
          </v-tab>
          <v-tab value="fieldPolicies" class="text-none">
            {{ t('operationCore.workspaceDefinitions.forms.tabFieldPolicies') }}
          </v-tab>
        </v-tabs>

        <v-divider />

        <v-card-text class="px-4 py-4" style="min-height: 420px">
          <v-window v-model="editorTab">
            <v-window-item value="general">
              <v-text-field
                v-model="form.name"
                :label="t('operationCore.workspaceDefinitions.forms.fieldName')"
                density="comfortable"
                required
              />
              <v-textarea
                v-model="form.description"
                class="mt-3"
                :label="t('operationCore.workspaceDefinitions.forms.fieldDescription')"
                rows="2"
                auto-grow
                density="comfortable"
                variant="outlined"
              />
              <v-divider class="my-4" />
              <p class="text-subtitle-2 font-weight-medium mb-2">
                {{ t('operationCore.workspaceDefinitions.forms.formHeaderBlock') }}
              </p>
              <v-text-field
                v-model="form.formHeading"
                :label="t('operationCore.workspaceDefinitions.forms.fieldFormHeading')"
                density="comfortable"
                variant="outlined"
                hide-details="auto"
                class="mb-3"
              />
              <v-textarea
                v-model="form.formIntro"
                :label="t('operationCore.workspaceDefinitions.forms.fieldFormIntro')"
                rows="2"
                auto-grow
                density="comfortable"
                variant="outlined"
                hide-details="auto"
              />
              <v-select
                v-model="form.dialogMaxWidth"
                class="mt-3"
                :items="dialogWidthItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.workspaceDefinitions.forms.fieldDialogWidth')"
                :hint="t('operationCore.workspaceDefinitions.forms.fieldDialogWidthHint')"
                persistent-hint
                density="comfortable"
                variant="outlined"
              />
              <v-divider class="my-4" />
              <p class="text-subtitle-2 font-weight-medium mb-3">
                {{ t('operationCore.workspaceDefinitions.forms.defaultsBlockTitle') }}
              </p>
              <v-row dense>
                <v-col cols="12" md="6">
                  <v-select
                    v-model="form.defaultTypeId"
                    :items="typeItems"
                    item-title="title"
                    item-value="value"
                    :label="t('operationCore.workspaceDefinitions.forms.fieldDefaultType')"
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
                    :label="t('operationCore.workspaceDefinitions.forms.fieldDefaultFlow')"
                    density="comfortable"
                    clearable
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-select
                    v-model="form.defaultStateId"
                    :items="stateItems"
                    item-title="title"
                    item-value="value"
                    :label="t('operationCore.workspaceDefinitions.forms.fieldDefaultState')"
                    density="comfortable"
                    clearable
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-select
                    v-model="form.defaultPriorityId"
                    :items="priorityItems"
                    item-title="title"
                    item-value="value"
                    :label="t('operationCore.workspaceDefinitions.forms.fieldDefaultPriority')"
                    density="comfortable"
                    clearable
                  />
                </v-col>
              </v-row>
              <v-checkbox
                v-model="form.isDefault"
                class="mt-2"
                :label="t('operationCore.workspaceDefinitions.forms.fieldDefault')"
                density="comfortable"
                hide-details
              />
            </v-window-item>

            <v-window-item value="layout">
              <h4 class="text-subtitle-2 font-weight-medium mb-1">
                {{ t('operationCore.workspaceDefinitions.forms.layoutTitle') }}
              </h4>
              <p class="text-caption text-medium-emphasis mb-4">
                {{ t('operationCore.workspaceDefinitions.forms.layoutHint') }}
              </p>
              <OcWorkspaceFormLayoutEditor
                :sections="form.sections"
                :field-cols="form.fieldCols"
                :layout-field-items="layoutFieldItems"
                @update:sections="(v) => (form.sections = v)"
                @update:field-cols="(v) => (form.fieldCols = v)"
              />
            </v-window-item>

            <v-window-item value="fieldPolicies">
              <OcWorkspaceFormFieldPolicyEditor
                v-model:field-behaviors="form.fieldBehaviors"
                v-model:default-values="form.defaultValues"
                :workspace-id="workspaceId"
                :layout-field-keys="allLayoutFieldKeys"
                :layout-field-items="layoutFieldItems"
                :default-state-flow-id="form.defaultStateFlowId"
                :default-type-id="form.defaultTypeId"
                :state-flows="stateFlows"
                :type-items="typeItems"
                :priority-items="priorityItems"
                :state-items="stateItems"
                :board-items="boardItems"
              />
            </v-window-item>
          </v-window>
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

    <OcFormPreviewDialog
      v-model="previewDialog"
      :context="previewContext"
      v-model:form-values="previewValues"
    />

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title>{{ t('operationCore.workspaceDefinitions.forms.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('operationCore.workspaceDefinitions.forms.deleteBody') }}</v-card-text>
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
