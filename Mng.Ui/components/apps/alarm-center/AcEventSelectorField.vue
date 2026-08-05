<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  createCustomEventSelection,
  filterEventCatalogRows,
  loadEventCatalogRows,
  rowToSelection,
  selectionLabel,
  selectionToRow,
  type EventCatalogRow,
  type EventCatalogSelection,
} from '@/utils/alarm/eventCatalog';
import {
  buildLinuxJournalCatalogRows,
  createCustomLinuxEventSelection,
  linuxPackageItems,
} from '@/utils/alarm/linuxJournalCatalog';

const props = withDefaults(
  defineProps<{
    modelValue?: EventCatalogSelection[];
    /** windows = Event ID dictionary; linux = journal package + action */
    platform?: 'windows' | 'linux';
    disabled?: boolean;
    label?: string;
    density?: 'default' | 'comfortable' | 'compact';
  }>(),
  {
    modelValue: () => [],
    platform: 'windows',
    disabled: false,
    density: 'compact',
  },
);

const emit = defineEmits<{
  'update:modelValue': [value: EventCatalogSelection[]];
}>();

const { t } = useAppI18n();

const isLinux = computed(() => props.platform === 'linux');

const dialogOpen = ref(false);
const loading = ref(false);
const allRows = ref<EventCatalogRow[]>([]);
const customByValue = ref<Record<string, EventCatalogSelection>>({});
const tableSearch = ref('');
const channelFilter = ref<string | null>(null);
const draftSelection = ref<string[]>([]);
const tablePage = ref(1);
const tableItemsPerPage = ref(10);
const sortBy = ref<{ key: string; order: 'asc' | 'desc' }[]>([
  { key: 'eventId', order: 'asc' },
]);

const customChannel = ref('Application');
const customEventId = ref<number | null>(null);
const customLinuxPackage = ref('sshd');
const customLinuxMatchKey = ref('');
const customLabel = ref('');
const customError = ref('');

const selected = computed(() => props.modelValue ?? []);

const catalogValueSet = computed(() => new Set(allRows.value.map(row => row.value)));

const channelItems = computed(() => {
  if (isLinux.value) {
    const map = new Map<string, string>();
    for (const item of linuxPackageItems()) map.set(item.value, item.title);
    for (const item of Object.values(customByValue.value)) {
      if (item.channel && !map.has(item.channel)) {
        map.set(item.channel, item.channelLabel || item.channel);
      }
    }
    const typed = resolveChannelInput(customLinuxPackage.value);
    if (typed && !map.has(typed)) map.set(typed, typed);
    return [...map.entries()]
      .map(([value, title]) => ({ value, title }))
      .sort((a, b) => a.title.localeCompare(b.title, undefined, { sensitivity: 'base' }));
  }

  const map = new Map<string, string>();
  for (const row of allRows.value) {
    if (!map.has(row.channel)) map.set(row.channel, row.channelLabel);
  }
  for (const item of Object.values(customByValue.value)) {
    if (item.channel && !map.has(item.channel)) {
      map.set(item.channel, item.channelLabel || item.channel);
    }
  }
  const typed = resolveChannelInput(customChannel.value);
  if (typed && !map.has(typed)) map.set(typed, typed);
  return [...map.entries()]
    .map(([value, title]) => ({ value, title }))
    .sort((a, b) => a.title.localeCompare(b.title, undefined, { sensitivity: 'base' }));
});

const mergedRows = computed(() => {
  const map = new Map<string, EventCatalogRow>();
  for (const row of allRows.value) map.set(row.value, row);
  for (const item of Object.values(customByValue.value)) {
    if (!map.has(item.value)) map.set(item.value, selectionToRow(item));
  }
  for (const item of selected.value) {
    if (!map.has(item.value)) map.set(item.value, selectionToRow(item));
  }
  return [...map.values()];
});

const filteredRows = computed(() =>
  filterEventCatalogRows(mergedRows.value, tableSearch.value, channelFilter.value),
);

