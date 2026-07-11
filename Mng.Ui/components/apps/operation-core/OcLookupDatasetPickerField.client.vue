<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcDatasetPickerApi } from '@/composables/useOcDatasetPicker';
import {
  collectLookupIdsFromValue,
  formatLookupPickerCell,
  resolveLookupPickerColumns,
  type OcLookupColumn,
  type OcLookupConfig,
} from '@/utils/ocLookupFieldOptions';

const props = withDefaults(
  defineProps<{
    multiple?: boolean;
    disabled?: boolean;
    density?: 'default' | 'comfortable' | 'compact';
    variant?: 'outlined' | 'filled' | 'plain' | 'underlined' | 'solo';
    hideDetails?: boolean | 'auto';
    placeholder?: string;
    externalPicker: OcDatasetPickerApi;
    label?: string;
    showRequiredMark?: boolean;
    error?: boolean;
    errorMessages?: string | string[];
    fieldClass?: string;
    /** Full lookup config (columns / formats). */
    lookupConfig?: OcLookupConfig | null;
    /** @deprecated Prefer lookupConfig.columns */
    labelFieldKey?: string;
    /** @deprecated Prefer lookupConfig.columns */
    searchFieldKeys?: string[];
    selectionMin?: number;
    selectionMax?: number;
  }>(),
  {
    multiple: false,
    disabled: false,
    density: 'comfortable',
    variant: 'outlined',
    hideDetails: 'auto',
    labelFieldKey: 'name',
    searchFieldKeys: () => [],
  }
);

const model = defineModel<unknown>();

const { t } = useAppI18n();
const picker = computed(() => props.externalPicker);

const dialogOpen = ref(false);
const draftSelection = ref<string[]>([]);
const tableSearch = ref('');
const tablePage = ref(1);
const tableItemsPerPage = ref(20);

const tableLoading = computed(() => picker.value.loading.value);
const tableTotal = computed(() => picker.value.totalItems.value);

const selectedIds = computed(() => collectLookupIdsFromValue(model.value));

/** Multi always uses chips; single uses chips when readonly/disabled (profile). */
const useChipStrip = computed(() => props.multiple || props.disabled);

const displayText = computed(() => {
  const ids = selectedIds.value;
  if (!ids.length) return '';
  return ids.map((id) => picker.value.labelFor(id)).join(', ');
});

const effectiveColumns = computed<OcLookupColumn[]>(() => {
  if (props.lookupConfig) return resolveLookupPickerColumns(props.lookupConfig);
  const fallback: OcLookupConfig = {
    source: 'dataset',
    presentation: 'picker',
    valueField: '__dataId',
    labelField: props.labelFieldKey || 'name',
    staticItems: [],
    searchFields: props.searchFieldKeys ?? [],
    pageSize: 50,
    filter: null,
    dependsOn: null,
    columns: [],
    defaultSort: null,
    selection: null,
  };
  return resolveLookupPickerColumns(fallback);
});

/** Text/enum/date columns — show filters unless explicitly filterable:false. Relation labels need related-dataset search (later). */
const filterableColumns = computed(() =>
  effectiveColumns.value.filter((c) => {
    if (c.format === 'relationLabel') return false;
    if (c.filterable === false) return false;
    // Explicit true, or default-on for ordinary columns (TP-2 UX)
    return c.filterable === true || c.filterable == null;
  })
);

const hasActiveColumnFilters = computed(() =>
  Object.values(picker.value.columnFilters.value).some((v) => String(v ?? '').trim())
);

function enumFilterItems(col: OcLookupColumn) {
  if (!col.enumMap) return [];
  return Object.entries(col.enumMap).map(([value, title]) => ({ value, title }));
}

const tableHeaders = computed(() =>
  effectiveColumns.value.map((col) => ({
    title: col.title || col.field,
    key: col.field,
    sortable: col.sortable === true,
    width: col.width,
  }))
);

const tableItems = computed(() =>
  picker.value.items.value.map((row) => {
    const item: Record<string, unknown> = {
      value: row.value,
      title: row.title,
    };
    for (const col of effectiveColumns.value) {
      const raw =
        col.field === props.lookupConfig?.labelField || col.field === props.labelFieldKey
          ? (row.raw[col.field] ?? row.title)
          : row.raw[col.field];
      item[col.field] = formatLookupPickerCell(raw, col);
    }
    // Ensure label column always has something when using L4 fallback key "title"
    if (!item[effectiveColumns.value[0]?.field ?? '']) {
      item[effectiveColumns.value[0]?.field ?? 'title'] = row.title;
    }
    return item;
  })
);

const selectionCountLabel = computed(() =>
  t('operationCore.formUi.datasetPicker.selectedCount', { count: draftSelection.value.length })
);

