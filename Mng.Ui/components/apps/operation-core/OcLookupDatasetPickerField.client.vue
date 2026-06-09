<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcDatasetPickerApi } from '@/composables/useOcDatasetPicker';
import { collectLookupIdsFromValue } from '@/utils/ocLookupFieldOptions';

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
    /** Tablo başlığı — görünen alan adı. */
    labelFieldKey?: string;
    /** Ek sütunlar (dataset alan anahtarları). */
    searchFieldKeys?: string[];
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

const displayText = computed(() => {
  const ids = collectLookupIdsFromValue(model.value);
  if (!ids.length) return '';
  return ids.map((id) => picker.value.labelFor(id)).join(', ');
});

const tableHeaders = computed(() => {
  const cols: { title: string; key: string; sortable: boolean }[] = [
    {
      title: props.labelFieldKey || t('operationCore.formUi.datasetPicker.labelColumn'),
      key: 'title',
      sortable: false,
    },
  ];
  for (const key of props.searchFieldKeys.slice(0, 2)) {
    cols.push({ title: key, key: `extra_${key}`, sortable: false });
  }
  return cols;
});

const tableItems = computed(() =>
  picker.value.items.value.map((row) => {
    const item: Record<string, unknown> = {
      title: row.title,
      value: row.value,
    };
    for (const key of props.searchFieldKeys.slice(0, 2)) {
      const raw = row.raw[key];
      item[`extra_${key}`] = raw != null && raw !== '' ? String(raw) : '—';
    }
    return item;
  })
);

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
  dialogOpen.value = true;
  await picker.value.resetAndFetch(tableSearch.value);
}

function clearSelection() {
  if (props.disabled) return;
  model.value = props.multiple ? [] : null;
}

function onTableSearchUpdate(query: string) {
  tableSearch.value = query ?? '';
  tablePage.value = 1;
  picker.value.onSearchUpdate(tableSearch.value);
}

function onTableOptionsUpdate(opts: { page: number; itemsPerPage: number }) {
  tablePage.value = opts.page;
  tableItemsPerPage.value = opts.itemsPerPage > 0 ? opts.itemsPerPage : 20;
  void picker.value.onTableOptionsUpdate({
    page: tablePage.value,
    itemsPerPage: tableItemsPerPage.value,
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
    else set.add(id);
    draftSelection.value = [...set];
    return;
  }
  draftSelection.value = [id];
}

async function confirmSelection() {
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
  draftSelection.value = value.map((v) => String(v));
}
</script>

<template>
  <div class="oc-dataset-picker-field">
  <v-text-field
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

  <v-dialog v-model="dialogOpen" max-width="760" scrollable>
    <v-card rounded="lg">
      <v-card-title class="text-subtitle-1 font-weight-semibold pa-4 pb-2">
        {{ label || t('operationCore.formUi.datasetPicker.dialogTitle') }}
      </v-card-title>

      <v-card-text class="pa-4 pt-2">
        <v-text-field
          :model-value="tableSearch"
          :placeholder="t('operationCore.formUi.datasetPicker.searchHint')"
          prepend-inner-icon="mdi-magnify"
          density="comfortable"
          variant="outlined"
          hide-details
          clearable
          class="mb-3"
          @update:model-value="onTableSearchUpdate"
        />

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
          <template v-if="!multiple" #[`item.title`]="{ item }">
            <span
              :class="{
                'font-weight-semibold text-primary': isRowSelected(item.value),
              }"
            >
              {{ item.title }}
            </span>
          </template>
        </v-data-table-server>

        <p v-if="!tableLoading && !tableItems.length" class="text-caption text-medium-emphasis mt-2 mb-0">
          {{ t('operationCore.formUi.datasetPicker.empty') }}
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
          :disabled="!draftSelection.length"
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