const sortedRows = computed(() => {
  const rows = [...filteredRows.value];
  const sort = sortBy.value[0];
  if (!sort) return rows;
  const dir = sort.order === 'desc' ? -1 : 1;
  rows.sort((a, b) => {
    const key = sort.key as keyof EventCatalogRow;
    const left = a[key];
    const right = b[key];
    if (typeof left === 'number' && typeof right === 'number') return (left - right) * dir;
    return String(left ?? '').localeCompare(String(right ?? ''), undefined, { sensitivity: 'base' }) * dir;
  });
  return rows;
});

const headers = computed(() => {
  if (isLinux.value) {
    return [
      { title: '', key: 'selected', sortable: false, width: '48px' },
      { title: t('alarmCenter.scenarioStudio.eventSelector.colPackage'), key: 'channelLabel', sortable: true, minWidth: '160px' },
      { title: t('alarmCenter.scenarioStudio.eventSelector.colMatchKey'), key: 'matchKey', sortable: true, minWidth: '160px' },
      { title: t('alarmCenter.scenarioStudio.eventSelector.colName'), key: 'label', sortable: true, minWidth: '200px' },
    ];
  }
  return [
    { title: '', key: 'selected', sortable: false, width: '48px' },
    { title: t('alarmCenter.scenarioStudio.eventSelector.colEventId'), key: 'eventId', sortable: true, width: '110px' },
    { title: t('alarmCenter.scenarioStudio.eventSelector.colName'), key: 'label', sortable: true, minWidth: '220px' },
    { title: t('alarmCenter.scenarioStudio.eventSelector.colChannel'), key: 'channelLabel', sortable: true, minWidth: '200px' },
    { title: t('alarmCenter.scenarioStudio.eventSelector.colMatchKey'), key: 'matchKey', sortable: true, minWidth: '160px' },
  ];
});

const rowByValue = computed(() => {
  const map = new Map<string, EventCatalogRow>();
  for (const row of mergedRows.value) map.set(row.value, row);
  return map;
});

function chipLabel(item: EventCatalogSelection): string {
  if (isLinux.value || item.eventId <= 0) {
    return `${item.label} (${item.matchKey})`;
  }
  return selectionLabel(item);
}

function rememberCustom(item: EventCatalogSelection) {
  if (catalogValueSet.value.has(item.value)) return;
  customByValue.value = { ...customByValue.value, [item.value]: item };
}

async function loadRowsForPlatform() {
  loading.value = true;
  try {
    allRows.value = isLinux.value
      ? buildLinuxJournalCatalogRows()
      : await loadEventCatalogRows();
    sortBy.value = isLinux.value
      ? [{ key: 'channelLabel', order: 'asc' }]
      : [{ key: 'eventId', order: 'asc' }];
    customChannel.value = isLinux.value ? 'sshd' : 'Application';
    customLinuxPackage.value = 'sshd';
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadRowsForPlatform();
});

watch(
  () => props.platform,
  async () => {
    allRows.value = [];
    customByValue.value = {};
    await loadRowsForPlatform();
  },
);

watch(
  () => [tableSearch.value, channelFilter.value],
  () => {
    tablePage.value = 1;
  },
);

watch(
  selected,
  (items) => {
    for (const item of items) rememberCustom(item);
  },
  { immediate: true, deep: true },
);

function resolveChannelInput(value: unknown): string {
  if (value == null) return '';
  if (typeof value === 'string') return value.trim();
  if (typeof value === 'object' && value !== null && 'value' in value) {
    return String((value as { value: unknown }).value ?? '').trim();
  }
  return String(value).trim();
}

async function openDialog() {
  if (props.disabled) return;
  if (!allRows.value.length) await loadRowsForPlatform();
  draftSelection.value = selected.value.map(item => item.value);
  tableSearch.value = '';
  channelFilter.value = null;
  customError.value = '';
  tablePage.value = 1;
  dialogOpen.value = true;
}

