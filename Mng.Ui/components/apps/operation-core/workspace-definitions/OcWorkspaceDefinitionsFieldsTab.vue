<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useOcWorkspaceMetadataCacheReload } from '@/composables/useOcWorkspaceMetadataCacheReload';
import { useOcLookupDatasetCatalog } from '@/composables/useOcLookupDatasetCatalog';
import { useDatasetStore } from '@/stores/apps/dataset';
import {
  ocCreateField,
  ocDeleteField,

  ocGetWorkspace,
  ocListGlobalPoolFields,
  ocListWorkspaceScopedFields,
  ocUpdateField,
  ocUpdateWorkspace,
} from '@/services/operationCoreService';
import type { OpField } from '@/types/apps/operationCore';
import {
  OC_FIELD_CATEGORIES,
  OC_FIELD_KEY_PATTERN,
  OC_POOL_FIELD_TYPE_VALUES,
  parseOcFieldOptions,
  stringifyOcFieldOptions,
} from '@/utils/ocFieldDefinitions';
import {
  buildOcFileFieldOptionsPayload,
  normalizeOcFileExtensionList,
  OC_FILE_EXTENSION_PRESETS,
  parseOcFileFieldOptions,
} from '@/utils/ocFileFieldOptions';
import {
  buildLookupFieldKeyItems,
  buildOcLookupFieldOptionsPayload,
  OC_LOOKUP_DEFAULT_LABEL_FIELD,
  OC_LOOKUP_DEFAULT_PAGE_SIZE,
  OC_LOOKUP_DEFAULT_VALUE_FIELD,
  parseOcLookupFromFieldOptions,
  type OcLookupPresentation,
  type OcLookupStaticItem,
} from '@/utils/ocLookupFieldOptions';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const metaCache = useOcWorkspaceMetadataCacheReload(() => props.workspaceId);
const datasetStore = useDatasetStore();
const {
  load: loadLookupCatalog,
  loading: lookupCatalogLoading,
  selectableDatasets: lookupSelectableDatasets,
} = useOcLookupDatasetCatalog();

const loading = ref(true);
const savingSelection = ref(false);
const savingField = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);
const optionsError = ref<string | null>(null);

const globalFields = ref<OpField[]>([]);
const scopedFields = ref<OpField[]>([]);
const selectedFieldIds = ref<string[]>([]);

const dialog = ref(false);
const editId = ref<string | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<OpField | null>(null);

const defaultForm = () => ({
  key: '',
  label: '',
  fieldType: 'text' as string,
  category: 'classification' as string,
  cardinality: 'single' as string,
  description: '',
  sortOrder: '' as string,
  relationDatasetName: '',
  optionsJson: '',
  maxSizeMb: 5,
  allowedExtensions: [] as string[],
  isSensitive: false,
  lookupPresentation: 'autocomplete' as OcLookupPresentation,
  lookupValueField: OC_LOOKUP_DEFAULT_VALUE_FIELD,
  lookupLabelField: OC_LOOKUP_DEFAULT_LABEL_FIELD,
  lookupPageSize: OC_LOOKUP_DEFAULT_PAGE_SIZE,
  lookupFilter: '',
  lookupDependsOnFieldKey: '',
  lookupDependsOnFilterTemplate: '',
  staticItems: [] as OcLookupStaticItem[],
});

const form = ref(defaultForm());
const relationSchemaLoading = ref(false);

const fieldTypeItems = computed(() =>
  OC_POOL_FIELD_TYPE_VALUES.map((value) => ({
    value,
    title: t(`operationCore.definitions.fields.fieldType.${value}`),
  }))
);

const categoryItems = computed(() =>
  OC_FIELD_CATEGORIES.map((value) => ({
    value,
    title: t(`operationCore.definitions.fields.category.${value}`),
  }))
);

const cardinalityItems = computed(() => [
  { value: 'single', title: t('operationCore.definitions.fields.cardinality.single') },
  { value: 'multi', title: t('operationCore.definitions.fields.cardinality.multi') },
]);

