<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { pmSearchProjectWorkItems } from '@/services/projectManagementService';
import type { PmWorkItemCandidate } from '@/types/apps/projectManagement';

const props = withDefaults(
  defineProps<{
    modelValue: boolean;
    projectId: string;
    excludeIds?: string[];
    title?: string | null;
    hint?: string | null;
    confirmLabel?: string | null;
  }>(),
  {
    excludeIds: () => [],
    title: null,
    hint: null,
    confirmLabel: null,
  },
);

const emit = defineEmits<{
  'update:modelValue': [boolean];
  pick: [PmWorkItemCandidate];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const open = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value),
});

const searchQuery = ref('');
const loading = ref(false);
const items = ref<PmWorkItemCandidate[]>([]);
const total = ref(0);
const page = ref(1);
const itemsPerPage = ref(25);
const selectedId = ref<string | null>(null);

const pageSizeOptions = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
  { value: 100, title: '100' },
];

const excludedSet = computed(() => {
  const ids = new Set<string>();
  for (const id of props.excludeIds ?? []) {
    const value = id?.trim().toLowerCase();
    if (value) ids.add(value);
  }
  return ids;
});

const displayed = computed(() =>
  items.value.filter((row) => !excludedSet.value.has(row.id.trim().toLowerCase())),
);

const selected = computed(() => displayed.value.find((row) => row.id === selectedId.value) ?? null);

const dialogTitle = computed(() => props.title || t('projectManagement.pickWorkItem.title'));
const dialogHint = computed(() => props.hint || t('projectManagement.pickWorkItem.hint'));
const dialogConfirm = computed(() => props.confirmLabel || t('projectManagement.pickWorkItem.confirm'));

const headers = computed(() => [
  { title: t('projectManagement.pickWorkItem.colKey'), key: 'key', width: 140 },
  { title: t('projectManagement.pickWorkItem.colTitle'), key: 'title', minWidth: 220 },
  { title: t('projectManagement.fields.status'), key: 'state', width: 140 },
]);

function stateLabel(row: PmWorkItemCandidate) {
  if (row.stateName) return row.stateName;
  if (row.closed) return t('projectManagement.pickWorkItem.closed');
  return '—';
}

function stateColor(row: PmWorkItemCandidate) {
  if (row.closed) return 'default';
  if (row.stateCategory === 'done') return 'success';
  if (row.stateCategory === 'blocked') return 'error';
  return 'info';
}

function rowClass(item: PmWorkItemCandidate) {
  return item.id === selectedId.value ? 'pm-pick-work-row-selected' : undefined;
}

async function loadItems() {
  if (!open.value || !props.projectId) {
    items.value = [];
    total.value = 0;
    return;
  }
  loading.value = true;
  try {
    const take = itemsPerPage.value > 0 ? itemsPerPage.value : 25;
    const skip = Math.max(0, (page.value - 1) * take);
    const result = await pmSearchProjectWorkItems(props.projectId, searchQuery.value, { skip, take });
    items.value = result.items;
    total.value = result.total;
    if (selectedId.value && !displayed.value.some((row) => row.id === selectedId.value)) {
      selectedId.value = null;
    }
  } catch (error) {
    items.value = [];
    total.value = 0;
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    loading.value = false;
  }
}

function onRowClick(_event: Event, ctx: { item: PmWorkItemCandidate }) {
  selectedId.value = ctx.item.id;
}

function onRowDblClick(_event: Event, ctx: { item: PmWorkItemCandidate }) {
  selectedId.value = ctx.item.id;
  confirmPick();
}

function confirmPick() {
  const row = selected.value;
  if (!row) return;
  emit('pick', row);
  open.value = false;
}

let searchDebounce: ReturnType<typeof setTimeout> | null = null;
watch(searchQuery, () => {
  if (!open.value) return;
  if (searchDebounce) clearTimeout(searchDebounce);
  searchDebounce = setTimeout(() => {
    if (page.value === 1) void loadItems();
    else page.value = 1;
  }, 300);
});

watch([page, itemsPerPage], () => {
  if (!open.value) return;
  void loadItems();
});

watch(open, (isOpen) => {
  if (!isOpen) {
    selectedId.value = null;
    searchQuery.value = '';
    items.value = [];
    total.value = 0;
    page.value = 1;
    return;
  }
  page.value = 1;
  void loadItems();
});
</script>

<template>
  <v-dialog v-model="open" max-width="880" scrollable>
    <v-card rounded="lg">
      <v-card-title class="d-flex align-start ga-3 px-6 py-4">
        <v-avatar color="primary" variant="tonal" rounded="lg">
          <v-icon icon="mdi-briefcase-outline" />
        </v-avatar>
        <div class="flex-grow-1">
          <div>{{ dialogTitle }}</div>
          <div class="text-body-2 text-medium-emphasis font-weight-regular mt-1">{{ dialogHint }}</div>
        </div>
      </v-card-title>
      <v-divider />
      <v-card-text class="px-6 py-4">
        <v-text-field
          v-model="searchQuery"
          :label="t('projectManagement.pickWorkItem.search')"
          :placeholder="t('projectManagement.searchWorkItem')"
          variant="outlined"
          density="compact"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          class="mb-3"
        />
        <v-data-table-server
          v-model:page="page"
          v-model:items-per-page="itemsPerPage"
          :headers="headers"
          :items="displayed"
          :items-length="total"
          :items-per-page-options="pageSizeOptions"
          :loading="loading"
          item-value="id"
          density="comfortable"
          hover
          fixed-header
          height="420"
          class="rounded-lg border pm-pick-work-table"
          :row-props="({ item }) => ({ class: rowClass(item) })"
          @click:row="onRowClick"
          @dblclick:row="onRowDblClick"
        >
          <template #item.key="{ item }">
            <span class="font-weight-medium">{{ item.key || item.id.slice(0, 8) }}</span>
          </template>
          <template #item.title="{ item }">
            {{ item.title || '—' }}
          </template>
          <template #item.state="{ item }">
            <v-chip size="small" variant="tonal" :color="stateColor(item)">
              {{ stateLabel(item) }}
            </v-chip>
          </template>
          <template #no-data>
            <div class="text-center py-8 text-medium-emphasis">
              {{ t('projectManagement.pickWorkItem.empty') }}
            </div>
          </template>
        </v-data-table-server>
      </v-card-text>
      <v-divider />
      <v-card-actions class="px-6 py-3">
        <div v-if="selected" class="text-caption text-medium-emphasis">
          {{ selected.key }} · {{ selected.title }}
        </div>
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="open = false">
          {{ t('projectManagement.cancel') }}
        </v-btn>
        <v-btn color="primary" variant="flat" class="text-none" :disabled="!selected" @click="confirmPick">
          {{ dialogConfirm }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.pm-pick-work-table :deep(tr.pm-pick-work-row-selected) {
  background: rgba(var(--v-theme-primary), 0.08);
}

.pm-pick-work-table :deep(tbody tr) {
  cursor: pointer;
}
</style>