function removeChip(value: string) {
  if (props.disabled) return;
  emit(
    'update:modelValue',
    selected.value.filter(item => item.value !== value),
  );
}

function clearSelection() {
  if (props.disabled) return;
  emit('update:modelValue', []);
}

function clearDraft() {
  draftSelection.value = [];
}

function selectAllFiltered() {
  const set = new Set(draftSelection.value);
  for (const row of filteredRows.value) set.add(row.value);
  draftSelection.value = [...set];
}

function isSelected(value: string) {
  return draftSelection.value.includes(value);
}

function toggleRow(value: string) {
  const set = new Set(draftSelection.value);
  if (set.has(value)) set.delete(value);
  else set.add(value);
  draftSelection.value = [...set];
}

function onRowClick(_event: Event, row: { item: EventCatalogRow }) {
  toggleRow(row.item.value);
}

function addCustomEvent() {
  customError.value = '';
  const created = isLinux.value
    ? createCustomLinuxEventSelection({
        packageName: resolveChannelInput(customLinuxPackage.value),
        matchKey: customLinuxMatchKey.value,
        label: customLabel.value,
      })
    : createCustomEventSelection({
        channel: resolveChannelInput(customChannel.value),
        eventId: Number(customEventId.value),
        label: customLabel.value,
      });

  if (!created) {
    customError.value = t(
      isLinux.value
        ? 'alarmCenter.scenarioStudio.eventSelector.customLinuxInvalid'
        : 'alarmCenter.scenarioStudio.eventSelector.customInvalid',
    );
    return;
  }
  rememberCustom(created);
  if (!draftSelection.value.includes(created.value)) {
    draftSelection.value = [...draftSelection.value, created.value];
  }
  if (isLinux.value) {
    customLinuxPackage.value = created.channel;
    customLinuxMatchKey.value = '';
  } else {
    customChannel.value = created.channel;
  }
  channelFilter.value = created.channel;
  tableSearch.value = isLinux.value ? created.matchKey : String(created.eventId);
  customLabel.value = '';
}

function confirmSelection() {
  const next: EventCatalogSelection[] = [];
  const seen = new Set<string>();
  for (const value of draftSelection.value) {
    if (seen.has(value)) continue;
    seen.add(value);
    const row = rowByValue.value.get(value);
    if (row) {
      next.push(rowToSelection(row));
      continue;
    }
    const custom = customByValue.value[value];
    if (custom) {
      next.push(custom);
      continue;
    }
    const existing = selected.value.find(item => item.value === value);
    if (existing) next.push(existing);
  }
  emit('update:modelValue', next);
  dialogOpen.value = false;
}

function cancelDialog() {
  dialogOpen.value = false;
}

function onSortBy(value: { key: string; order: 'asc' | 'desc' }[]) {
  sortBy.value = value?.length
    ? value
    : [{ key: isLinux.value ? 'channelLabel' : 'eventId', order: 'asc' }];
}
</script>

