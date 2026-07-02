<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  useKeeperDirectoryPicker,
  type KeeperDirectoryPickerApi,
} from '@/composables/useKeeperDirectoryPicker';
import { collectDirectoryIdsFromValue, KEEPER_DIRECTORY_PICKER_SELECT_ALL_MAX, type KeeperDirectoryEntity, type KeeperGroupValueKey } from '@/utils/keeperDirectoryPicker';

const props = withDefaults(
  defineProps<{
    entity: KeeperDirectoryEntity;
    multiple?: boolean;
    disabled?: boolean;
    density?: 'default' | 'comfortable' | 'compact';
    variant?: 'outlined' | 'filled' | 'plain' | 'underlined' | 'solo';
    hideDetails?: boolean | 'auto';
    placeholder?: string;
    /** Form genelinde tek picker; yoksa bileşen kendi picker'ını kullanır. */
    externalPicker?: KeeperDirectoryPickerApi;
    label?: string;
    showRequiredMark?: boolean;
    error?: boolean;
    errorMessages?: string | string[];
    fieldClass?: string;
    /** Grup seçiminde model değeri: id (varsayılan) veya name */
    groupValueKey?: KeeperGroupValueKey;
  }>(),
  {
    multiple: false,
    disabled: false,
    density: 'comfortable',
    variant: 'outlined',
    hideDetails: 'auto',
    groupValueKey: 'id',
  }
);

const model = defineModel<unknown>();

const { t } = useAppI18n();
const internalPicker = useKeeperDirectoryPicker(
  props.entity,
  props.entity === 'group' ? { groupValueKey: props.groupValueKey } : undefined
);
const picker = computed(() => props.externalPicker ?? internalPicker);

const dialogOpen = ref(false);
const draftSelection = ref<string[]>([]);
const tableSearch = ref('');
const tablePage = ref(1);
const tableItemsPerPage = ref(20);
const selectingAll = ref(false);

const tableLoading = computed(() => picker.value.loading.value || selectingAll.value);
const tableTotal = computed(() => picker.value.totalItems.value);

const selectAllCount = computed(() =>
  Math.min(tableTotal.value || 0, KEEPER_DIRECTORY_PICKER_SELECT_ALL_MAX)
);

const selectAllLabel = computed(() => {
  const count = selectAllCount.value;
  if (count > 0) {
    return t('directoryPicker.selectAllCount', { count });
  }
  return t('directoryPicker.selectAll');
});

const selectedIds = computed(() => collectDirectoryIdsFromValue(model.value));

const i18nPrefix = computed(() =>
  props.entity === 'user' ? 'directoryPicker.user' : 'directoryPicker.group'
);

const placeholderText = computed(
  () => props.placeholder ?? t(`${i18nPrefix.value}.placeholder`)
);

const displayText = computed(() => {
  const ids = selectedIds.value;
  if (!ids.length) return '';
  if (props.multiple) return '';
  return picker.value.labelFor(ids[0] ?? '');
});

const tableHeaders = computed(() => {
  if (props.entity === 'user') {
    return [
      { title: t('directoryPicker.user.columnName'), key: 'title', sortable: false },
      { title: t('directoryPicker.user.columnUsername'), key: 'extra_username', sortable: false },
      { title: t('directoryPicker.user.columnEmail'), key: 'extra_email', sortable: false },
      { title: t('directoryPicker.user.columnDepartment'), key: 'extra_department', sortable: false },
    ];
  }
  return [
    { title: t('directoryPicker.group.columnName'), key: 'title', sortable: false },
    { title: t('directoryPicker.group.columnDescription'), key: 'extra_description', sortable: false },
    { title: t('directoryPicker.group.columnMembers'), key: 'extra_memberCount', sortable: false },
  ];
});

const tableItems = computed(() =>
  picker.value.items.value.map((row) => {
    if (props.entity === 'user') {
      return {
        title: row.title,
        value: row.value,
        extra_username: String(row.raw.username ?? '') || '—',
        extra_email: String(row.raw.email ?? '') || '—',
        extra_department: String(row.raw.department ?? '') || '—',
      };
    }
    return {
      title: row.title,
      value: row.value,
      extra_description: String(row.raw.description ?? '') || '—',
      extra_memberCount:
        row.raw.memberCount != null && row.raw.memberCount !== ''
          ? String(row.raw.memberCount)
          : '—',
    };
  })
);

const multiBoxClass = computed(() => [
  'mng-directory-picker-field__multi',
  `mng-directory-picker-field__multi--${props.variant}`,
  `mng-directory-picker-field__multi--${props.density}`,
  {
    'mng-directory-picker-field__multi--error': props.error,
    'mng-directory-picker-field__multi--disabled': props.disabled,
  },
  props.fieldClass,
]);

async function syncSelectionFromModel() {
  const ids = selectedIds.value;
  if (ids.length) await picker.value.ensureSelectedLabels(ids);
}

onMounted(() => {
  void syncSelectionFromModel();
});

watch(
  () => model.value,
  () => {
    void syncSelectionFromModel();
  }
);

async function openDialog() {
  if (props.disabled) return;
  draftSelection.value = [...selectedIds.value];
  tableSearch.value = picker.value.searchTerm.value;
  tablePage.value = 1;
  dialogOpen.value = true;
  await picker.value.resetAndFetch(tableSearch.value);
}

function removeChip(id: string) {
  if (props.disabled) return;
  if (!props.multiple) return;
  model.value = selectedIds.value.filter((x) => x !== id);
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
  await picker.value.ensureSelectedLabels(collectDirectoryIdsFromValue(model.value));
  dialogOpen.value = false;
}

