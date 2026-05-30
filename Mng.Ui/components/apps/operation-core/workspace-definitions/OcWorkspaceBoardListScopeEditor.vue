<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcBoardCatalogLabel from '@/components/apps/operation-core/OcBoardCatalogLabel.vue';
import type {
  OpBoardColumnConfig,
  OpBoardListColumnConfig,
  OpBoardSortConfig,
  OcSortDirection,
  OpField,
  OpPriority,
  OpState,
  OpStateFlow,
  OpWorkItemType,
} from '@/types/apps/operationCore';
import { buildCatalogDisplayMap } from '@/utils/ocCatalogDisplay';
import {
  boardListColumnKeys,
  buildListScopeColumns,
  defaultFilterableForKey,
  defaultSortableForKey,
  listScopeStateIdsFromColumns,
  OC_BOARD_LIST_TABLE_COLUMN_KEYS,
  suggestListScopeStateIdsFromFlow,
} from '@/utils/ocBoardListColumns';

const props = defineProps<{
  columns: OpBoardColumnConfig[];
  listColumns: OpBoardListColumnConfig[];
  defaultSort: OpBoardSortConfig | null;
  stateFlowId: string;
  stateFlows: OpStateFlow[];
  stateItems: { value: string; title: string }[];
  enabledStateIds: string[];
  stateCatalog?: OpState[];
  priorityCatalog?: OpPriority[];
  typeCatalog?: OpWorkItemType[];
  fieldCatalog?: OpField[];
}>();

const emit = defineEmits<{
  'update:columns': [OpBoardColumnConfig[]];
  'update:listColumns': [OpBoardListColumnConfig[]];
  'update:defaultSort': [OpBoardSortConfig | null];
}>();

const { t } = useAppI18n();

const activeFlow = computed(
  () => props.stateFlows.find((f) => f.__dataId === props.stateFlowId) ?? null
);

const stateTitleById = computed(
  () => new Map(props.stateItems.map((s) => [s.value, s.title]))
);

const enabledStateIdSet = computed(() => new Set(props.enabledStateIds));

const selectableStates = computed(() => {
  if (enabledStateIdSet.value.size === 0) return props.stateItems;
  return props.stateItems.filter((s) => enabledStateIdSet.value.has(s.value));
});

const selectedStateIds = computed(() => listScopeStateIdsFromColumns(props.columns));

const selectedStateSet = computed(() => new Set(selectedStateIds.value));

const poolFieldOptions = computed(() =>
  (props.fieldCatalog ?? [])
    .filter((f) => f.key)
    .map((f) => ({
      value: f.key,
      title: f.label?.trim() || f.key,
    }))
);

const poolFieldKeys = computed(() => poolFieldOptions.value.map((o) => o.value));

const tableColumnOptions = computed(() => {
  const core = OC_BOARD_LIST_TABLE_COLUMN_KEYS.map((value) => ({
    value: value as string,
    title: t(`operationCore.workspaceDefinitions.boards.listTableColumns.${value}`),
  }));
  return [...core, ...poolFieldOptions.value];
});

const columnLabelByKey = computed(() => {
  const map = new Map<string, string>();
  for (const opt of tableColumnOptions.value) map.set(opt.value, opt.title);
  return map;
});

function columnLabel(key: string): string {
  return columnLabelByKey.value.get(key) ?? key;
}

// Seçili sütunlar (sıralı) — source of truth props.listColumns.
const selectedColumns = computed(() => props.listColumns ?? []);

const selectedColumnKeys = computed({
  get: () => boardListColumnKeys(selectedColumns.value),
  set: (keys: unknown[]) => setSelectedColumns(keys.map((k) => String(k))),
});

// Varsayılan sıralama için yalnızca "sortable" işaretli sütunlar.
const sortableColumnOptions = computed(() =>
  selectedColumns.value
    .filter((c) => c.sortable)
    .map((c) => ({ value: c.key, title: columnLabel(c.key) }))
);

const defaultSortField = computed({
  get: () => props.defaultSort?.field ?? null,
  set: (field: string | null) => {
    if (!field) {
      emit('update:defaultSort', null);
      return;
    }
    emit('update:defaultSort', { field, direction: props.defaultSort?.direction ?? 'asc' });
  },
});

const defaultSortDirection = computed({
  get: () => props.defaultSort?.direction ?? 'asc',
  set: (direction: OcSortDirection) => {
    if (!props.defaultSort?.field) return;
    emit('update:defaultSort', { field: props.defaultSort.field, direction });
  },
});