<template>
  <div class="ac-event-selector">
    <div v-if="label" class="text-caption mb-1">{{ label }}</div>
    <div
      class="ac-event-selector__box"
      :class="{ 'ac-event-selector__box--disabled': disabled }"
      @click="openDialog"
    >
      <div class="ac-event-selector__chips">
        <v-chip
          v-for="item in selected"
          :key="item.value"
          size="small"
          closable
          :disabled="disabled"
          @click:close.stop="removeChip(item.value)"
        >
          {{ chipLabel(item) }}
        </v-chip>
        <span v-if="!selected.length" class="text-body-2 text-medium-emphasis">
          {{ t(isLinux
            ? 'alarmCenter.scenarioStudio.eventSelector.placeholderLinux'
            : 'alarmCenter.scenarioStudio.eventSelector.placeholder') }}
        </span>
      </div>
      <div class="ac-event-selector__actions">
        <v-btn
          v-if="selected.length && !disabled"
          icon="mdi-close"
          variant="text"
          size="x-small"
          :aria-label="t('alarmCenter.scenarioStudio.eventSelector.clear')"
          @click.stop="clearSelection"
        />
        <v-btn
          icon="mdi-table-search"
          variant="text"
          size="small"
          :disabled="disabled"
          :aria-label="t('alarmCenter.scenarioStudio.eventSelector.open')"
          @click.stop="openDialog"
        />
      </div>
    </div>

    <v-dialog v-model="dialogOpen" max-width="920" scrollable>
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-semibold pa-4 pb-2">
          {{ t(isLinux
            ? 'alarmCenter.scenarioStudio.eventSelector.dialogTitleLinux'
            : 'alarmCenter.scenarioStudio.eventSelector.dialogTitle') }}
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <div class="d-flex flex-wrap ga-2 mb-3">
            <v-text-field
              v-model="tableSearch"
              :placeholder="t(isLinux
                ? 'alarmCenter.scenarioStudio.eventSelector.searchHintLinux'
                : 'alarmCenter.scenarioStudio.eventSelector.searchHint')"
              prepend-inner-icon="mdi-magnify"
              density="comfortable"
              variant="outlined"
              hide-details
              clearable
              class="ac-event-selector__search"
            />
            <v-select
              v-model="channelFilter"
              :items="channelItems"
              item-title="title"
              item-value="value"
              :label="t(isLinux
                ? 'alarmCenter.scenarioStudio.eventSelector.packageFilter'
                : 'alarmCenter.scenarioStudio.eventSelector.channelFilter')"
              density="comfortable"
              variant="outlined"
              hide-details
              clearable
              class="ac-event-selector__channel"
            />
          </div>

          <div class="d-flex flex-wrap align-center ga-2 mb-3">
            <v-btn
              size="small"
              variant="tonal"
              color="primary"
              :disabled="!filteredRows.length || loading"
              @click="selectAllFiltered"
            >
              {{ t('alarmCenter.scenarioStudio.eventSelector.selectFiltered', { count: filteredRows.length }) }}
            </v-btn>
            <v-btn
              size="small"
              variant="text"
              :disabled="!draftSelection.length"
              @click="clearDraft"
            >
              {{ t('alarmCenter.scenarioStudio.eventSelector.clearSelection') }}
            </v-btn>
            <span v-if="draftSelection.length" class="text-caption text-medium-emphasis">
              {{ t('alarmCenter.scenarioStudio.eventSelector.selectedCount', { count: draftSelection.length }) }}
            </span>
          </div>

          <div class="ac-event-selector__custom mb-3">
            <div class="text-caption text-medium-emphasis mb-2">
              {{ t(isLinux
                ? 'alarmCenter.scenarioStudio.eventSelector.customHintLinux'
                : 'alarmCenter.scenarioStudio.eventSelector.customHint') }}
            </div>
            <div class="d-flex flex-wrap ga-2 align-start">
              <template v-if="isLinux">
                <v-combobox
                  v-model="customLinuxPackage"
                  :items="channelItems"
                  item-title="title"
                  item-value="value"
                  :label="t('alarmCenter.scenarioStudio.eventSelector.colPackage')"
                  density="compact"
                  variant="outlined"
                  hide-details
                  class="ac-event-selector__custom-channel"
                />
                <v-text-field
                  v-model="customLinuxMatchKey"
                  :label="t('alarmCenter.scenarioStudio.eventSelector.colMatchKey')"
                  density="compact"
                  variant="outlined"
                  hide-details
                  class="ac-event-selector__custom-id"
                  @keyup.enter="addCustomEvent"
                />
              </template>
              <template v-else>
                <v-combobox
                  v-model="customChannel"
                  :items="channelItems"
                  item-title="title"
                  item-value="value"
                  :label="t('alarmCenter.scenarioStudio.eventSelector.customChannel')"
                  density="compact"
                  variant="outlined"
                  hide-details
                  class="ac-event-selector__custom-channel"
                />
                <v-text-field
                  v-model.number="customEventId"
                  type="number"
                  min="1"
                  :label="t('alarmCenter.scenarioStudio.eventSelector.customEventId')"
                  density="compact"
                  variant="outlined"
                  hide-details
                  class="ac-event-selector__custom-id"
                  @keyup.enter="addCustomEvent"
                />
              </template>
              <v-text-field
                v-model="customLabel"
                :label="t('alarmCenter.scenarioStudio.eventSelector.customLabel')"
                density="compact"
                variant="outlined"
                hide-details
                class="ac-event-selector__custom-label"
                @keyup.enter="addCustomEvent"
              />
              <v-btn
                color="primary"
                variant="tonal"
                class="mt-1"
                prepend-icon="mdi-plus"
                @click="addCustomEvent"
              >
                {{ t('alarmCenter.scenarioStudio.eventSelector.customAdd') }}
              </v-btn>
            </div>
            <div v-if="customError" class="text-caption text-error mt-1">{{ customError }}</div>
          </div>

          <div class="ac-event-selector__table-wrap">
            <v-data-table
              v-model:page="tablePage"
              v-model:items-per-page="tableItemsPerPage"
              :headers="headers"
              :items="sortedRows"
              :sort-by="sortBy"
              :loading="loading"
              item-value="value"
              density="compact"
              hover
              fixed-header
              height="360"
              class="ac-event-selector__table"
              @click:row="onRowClick"
              @update:sort-by="onSortBy"
            >
              <template #item.selected="{ item }">
                <v-checkbox-btn
                  :model-value="isSelected(item.value)"
                  @click.stop
                  @update:model-value="toggleRow(item.value)"
                />
              </template>
              <template #item.eventId="{ item }">
                <span class="font-mono">{{ item.eventId }}</span>
              </template>
              <template #item.matchKey="{ item }">
                <span class="font-mono text-medium-emphasis">{{ item.matchKey || '—' }}</span>
              </template>
            </v-data-table>
          </div>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="cancelDialog">
            {{ t('alarmCenter.scenarioStudio.eventSelector.cancel') }}
          </v-btn>
          <v-btn color="primary" variant="flat" @click="confirmSelection">
            {{ t('alarmCenter.scenarioStudio.eventSelector.confirm') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.ac-event-selector__box {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  min-height: 40px;
  padding: 6px 8px 6px 12px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
  cursor: pointer;
  background: rgb(var(--v-theme-surface));
}

.ac-event-selector__box--disabled {
  opacity: 0.6;
  cursor: default;
  pointer-events: none;
}

.ac-event-selector__chips {
  flex: 1;
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  align-items: center;
  min-height: 28px;
}

.ac-event-selector__actions {
  display: flex;
  align-items: center;
  flex-shrink: 0;
}

.ac-event-selector__search {
  flex: 1 1 240px;
  min-width: 180px;
}

.ac-event-selector__channel {
  flex: 0 1 260px;
  min-width: 180px;
}

.ac-event-selector__custom {
  padding: 10px 12px;
  border: 1px dashed rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.02);
}

.ac-event-selector__custom-channel {
  flex: 1 1 180px;
  min-width: 160px;
}

.ac-event-selector__custom-id {
  flex: 0 1 160px;
  min-width: 120px;
}

.ac-event-selector__custom-label {
  flex: 1 1 180px;
  min-width: 140px;
}

.ac-event-selector__table-wrap {
  width: 100%;
  max-width: 100%;
  overflow-x: auto;
  overflow-y: hidden;
  -webkit-overflow-scrolling: touch;
}

.ac-event-selector__table {
  min-width: 640px;
}

.ac-event-selector__table :deep(.v-table__wrapper) {
  overflow-x: auto !important;
}

.ac-event-selector__table :deep(tbody tr) {
  cursor: pointer;
}

.ac-event-selector__table :deep(th),
.ac-event-selector__table :deep(td) {
  white-space: nowrap;
}
</style>
