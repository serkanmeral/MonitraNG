<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ocCreateField,
  ocDeleteField,
  ocExtractDgErrorMessage,
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
  resolveOcFieldOptionsHint,
  stringifyOcFieldOptions,
} from '@/utils/ocFieldDefinitions';
import {
  buildOcFileFieldOptionsPayload,
  normalizeOcFileExtensionList,
  OC_FILE_EXTENSION_PRESETS,
  parseOcFileFieldOptions,
} from '@/utils/ocFileFieldOptions';

const props = defineProps<{
  workspaceId: string;
}>();

const { t, locale } = useAppI18n();

const fieldOptionsHintText = computed(() => resolveOcFieldOptionsHint(locale()));

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
});

const form = ref(defaultForm());

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
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.fields.loadError')
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

function openCreateScoped() {
  editId.value = null;
  optionsError.value = null;
  form.value = defaultForm();
  dialog.value = true;
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
    optionsJson: row.fieldType === 'file' ? '' : stringifyOcFieldOptions(row.options),
    maxSizeMb: Math.max(1, Math.round(fileOpts.maxSizeBytes / (1024 * 1024))),
    allowedExtensions: [...fileOpts.allowedExtensions],
    isSensitive: Boolean(row.isSensitive),
  };
  dialog.value = true;
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
    successLocal.value = t('operationCore.workspaceDefinitions.saveSuccess');
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.fields.saveSelectionError')
    );
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
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.definitions.fields.saveError')
    );
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
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.definitions.fields.deleteError')
    );
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

    <v-dialog v-model="dialog" max-width="640">
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
          <v-text-field
            v-if="showRelationDataset"
            v-model="form.relationDatasetName"
            class="mt-3"
            :label="t('operationCore.definitions.fields.fieldRelationDataset')"
            density="comfortable"
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
          <v-textarea
            v-else
            v-model="form.optionsJson"
            class="mt-3"
            :label="t('operationCore.definitions.fields.fieldOptions')"
            :hint="fieldOptionsHintText"
            persistent-hint
            rows="3"
            auto-grow
            density="comfortable"
            variant="outlined"
          />
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