function cancelDialog() {
  dialogOpen.value = false;
}

function onDraftSelectionUpdate(value: unknown) {
  if (!props.multiple || !Array.isArray(value)) return;
  draftSelection.value = value.map((v) => String(v));
}

function clearDraftSelection() {
  draftSelection.value = [];
}

async function selectAllMatching() {
  if (!props.multiple || selectingAll.value) return;

  selectingAll.value = true;
  try {
    const rows = await picker.value.fetchAllMatchingRows();
    const set = new Set(draftSelection.value);
    for (const row of rows) {
      set.add(row.value);
    }
    draftSelection.value = [...set];
    await picker.value.ensureSelectedLabels(draftSelection.value);
  } finally {
    selectingAll.value = false;
  }
}
</script>

<template>
  <div class="mng-directory-picker-field">
    <v-text-field
      v-if="!multiple"
      :model-value="displayText"
      readonly
      :disabled="disabled"
      :placeholder="placeholderText"
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
        <span v-if="showRequiredMark" class="mng-directory-picker-field__required" aria-hidden="true"> *</span>
      </template>
      <template #append-inner>
        <v-btn
          icon="mdi-table-search"
          variant="text"
          size="small"
          :disabled="disabled"
          :aria-label="t(`${i18nPrefix}.open`)"
          @click.stop="openDialog"
        />
      </template>
    </v-text-field>

    <div v-else class="mng-directory-picker-field__multi-wrap">
      <div v-if="label || showRequiredMark" class="text-caption mb-1">
        <span v-if="label">{{ label }}</span>
        <span v-if="showRequiredMark" class="mng-directory-picker-field__required" aria-hidden="true"> *</span>
      </div>
      <div :class="multiBoxClass" @click="openDialog">
        <div class="mng-directory-picker-field__chips">
          <v-chip
            v-for="id in selectedIds"
            :key="id"
            size="small"
            closable
            :disabled="disabled"
            @click:close.stop="removeChip(id)"
          >
            {{ picker.labelFor(id) }}
          </v-chip>
          <span v-if="!selectedIds.length" class="text-body-2 text-medium-emphasis">
            {{ placeholderText }}
          </span>
        </div>
        <div class="mng-directory-picker-field__actions">
          <v-btn
            v-if="selectedIds.length && !disabled"
            icon="mdi-close"
            variant="text"
            size="x-small"
            :aria-label="t('directoryPicker.clear')"
            @click.stop="clearSelection"
          />
          <v-btn
            icon="mdi-table-search"
            variant="text"
            size="small"
            :disabled="disabled"
            :aria-label="t(`${i18nPrefix}.open`)"
            @click.stop="openDialog"
          />
        </div>
      </div>
      <div
        v-if="error && errorMessages"
        class="text-caption text-error mt-1"
      >
        {{ Array.isArray(errorMessages) ? errorMessages[0] : errorMessages }}
      </div>
    </div>

    <v-dialog v-model="dialogOpen" max-width="820" scrollable>
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-semibold pa-4 pb-2">
          {{ label || t(`${i18nPrefix}.dialogTitle`) }}
        </v-card-title>

        <v-card-text class="pa-4 pt-2">
          <v-text-field
            :model-value="tableSearch"
            :placeholder="t(`${i18nPrefix}.searchHint`)"
            prepend-inner-icon="mdi-magnify"
            density="comfortable"
            variant="outlined"
            hide-details
            clearable
            class="mb-3"
            @update:model-value="onTableSearchUpdate"
          />

          <div
            v-if="multiple"
            class="d-flex flex-wrap align-center ga-2 mb-3"
          >
            <v-btn
              size="small"
              variant="tonal"
              color="primary"
              :loading="selectingAll"
              :disabled="!selectAllCount || tableLoading"
              @click="selectAllMatching"
            >
              {{ selectAllLabel }}
            </v-btn>
            <v-btn
              size="small"
              variant="text"
              :disabled="!draftSelection.length || selectingAll"
              @click="clearDraftSelection"
            >
              {{ t('directoryPicker.clearSelection') }}
            </v-btn>
            <span
              v-if="draftSelection.length"
              class="text-caption text-medium-emphasis"
            >
              {{ t('directoryPicker.selectedCount', { count: draftSelection.length }) }}
            </span>
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
            class="border rounded-md mng-directory-picker-field__table"
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
            {{ t(`${i18nPrefix}.empty`) }}
          </p>
        </v-card-text>

        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="cancelDialog">
            {{ t('directoryPicker.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            :disabled="!draftSelection.length"
            @click="confirmSelection"
          >
            {{ t('directoryPicker.select') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.mng-directory-picker-field__required {
  color: rgb(var(--v-theme-error));
  font-weight: 600;
}

.mng-directory-picker-field__multi {
  display: flex;
  align-items: flex-start;
  gap: 4px;
  min-height: 44px;
  padding: 6px 8px 6px 12px;
  cursor: pointer;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 4px;
  background: rgb(var(--v-theme-surface));
}

.mng-directory-picker-field__multi--compact {
  min-height: 36px;
  padding: 4px 6px 4px 10px;
}

.mng-directory-picker-field__multi--error {
  border-color: rgb(var(--v-theme-error));
}

.mng-directory-picker-field__multi--disabled {
  opacity: 0.6;
  pointer-events: none;
  cursor: default;
}

.mng-directory-picker-field__chips {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  flex: 1;
  min-width: 0;
  align-items: center;
}

.mng-directory-picker-field__actions {
  display: flex;
  align-items: center;
  flex-shrink: 0;
}

.mng-directory-picker-field__table :deep(tbody tr) {
  cursor: pointer;
}
</style>