const confirmDisabled = computed(() => {
  const n = draftSelection.value.length;
  if (n === 0) return true;
  if (props.selectionMin != null && n < props.selectionMin) return true;
  if (props.selectionMax != null && n > props.selectionMax) return true;
  return false;
});

async function syncSelectionFromModel() {
  const ids = collectLookupIdsFromValue(model.value);
  if (ids.length) await picker.value.ensureSelectedLabels(ids);
}

watch(
  () => model.value,
  () => {
    void syncSelectionFromModel();
  },
  { immediate: true }
);

async function openDialog() {
  if (props.disabled) return;
  draftSelection.value = collectLookupIdsFromValue(model.value);
  tableSearch.value = picker.value.searchTerm.value;
  tablePage.value = 1;
  tableItemsPerPage.value = Math.min(
    50,
    Math.max(10, props.lookupConfig?.pageSize ?? 25)
  );
  dialogOpen.value = true;
  await picker.value.resetAndFetch(tableSearch.value);
}

function onColumnFilterUpdate(field: string, value: unknown) {
  picker.value.setColumnFilter(field, value == null ? '' : String(value));
  tablePage.value = 1;
}

function clearFilters() {
  picker.value.clearColumnFilters();
  tableSearch.value = '';
  picker.value.onSearchUpdate('');
  tablePage.value = 1;
}

function clearSelection() {
  if (props.disabled) return;
  model.value = props.multiple ? [] : null;
}

function removeChip(id: string) {
  if (props.disabled) return;
  if (!props.multiple) {
    model.value = null;
    return;
  }
  model.value = selectedIds.value.filter((x) => x !== id);
}

function onTableSearchUpdate(query: string) {
  tableSearch.value = query ?? '';
  tablePage.value = 1;
  picker.value.onSearchUpdate(tableSearch.value);
}

function onTableOptionsUpdate(opts: {
  page: number;
  itemsPerPage: number;
  sortBy?: Array<{ key: string; order: 'asc' | 'desc' }>;
}) {
  tablePage.value = opts.page;
  tableItemsPerPage.value = opts.itemsPerPage > 0 ? opts.itemsPerPage : 20;
  void picker.value.onTableOptionsUpdate({
    page: tablePage.value,
    itemsPerPage: tableItemsPerPage.value,
    sortBy: opts.sortBy,
  });
}

function isRowSelected(value: string): boolean {
  return draftSelection.value.includes(value);
}

function onRowClick(_event: Event, row: { item: { value: string } }) {
  const id = row.item.value;
  if (props.multiple) {
    const set = new Set(draftSelection.value);
    if (set.has(id)) set.delete(id);
    else {
      if (props.selectionMax != null && set.size >= props.selectionMax) return;
      set.add(id);
    }
    draftSelection.value = [...set];
    return;
  }
  draftSelection.value = [id];
}

async function confirmSelection() {
  if (confirmDisabled.value) return;
  if (props.multiple) {
    model.value = [...draftSelection.value];
  } else {
    model.value = draftSelection.value[0] ?? null;
  }
  await picker.value.ensureSelectedLabels(collectLookupIdsFromValue(model.value));
  dialogOpen.value = false;
}

function cancelDialog() {
  dialogOpen.value = false;
}

function onDraftSelectionUpdate(value: unknown) {
  if (!props.multiple || !Array.isArray(value)) return;
  let next = value.map((v) => String(v));
  if (props.selectionMax != null && next.length > props.selectionMax) {
    next = next.slice(0, props.selectionMax);
  }
  draftSelection.value = next;
}
</script>