function setSelectedColumns(keys: string[]) {
  const existing = new Map(selectedColumns.value.map((c) => [c.key, c]));
  const next: OpBoardListColumnConfig[] = [];
  const seen = new Set<string>();
  for (const key of keys) {
    if (!key || seen.has(key)) continue;
    seen.add(key);
    const prev = existing.get(key);
    next.push(
      prev
        ? { ...prev }
        : { key, sortable: defaultSortableForKey(key), filterable: defaultFilterableForKey(key) }
    );
  }
  emitColumns(next);
}

function emitColumns(next: OpBoardListColumnConfig[]) {
  emit('update:listColumns', next);
  // Kaldırılan sütun varsayılan sıralama alanıysa temizle.
  if (props.defaultSort?.field && !next.some((c) => c.key === props.defaultSort?.field)) {
    emit('update:defaultSort', null);
  }
}

function moveColumn(index: number, dir: -1 | 1) {
  const next = [...selectedColumns.value];
  const target = index + dir;
  if (target < 0 || target >= next.length) return;
  [next[index], next[target]] = [next[target], next[index]];
  emit('update:listColumns', next);
}

function removeColumn(index: number) {
  const next = selectedColumns.value.filter((_, i) => i !== index);
  emitColumns(next);
}

function updateColumnFlag(index: number, field: 'sortable' | 'filterable', value: boolean) {
  const next = selectedColumns.value.map((c, i) => (i === index ? { ...c, [field]: value } : c));
  // Bir sütun sortable'dan çıkarsa ve varsayılan sıralama oysa temizle.
  emit('update:listColumns', next);
  if (field === 'sortable' && !value && props.defaultSort?.field === next[index]?.key) {
    emit('update:defaultSort', null);
  }
}

const stateCatalogById = computed(() => buildCatalogDisplayMap(props.stateCatalog ?? []));
const priorityCatalogById = computed(() => buildCatalogDisplayMap(props.priorityCatalog ?? []));
const typeCatalogById = computed(() => buildCatalogDisplayMap(props.typeCatalog ?? []));

const catalogPreviewColumns = computed(() =>
  selectedColumnKeys.value.filter((key) => key === 'stateId' || key === 'priorityId' || key === 'typeId')
);

function catalogPreviewItem(columnKey: string) {
  if (columnKey === 'stateId') {
    const id = selectedStateIds.value[0];
    if (!id) return null;
    return stateCatalogById.value.get(id) ?? { id, name: stateTitleById.value.get(id) ?? id, color: null, icon: null };
  }
  if (columnKey === 'priorityId') {
    const first = props.priorityCatalog?.[0];
    return first ? priorityCatalogById.value.get(first.__dataId) ?? null : null;
  }
  if (columnKey === 'typeId') {
    const first = props.typeCatalog?.[0];
    return first ? typeCatalogById.value.get(first.__dataId) ?? null : null;
  }
  return null;
}

function stateChipCatalog(stateId: string) {
  return (
    stateCatalogById.value.get(stateId) ?? {
      id: stateId,
      name: stateTitleById.value.get(stateId) ?? stateId,
      color: null,
      icon: null,
    }
  );
}

function patchColumns(next: OpBoardColumnConfig[]) {
  emit('update:columns', next);
}

function setSelectedStates(stateIds: string[]) {
  patchColumns(buildListScopeColumns(stateIds, stateTitleById.value));
}

function toggleState(stateId: string) {
  const set = new Set(selectedStateIds.value);
  if (set.has(stateId)) set.delete(stateId);
  else set.add(stateId);
  setSelectedStates([...set]);
}

function applyFromFlow() {
  const flow = activeFlow.value;
  if (!flow) return;
  setSelectedStates(suggestListScopeStateIdsFromFlow(flow));
}

function selectAllStates() {
  setSelectedStates(selectableStates.value.map((s) => s.value));
}

function clearStates() {
  patchColumns([]);
}
</script>