const globalFieldsByCategory = computed(() => {
  const map = new Map<string, OpField[]>();
  for (const field of globalFields.value) {
    const cat = field.category || 'operational';
    if (!map.has(cat)) map.set(cat, []);
    map.get(cat)!.push(field);
  }
  return [...map.entries()].sort(([a], [b]) => a.localeCompare(b));
});

const scopedTableHeaders = computed(() => [
  { title: t('operationCore.definitions.fields.colKey'), key: 'key', sortable: true },
  { title: t('operationCore.definitions.fields.colLabel'), key: 'label', sortable: true },
  { title: t('operationCore.definitions.fields.colFieldType'), key: 'fieldType', sortable: true },
  { title: t('operationCore.definitions.fields.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const showRelationDataset = computed(() => form.value.fieldType === 'relation');
const showFileOptions = computed(() => form.value.fieldType === 'file');
const showSelectOptions = computed(() => form.value.fieldType === 'select');
const showRelationLookupOptions = computed(() => form.value.fieldType === 'relation');
const showAdvancedOptionsJson = computed(
  () => !showFileOptions.value && !showSelectOptions.value && !showRelationLookupOptions.value
);

const lookupPresentationItems = computed(() => [
  { value: 'dropdown', title: t('operationCore.definitions.fields.lookupPresentation.dropdown') },
  { value: 'autocomplete', title: t('operationCore.definitions.fields.lookupPresentation.autocomplete') },
  { value: 'picker', title: t('operationCore.definitions.fields.lookupPresentation.picker') },
]);

const relationDatasetFieldItems = computed(() => {
  const ds = datasetStore.currentDataset;
  if (!ds?.name || ds.name !== form.value.relationDatasetName.trim()) {
    return buildLookupFieldKeyItems(undefined);
  }
  return buildLookupFieldKeyItems(ds.fields);
});

const dependsOnFieldItems = computed(() => {
  const keys = new Set<string>();
  for (const f of [...globalFields.value, ...scopedFields.value]) {
    if (f.key?.trim()) keys.add(f.key.trim());
  }
  return [...keys].sort().map((k) => ({ title: k, value: k }));
});

async function ensureLookupCatalogLoaded() {
  try {
    await loadLookupCatalog();
  } catch {
    /* catalog optional — relationDatasetName still editable if load fails */
  }
}

async function loadRelationDatasetSchema(name: string) {
  const trimmed = name.trim();
  if (!trimmed) return;
  relationSchemaLoading.value = true;
  try {
    await datasetStore.fetchDatasetByName(trimmed);
  } catch {
    /* schema hints optional */
  } finally {
    relationSchemaLoading.value = false;
  }
}

watch(
  () => dialog.value,
  (open) => {
    if (open) void ensureLookupCatalogLoaded();
  }
);

watch(
  () => form.value.relationDatasetName,
  (name) => {
    if (form.value.fieldType !== 'relation') return;
    void loadRelationDatasetSchema(name);
  }
);

function addStaticItem() {
  form.value.staticItems = [...form.value.staticItems, { value: '', label: '' }];
}

function removeStaticItem(index: number) {
  form.value.staticItems = form.value.staticItems.filter((_, i) => i !== index);
}

const fileExtensionPresetItems = [...OC_FILE_EXTENSION_PRESETS];

function categoryLabel(value: string | null | undefined) {
  if (!value) return '—';
  const key = `operationCore.definitions.fields.category.${value}`;
  const translated = t(key);
  return translated !== key ? translated : value;
}

function toggleFieldId(id: string, enabled: boolean) {
  if (enabled) {
    if (!selectedFieldIds.value.includes(id)) {
      selectedFieldIds.value = [...selectedFieldIds.value, id];
    }
  } else {
    selectedFieldIds.value = selectedFieldIds.value.filter((x) => x !== id);
  }
}

function syncAllowedExtensionsFromCombobox(raw: unknown) {
  form.value.allowedExtensions = normalizeOcFileExtensionList(raw);
}

function buildFieldPayload(): Record<string, unknown> | null {
  optionsError.value = null;
  const key = form.value.key.trim();
  const label = form.value.label.trim();
  if (!label) return null;
  if (!editId.value && !OC_FIELD_KEY_PATTERN.test(key)) {
    optionsError.value = t('operationCore.definitions.fields.keyInvalid');
    return null;
  }

  const optionsRaw = form.value.optionsJson.trim();
  let options: Record<string, unknown> | null = null;
  if (form.value.fieldType === 'file') {
    form.value.allowedExtensions = normalizeOcFileExtensionList(form.value.allowedExtensions);
    options = buildOcFileFieldOptionsPayload(form.value.maxSizeMb, form.value.allowedExtensions);
  } else if (form.value.fieldType === 'select') {
    const items = form.value.staticItems.filter((i) => i.value.trim() && i.label.trim());
    if (!items.length) {
      optionsError.value = t('operationCore.definitions.fields.staticItemsRequired');
      return null;
    }
    options = buildOcLookupFieldOptionsPayload({
      fieldType: 'select',
      presentation: form.value.lookupPresentation,
      staticItems: items,
    });
  } else if (form.value.fieldType === 'relation') {
    if (!form.value.relationDatasetName.trim()) {
      optionsError.value = t('operationCore.definitions.fields.relationDatasetRequired');
      return null;
    }
    options = buildOcLookupFieldOptionsPayload({
      fieldType: 'relation',
      presentation: form.value.lookupPresentation,
      valueField: form.value.lookupValueField,
      labelField: form.value.lookupLabelField,
      pageSize: form.value.lookupPageSize,
      filter: form.value.lookupFilter.trim() || null,
      dependsOnFieldKey: form.value.lookupDependsOnFieldKey,
      dependsOnFilterTemplate: form.value.lookupDependsOnFilterTemplate,
    });
  } else if (optionsRaw) {
    const parsed = parseOcFieldOptions(optionsRaw);
    if (!parsed) {
      optionsError.value = t('operationCore.definitions.fields.optionsInvalid');
      return null;
    }
    options = parsed;
  }

  const sortRaw = form.value.sortOrder.trim();
  const sortOrder = sortRaw === '' ? null : Number(sortRaw);

  const payload: Record<string, unknown> = {
    label,
    fieldType: form.value.fieldType,
    scope: 'pool',
    category: form.value.category || null,
    cardinality: form.value.cardinality || null,
    description: form.value.description.trim() || null,
    sortOrder: Number.isFinite(sortOrder) ? sortOrder : null,
    relationDatasetName:
      form.value.fieldType === 'relation' && form.value.relationDatasetName.trim()
        ? form.value.relationDatasetName.trim()
        : null,
    options,
    isSensitive: form.value.isSensitive,
    isSystem: false,
    workspaceId: props.workspaceId,
  };
  if (!editId.value) payload.key = key;
  return payload;
}

async function loadAll() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    const [ws, global, scoped] = await Promise.all([
      ocGetWorkspace(props.workspaceId),
      ocListGlobalPoolFields(),
      ocListWorkspaceScopedFields(props.workspaceId),
    ]);
    globalFields.value = global;
    scopedFields.value = scoped;
    const scopedIds = scoped.map((f) => f.__dataId).filter(Boolean);
    const mergedEnabled = [...new Set([...(ws?.enabledFieldIds ?? []), ...scopedIds])];
    if (mergedEnabled.length !== (ws?.enabledFieldIds?.length ?? 0)) {
      await ocUpdateWorkspace(props.workspaceId, { enabledFieldIds: mergedEnabled });
      selectedFieldIds.value = mergedEnabled;
    } else {
      selectedFieldIds.value = ws?.enabledFieldIds ? [...ws.enabledFieldIds] : [];
    }
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.fields.loadError');
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

function openCreateScoped() {
  editId.value = null;
  optionsError.value = null;
  form.value = defaultForm();
  dialog.value = true;
}

function applyLookupToForm(row: OpField) {
  const lookup = parseOcLookupFromFieldOptions(row.options, row.fieldType);
  form.value.lookupPresentation = lookup?.presentation ?? 'autocomplete';
  form.value.lookupValueField = lookup?.valueField ?? OC_LOOKUP_DEFAULT_VALUE_FIELD;
  form.value.lookupLabelField = lookup?.labelField ?? OC_LOOKUP_DEFAULT_LABEL_FIELD;
  form.value.lookupPageSize = lookup?.pageSize ?? OC_LOOKUP_DEFAULT_PAGE_SIZE;
  form.value.lookupFilter = lookup?.filter ?? '';
  form.value.lookupDependsOnFieldKey = lookup?.dependsOn?.fieldKey ?? '';
  form.value.lookupDependsOnFilterTemplate = lookup?.dependsOn?.filterTemplate ?? '';
  form.value.staticItems = lookup?.staticItems?.length
    ? lookup.staticItems.map((i) => ({ ...i }))
    : [{ value: '', label: '' }];
}

function openEditScoped(row: OpField) {
  editId.value = row.__dataId;
  optionsError.value = null;
  const fileOpts = parseOcFileFieldOptions(row.options);
  form.value = {
    key: row.key,
    label: row.label,
    fieldType: row.fieldType || 'text',
    category: row.category || 'classification',
    cardinality: row.cardinality || 'single',
    description: row.description ?? '',
    sortOrder: row.sortOrder != null ? String(row.sortOrder) : '',
    relationDatasetName: row.relationDatasetName ?? '',
    optionsJson:
      row.fieldType !== 'file' && row.fieldType !== 'relation' && row.fieldType !== 'select'
        ? stringifyOcFieldOptions(row.options)
        : '',
    maxSizeMb: Math.max(1, Math.round(fileOpts.maxSizeBytes / (1024 * 1024))),
    allowedExtensions: [...fileOpts.allowedExtensions],
    isSensitive: Boolean(row.isSensitive),
    lookupPresentation: 'autocomplete',
    lookupValueField: OC_LOOKUP_DEFAULT_VALUE_FIELD,
    lookupLabelField: OC_LOOKUP_DEFAULT_LABEL_FIELD,
    lookupPageSize: OC_LOOKUP_DEFAULT_PAGE_SIZE,
    lookupFilter: '',
    lookupDependsOnFieldKey: '',
    lookupDependsOnFilterTemplate: '',
    staticItems: [{ value: '', label: '' }],
  };
  if (row.fieldType === 'select' || row.fieldType === 'relation') {
    applyLookupToForm(row);
  }
  dialog.value = true;
  if (row.fieldType === 'relation' && row.relationDatasetName) {
    void loadRelationDatasetSchema(row.relationDatasetName);
  }
}

function openDelete(row: OpField) {
  deleteTarget.value = row;
  deleteDialog.value = true;
}

async function saveSelection() {
  if (!props.workspaceId) return;
  savingSelection.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    await ocUpdateWorkspace(props.workspaceId, {
      enabledFieldIds: selectedFieldIds.value,
    });
    await loadAll();
    await metaCache.applySaveSuccess(
      (msg) => {
        successLocal.value = msg;
      },
      t('operationCore.workspaceDefinitions.saveSuccess')
    );
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.fields.saveSelectionError');
  } finally {
    savingSelection.value = false;
  }
}

async function submitScopedField() {
  if (!form.value.label.trim()) return;
  if (!editId.value && !form.value.key.trim()) return;
  const body = buildFieldPayload();
  if (!body) return;

  savingField.value = true;
  errorLocal.value = null;
  try {
    if (editId.value) {
      await ocUpdateField(editId.value, body);
    } else {
      const newId = await ocCreateField(body);
      if (newId && !selectedFieldIds.value.includes(newId)) {
        selectedFieldIds.value = [...selectedFieldIds.value, newId];
        await ocUpdateWorkspace(props.workspaceId, {
          enabledFieldIds: selectedFieldIds.value,
        });
      }
    }
    dialog.value = false;
    await loadAll();
    void metaCache.reloadAfterMetadataChange();
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.definitions.fields.saveError');
  } finally {
    savingField.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    const id = deleteTarget.value.__dataId;
    await ocDeleteField(id);
    selectedFieldIds.value = selectedFieldIds.value.filter((x) => x !== id);
    deleteDialog.value = false;
    deleteTarget.value = null;
    await loadAll();
    void metaCache.reloadAfterMetadataChange();
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.definitions.fields.deleteError');
    deleteDialog.value = false;
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="oc-ws-fields-tab pa-4 pa-md-6">
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

    <div v-if="loading" class="d-flex justify-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <template v-else>
      <section class="mb-8">
        <h3 class="text-subtitle-1 font-weight-medium mb-1">
          {{ t('operationCore.workspaceDefinitions.fields.catalogTitle') }}
        </h3>
        <p class="text-body-2 text-medium-emphasis mb-4">
          {{ t('operationCore.workspaceDefinitions.fields.catalogSubtitle') }}
        </p>

        <v-card
          v-for="[category, fields] in globalFieldsByCategory"
          :key="category"
          variant="outlined"
          rounded="lg"
          class="mb-3"
        >
          <v-card-title class="text-subtitle-2 py-3">
            {{ categoryLabel(category) }}
          </v-card-title>
          <v-divider />
          <v-card-text class="pt-3">
            <div class="d-flex flex-column ga-1">
              <v-checkbox
                v-for="field in fields"
                :key="field.__dataId"
                :model-value="selectedFieldIds.includes(field.__dataId)"
                hide-details
                density="compact"
                @update:model-value="(v) => toggleFieldId(field.__dataId, !!v)"
              >
                <template #label>
                  <span>{{ field.label }}</span>
                  <code class="text-caption text-medium-emphasis ml-2">{{ field.key }}</code>
                </template>
              </v-checkbox>
            </div>
          </v-card-text>
        </v-card>

        <div class="d-flex justify-end mt-4">
          <v-btn
            color="primary"
            rounded="lg"
            class="text-none"
            :loading="savingSelection"
            @click="saveSelection"
          >
            {{ t('operationCore.workspaceDefinitions.fields.saveSelection') }}
          </v-btn>
        </div>
      </section>

      <section>
        <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
          <div>
            <h3 class="text-subtitle-1 font-weight-medium mb-1">
              {{ t('operationCore.workspaceDefinitions.fields.scopedTitle') }}
            </h3>
            <p class="text-body-2 text-medium-emphasis mb-0">
              {{ t('operationCore.workspaceDefinitions.fields.scopedSubtitle') }}
            </p>
          </div>
          <v-btn color="primary" variant="tonal" rounded="lg" class="text-none" @click="openCreateScoped">
            <v-icon icon="mdi-plus" start />
            {{ t('operationCore.workspaceDefinitions.fields.newScopedField') }}
          </v-btn>
        </div>

        <v-card variant="outlined" rounded="lg">
          <v-data-table
            :headers="scopedTableHeaders"
            :items="scopedFields"
            class="oc-ws-scoped-fields-table"
          >
            <template #[`item.key`]="{ item }">
              <code class="text-caption">{{ item.key }}</code>
            </template>
            <template #[`item.fieldType`]="{ item }">
              <code class="text-caption">{{ item.fieldType }}</code>
            </template>
            <template #[`item.actions`]="{ item }">
              <v-btn icon variant="text" size="small" @click="openEditScoped(item)">
                <v-icon icon="mdi-pencil-outline" />
              </v-btn>
              <v-btn icon variant="text" size="small" color="error" @click="openDelete(item)">
                <v-icon icon="mdi-delete-outline" />
              </v-btn>
            </template>
          </v-data-table>
        </v-card>
      </section>
    </template>

    <v-dialog v-model="dialog" max-width="720">
      <v-card rounded="xl">
        <v-card-title class="text-h6">
          {{
            editId
              ? t('operationCore.workspaceDefinitions.fields.editScopedField')
              : t('operationCore.workspaceDefinitions.fields.newScopedField')
          }}
        </v-card-title>
        <v-card-text>
          <v-alert v-if="optionsError" type="warning" variant="tonal" density="compact" class="mb-4">
            {{ optionsError }}
          </v-alert>
          <v-text-field
            v-model="form.key"
            :label="t('operationCore.definitions.fields.fieldKey')"
            :hint="t('operationCore.definitions.fields.keyHint')"
            persistent-hint
            density="comfortable"
            :disabled="!!editId"
            required
          />
          <v-text-field
            v-model="form.label"
            class="mt-3"
            :label="t('operationCore.definitions.fields.fieldLabel')"
            density="comfortable"
            required
          />
          <v-select
            v-model="form.fieldType"
            class="mt-3"
            :items="fieldTypeItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.definitions.fields.fieldFieldType')"
            density="comfortable"
          />
          <v-select
            v-model="form.category"
            class="mt-3"
            :items="categoryItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.definitions.fields.fieldCategory')"
            density="comfortable"
          />
          <v-select
            v-model="form.cardinality"
            class="mt-3"
            :items="cardinalityItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.definitions.fields.fieldCardinality')"
            density="comfortable"
          />
          <template v-if="showRelationLookupOptions">
            <v-autocomplete
              v-model="form.relationDatasetName"
              class="mt-3"
              :items="lookupSelectableDatasets"
              item-title="title"
              item-value="value"
              :label="t('operationCore.definitions.fields.fieldRelationDataset')"
              :hint="t('operationCore.definitions.fields.relationDatasetLookupHint')"
              persistent-hint
              density="comfortable"
              clearable
              :loading="lookupCatalogLoading || relationSchemaLoading"
            />
            <v-select
              v-model="form.lookupPresentation"
              class="mt-3"
              :items="lookupPresentationItems"
              item-title="title"
              item-value="value"
              :label="t('operationCore.definitions.fields.lookupPresentationLabel')"
              :hint="t('operationCore.definitions.fields.lookupPresentationHint')"
              persistent-hint
              density="comfortable"
            />
            <v-row dense class="mt-1">
              <v-col cols="12" md="6">
                <v-select
                  v-model="form.lookupValueField"
                  :items="relationDatasetFieldItems"
                  item-title="title"
                  item-value="value"
                  :label="t('operationCore.definitions.fields.lookupValueField')"
                  density="comfortable"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-select
                  v-model="form.lookupLabelField"
                  :items="relationDatasetFieldItems"
                  item-title="title"
                  item-value="value"
                  :label="t('operationCore.definitions.fields.lookupLabelField')"
                  density="comfortable"
                />
              </v-col>
            </v-row>
            <v-text-field
              v-model.number="form.lookupPageSize"
              class="mt-3"
              type="number"
              min="1"
              max="500"
              :label="t('operationCore.definitions.fields.lookupPageSize')"
              density="comfortable"
            />
            <v-text-field
              v-model="form.lookupFilter"
              class="mt-3"
              :label="t('operationCore.definitions.fields.lookupFilter')"
              :hint="t('operationCore.definitions.fields.lookupFilterHint')"
              persistent-hint
              density="comfortable"
            />
            <v-row dense class="mt-1">
              <v-col cols="12" md="6">
                <v-select
                  v-model="form.lookupDependsOnFieldKey"
                  :items="dependsOnFieldItems"
                  item-title="title"
                  item-value="value"
                  :label="t('operationCore.definitions.fields.lookupDependsOnField')"
                  clearable
                  density="comfortable"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-model="form.lookupDependsOnFilterTemplate"
                  :label="t('operationCore.definitions.fields.lookupDependsOnFilter')"
                  :hint="t('operationCore.definitions.fields.lookupDependsOnFilterHint')"
                  persistent-hint
                  density="comfortable"
                  :disabled="!form.lookupDependsOnFieldKey"
                />
              </v-col>
            </v-row>
          </template>
          <template v-else-if="showSelectOptions">
            <v-select
              v-model="form.lookupPresentation"
              class="mt-3"
              :items="lookupPresentationItems.filter((i) => i.value !== 'picker')"
              item-title="title"
              item-value="value"
              :label="t('operationCore.definitions.fields.lookupPresentationLabel')"
              density="comfortable"
            />
            <div class="mt-4">
              <div class="text-subtitle-2 mb-2">
                {{ t('operationCore.definitions.fields.staticItemsTitle') }}
              </div>
              <div
                v-for="(item, idx) in form.staticItems"
                :key="idx"
                class="d-flex ga-2 mb-2 align-start"
              >
                <v-text-field
                  v-model="item.value"
                  :label="t('operationCore.definitions.fields.staticItemValue')"
                  density="compact"
                  hide-details
                />
                <v-text-field
                  v-model="item.label"
                  :label="t('operationCore.definitions.fields.staticItemLabel')"
                  density="compact"
                  hide-details
                />
                <v-btn
                  icon
                  variant="text"
                  size="small"
                  :aria-label="t('operationCore.definitions.delete')"
                  @click="removeStaticItem(idx)"
                >
                  <v-icon icon="mdi-close" />
                </v-btn>
              </div>
              <v-btn variant="tonal" size="small" class="text-none" @click="addStaticItem">
                {{ t('operationCore.definitions.fields.staticItemAdd') }}
              </v-btn>
            </div>
          </template>
          <v-textarea
            v-if="showAdvancedOptionsJson"
            v-model="form.optionsJson"
            class="mt-3"
            :label="t('operationCore.definitions.fields.fieldOptions')"
            rows="3"
            auto-grow
            density="comfortable"
            variant="outlined"
          />
          <v-textarea
            v-model="form.description"
            class="mt-3"
            :label="t('operationCore.definitions.fields.fieldDescription')"
            rows="2"
            auto-grow
            density="comfortable"
            variant="outlined"
          />
          <template v-if="showFileOptions">
            <v-text-field
              v-model.number="form.maxSizeMb"
              class="mt-3"
              type="number"
              min="1"
              max="100"
              :label="t('operationCore.definitions.fields.fileMaxSizeMb')"
              :hint="t('operationCore.definitions.fields.fileMaxSizeHint')"
              persistent-hint
              density="comfortable"
            />
            <v-combobox
              :model-value="form.allowedExtensions"
              class="mt-3"
              :items="fileExtensionPresetItems"
              multiple
              chips
              closable-chips
              @update:model-value="syncAllowedExtensionsFromCombobox"
              :label="t('operationCore.definitions.fields.fileAllowedExtensions')"
              :hint="t('operationCore.definitions.fields.fileAllowedExtensionsHint')"
              persistent-hint
              density="comfortable"
            />
          </template>
          <v-text-field
            v-model="form.sortOrder"
            class="mt-3"
            type="number"
            :label="t('operationCore.definitions.fields.fieldSortOrder')"
            density="comfortable"
          />
          <v-checkbox
            v-model="form.isSensitive"
            class="mt-1"
            :label="t('operationCore.definitions.fields.fieldSensitive')"
            density="comfortable"
            hide-details
          />
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
            :loading="savingField"
            :disabled="!form.label.trim() || (!editId && !form.key.trim())"
            @click="submitScopedField"
          >
            {{ t('operationCore.definitions.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title>{{ t('operationCore.definitions.fields.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('operationCore.definitions.fields.deleteBody') }}</v-card-text>
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