<template>
  <div class="oc-dataset-picker-field">
    <div v-if="useChipStrip" class="oc-dataset-picker-field__multi mb-1">
      <div class="d-flex align-center justify-space-between ga-2 mb-1">
        <div class="text-body-2">
          <span v-if="label">{{ label }}</span>
          <span v-if="showRequiredMark" class="oc-field-required" aria-hidden="true"> *</span>
        </div>
        <v-btn
          v-if="!disabled"
          size="small"
          variant="tonal"
          prepend-icon="mdi-table-search"
          @click="openDialog"
        >
          {{ multiple ? t('operationCore.formUi.datasetPicker.add') : t('operationCore.formUi.datasetPicker.open') }}
        </v-btn>
      </div>
      <div v-if="selectedIds.length" class="d-flex flex-wrap ga-1 mb-1">
        <v-chip
          v-for="id in selectedIds"
          :key="id"
          size="small"
          :closable="!disabled && multiple"
          :disabled="disabled"
          @click:close="removeChip(id)"
        >
          {{ picker.labelFor(id) }}
        </v-chip>
      </div>
      <p v-else class="text-caption text-medium-emphasis mb-0">
        {{
          placeholder ??
          (multiple
            ? t('operationCore.formUi.datasetPicker.placeholderMulti')
            : t('operationCore.formUi.datasetPicker.placeholder'))
        }}
      </p>
      <div
        v-if="error && errorMessages"
        class="text-caption text-error mt-1"
      >
        {{ Array.isArray(errorMessages) ? errorMessages.join(' ') : errorMessages }}
      </div>
    </div>

    <v-text-field
      v-else
      :model-value="displayText"
      readonly
      :disabled="disabled"
      :placeholder="placeholder ?? t('operationCore.formUi.datasetPicker.placeholder')"
      :density="density"
      :variant="variant"
      :hide-details="hideDetails"
      :error="error"
      :error-messages="errorMessages"
      clearable
      :class="fieldClass"
      @click:clear="clearSelection"
      @click:control="openDialog"
    >
      <template v-if="label || showRequiredMark" #label>
        <span v-if="label">{{ label }}</span>
        <span v-if="showRequiredMark" class="oc-field-required" aria-hidden="true"> *</span>
      </template>
      <template #append-inner>
        <v-btn
          icon="mdi-table-search"
          variant="text"
          size="small"
          :disabled="disabled"
          :aria-label="t('operationCore.formUi.datasetPicker.open')"
          @click.stop="openDialog"
        />
      </template>
    </v-text-field>

    <v-dialog v-model="dialogOpen" max-width="960" scrollable>
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-semibold pa-4 pb-2">
          {{ label || t('operationCore.formUi.datasetPicker.dialogTitle') }}
        </v-card-title>

        <v-card-text class="pa-4 pt-2">
          <div class="d-flex flex-wrap ga-2 mb-3 align-end">
            <v-text-field
              :model-value="tableSearch"
              :placeholder="t('operationCore.formUi.datasetPicker.searchHint')"
              prepend-inner-icon="mdi-magnify"
              density="comfortable"
              variant="outlined"
              hide-details
              clearable
              class="oc-dataset-picker__search flex-grow-1"
              style="min-width: 200px"
              @update:model-value="onTableSearchUpdate"
            />
            <v-btn
              v-if="tableSearch || hasActiveColumnFilters"
              variant="text"
              size="small"
              @click="clearFilters"
            >
              {{ t('operationCore.formUi.datasetPicker.clearFilters') }}
            </v-btn>
          </div>

          <div
            v-if="filterableColumns.length"
            class="d-flex flex-wrap ga-2 mb-3"
          >
            <template v-for="col in filterableColumns" :key="`f-${col.field}`">
              <v-select
                v-if="col.format === 'enum' && col.enumMap"
                :model-value="picker.columnFilters.value[col.field] || null"
                :items="enumFilterItems(col)"
                item-title="title"
                item-value="value"
                :label="col.title || col.field"
                density="compact"
                variant="outlined"
                hide-details
                clearable
                style="min-width: 140px; max-width: 200px"
                @update:model-value="onColumnFilterUpdate(col.field, $event)"
              />
              <v-text-field
                v-else
                :model-value="picker.columnFilters.value[col.field] || ''"
                :label="col.title || col.field"
                density="compact"
                variant="outlined"
                hide-details
                clearable
                style="min-width: 140px; max-width: 200px"
                @update:model-value="onColumnFilterUpdate(col.field, $event)"
              />
            </template>
          </div>

          <v-data-table-server
            :page="tablePage"
            :items-per-page="tableItemsPerPage"
            :headers="tableHeaders"
            :items="tableItems"
            :loading="tableLoading"
            :items-length="tableTotal"
            :items-per-page-options="[10, 20, 50]"
            item-value="value"
            density="comfortable"
            class="border rounded-md oc-dataset-picker__table"
            :show-select="multiple"
            :model-value="multiple ? draftSelection : undefined"
            @update:model-value="onDraftSelectionUpdate"
            @update:options="onTableOptionsUpdate"
            @click:row="onRowClick"
          >
            <template
              v-for="col in effectiveColumns"
              :key="col.field"
              #[`item.${col.field}`]="{ item }"
            >
              <span
                :class="{
                  'font-weight-semibold text-primary':
                    !multiple && isRowSelected(String(item.value)),
                }"
              >
                {{ item[col.field] }}
              </span>
            </template>
          </v-data-table-server>

          <p v-if="!tableLoading && !tableItems.length" class="text-caption text-medium-emphasis mt-2 mb-0">
            {{ t('operationCore.formUi.datasetPicker.empty') }}
          </p>
          <p v-if="multiple" class="text-caption text-medium-emphasis mt-2 mb-0">
            {{ selectionCountLabel }}
          </p>
        </v-card-text>

        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="cancelDialog">
            {{ t('operationCore.formUi.datasetPicker.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            :disabled="confirmDisabled"
            @click="confirmSelection"
          >
            {{ t('operationCore.formUi.datasetPicker.select') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.oc-field-required {
  color: rgb(var(--v-theme-error));
  font-weight: 600;
}

.oc-dataset-picker__table :deep(tbody tr) {
  cursor: pointer;
}
</style>