<template>
  <div class="oc-board-list-scope-editor">
    <v-alert type="info" variant="tonal" density="compact" class="rounded-lg mb-4">
      <div class="text-body-2 font-weight-medium mb-1">
        {{ t('operationCore.workspaceDefinitions.boards.listScopeIntroTitle') }}
      </div>
      <p class="text-body-2 mb-0">
        {{ t('operationCore.workspaceDefinitions.boards.listScopeIntroBody') }}
      </p>
    </v-alert>

    <v-card variant="outlined" rounded="lg" class="pa-4 mb-4">
      <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-3">
        <div>
          <div class="text-subtitle-2 font-weight-bold">
            {{ t('operationCore.workspaceDefinitions.boards.listScopeStatesTitle') }}
          </div>
          <p class="text-caption text-medium-emphasis mb-0">
            {{ t('operationCore.workspaceDefinitions.boards.listScopeStatesHint') }}
          </p>
        </div>
        <div class="d-flex flex-wrap gap-2">
          <v-btn
            size="small"
            variant="tonal"
            rounded="lg"
            class="text-none"
            :disabled="!activeFlow"
            @click="applyFromFlow"
          >
            {{ t('operationCore.workspaceDefinitions.boards.listScopeFromFlow') }}
          </v-btn>
          <v-btn size="small" variant="text" class="text-none" @click="selectAllStates">
            {{ t('operationCore.workspaceDefinitions.boards.listScopeSelectAll') }}
          </v-btn>
          <v-btn
            size="small"
            variant="text"
            class="text-none"
            :disabled="selectedStateIds.length === 0"
            @click="clearStates"
          >
            {{ t('operationCore.workspaceDefinitions.boards.listScopeClear') }}
          </v-btn>
        </div>
      </div>

      <v-alert
        v-if="!stateFlowId"
        type="warning"
        variant="tonal"
        density="compact"
        class="mb-3 rounded-lg"
      >
        {{ t('operationCore.workspaceDefinitions.boards.selectFlowFirst') }}
      </v-alert>

      <div v-if="selectableStates.length === 0" class="text-body-2 text-medium-emphasis">
        {{ t('operationCore.workspaceDefinitions.boards.listScopeNoStates') }}
      </div>

      <div v-else class="d-flex flex-wrap gap-2">
        <v-chip
          v-for="state in selectableStates"
          :key="state.value"
          :color="selectedStateSet.has(state.value) ? 'primary' : undefined"
          :variant="selectedStateSet.has(state.value) ? 'flat' : 'outlined'"
          rounded="lg"
          class="cursor-pointer"
          @click="toggleState(state.value)"
        >
          <v-icon
            :icon="selectedStateSet.has(state.value) ? 'mdi-check' : 'mdi-plus'"
            start
            size="16"
          />
          <OcBoardCatalogLabel :item="stateChipCatalog(state.value)" />
        </v-chip>
      </div>

      <p v-if="selectedStateIds.length > 0" class="text-caption text-medium-emphasis mt-3 mb-0">
        {{
          t('operationCore.workspaceDefinitions.boards.listScopeSelectedCount', {
            count: selectedStateIds.length,
          })
        }}
      </p>
    </v-card>

    <v-card variant="outlined" rounded="lg" class="pa-4">
      <div class="text-subtitle-2 font-weight-bold mb-1">
        {{ t('operationCore.workspaceDefinitions.boards.listTableColumnsTitle') }}
      </div>
      <p class="text-caption text-medium-emphasis mb-3">
        {{ t('operationCore.workspaceDefinitions.boards.listTableColumnsHint') }}
      </p>
      <v-select
        v-model="selectedColumnKeys"
        :items="tableColumnOptions"
        item-title="title"
        item-value="value"
        :label="t('operationCore.workspaceDefinitions.boards.listTableColumnsLabel')"
        variant="outlined"
        density="comfortable"
        multiple
        chips
        closable-chips
      />

      <div v-if="selectedColumns.length > 0" class="oc-list-cols mt-2">
        <div class="oc-list-cols__head text-caption text-medium-emphasis">
          <span class="oc-list-cols__order">{{ t('operationCore.workspaceDefinitions.boards.listColumnOrder') }}</span>
          <span class="oc-list-cols__name">{{ t('operationCore.workspaceDefinitions.boards.listColumnField') }}</span>
          <span class="oc-list-cols__flag">{{ t('operationCore.workspaceDefinitions.boards.listColumnSortable') }}</span>
          <span class="oc-list-cols__flag">{{ t('operationCore.workspaceDefinitions.boards.listColumnFilterable') }}</span>
          <span class="oc-list-cols__actions" />
        </div>
        <div
          v-for="(col, index) in selectedColumns"
          :key="col.key"
          class="oc-list-cols__row"
        >
          <div class="oc-list-cols__order d-flex align-center ga-1">
            <v-btn
              icon="mdi-chevron-up"
              size="x-small"
              variant="text"
              :disabled="index === 0"
              @click="moveColumn(index, -1)"
            />
            <v-btn
              icon="mdi-chevron-down"
              size="x-small"
              variant="text"
              :disabled="index === selectedColumns.length - 1"
              @click="moveColumn(index, 1)"
            />
          </div>
          <span class="oc-list-cols__name text-body-2">{{ columnLabel(col.key) }}</span>
          <div class="oc-list-cols__flag">
            <v-switch
              :model-value="col.sortable"
              color="primary"
              density="compact"
              hide-details
              inset
              @update:model-value="updateColumnFlag(index, 'sortable', $event === true)"
            />
          </div>
          <div class="oc-list-cols__flag">
            <v-switch
              :model-value="col.filterable"
              color="primary"
              density="compact"
              hide-details
              inset
              @update:model-value="updateColumnFlag(index, 'filterable', $event === true)"
            />
          </div>
          <div class="oc-list-cols__actions">
            <v-btn icon="mdi-close" size="x-small" variant="text" @click="removeColumn(index)" />
          </div>
        </div>
      </div>

      <div class="mt-4 pt-3 border-t">
        <div class="text-caption font-weight-medium mb-2">
          {{ t('operationCore.workspaceDefinitions.boards.listDefaultSortTitle') }}
        </div>
        <p class="text-caption text-medium-emphasis mb-2">
          {{ t('operationCore.workspaceDefinitions.boards.listDefaultSortHint') }}
        </p>
        <div class="d-flex flex-wrap align-center ga-3">
          <v-select
            v-model="defaultSortField"
            :items="sortableColumnOptions"
            item-title="title"
            item-value="value"
            :label="t('operationCore.workspaceDefinitions.boards.listDefaultSortField')"
            :no-data-text="t('operationCore.workspaceDefinitions.boards.listDefaultSortNoSortable')"
            variant="outlined"
            density="compact"
            clearable
            hide-details
            style="max-width: 280px"
          />
          <v-btn-toggle
            v-model="defaultSortDirection"
            :disabled="!defaultSortField"
            density="compact"
            variant="outlined"
            divided
            mandatory
          >
            <v-btn value="asc" size="small" class="text-none">
              <v-icon icon="mdi-sort-ascending" start size="18" />
              {{ t('operationCore.workspaceDefinitions.boards.sortAsc') }}
            </v-btn>
            <v-btn value="desc" size="small" class="text-none">
              <v-icon icon="mdi-sort-descending" start size="18" />
              {{ t('operationCore.workspaceDefinitions.boards.sortDesc') }}
            </v-btn>
          </v-btn-toggle>
        </div>
      </div>

      <div v-if="catalogPreviewColumns.length > 0" class="mt-4 pt-3 border-t">
        <div class="text-caption font-weight-medium mb-2">
          {{ t('operationCore.workspaceDefinitions.boards.listTableColumnsPreviewTitle') }}
        </div>
        <p class="text-caption text-medium-emphasis mb-2">
          {{ t('operationCore.workspaceDefinitions.boards.listTableColumnsPreviewHint') }}
        </p>
        <div class="d-flex flex-wrap ga-3">
          <div
            v-for="colKey in catalogPreviewColumns"
            :key="colKey"
            class="d-flex flex-column ga-1"
          >
            <span class="text-caption text-medium-emphasis">
              {{ t(`operationCore.workspaceDefinitions.boards.listTableColumns.${colKey}`) }}
            </span>
            <OcBoardCatalogLabel :item="catalogPreviewItem(colKey)" />
          </div>
        </div>
      </div>
    </v-card>
  </div>
</template>

<style scoped>
.cursor-pointer {
  cursor: pointer;
}

.border-t {
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.oc-list-cols__head,
.oc-list-cols__row {
  display: grid;
  grid-template-columns: 72px 1fr 84px 84px 40px;
  align-items: center;
  gap: 0.5rem;
  padding: 0.25rem 0.25rem;
}

.oc-list-cols__head {
  padding-bottom: 0.25rem;
}

.oc-list-cols__row {
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.oc-list-cols__flag {
  display: flex;
  justify-content: center;
}

.oc-list-cols__actions {
  display: flex;
  justify-content: flex-end;
}
</style>
