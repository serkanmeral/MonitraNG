<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcPersonPickerAutocomplete from '@/components/apps/operation-core/OcPersonPickerAutocomplete.vue';
import type { OcBoardListFilter } from '@/types/apps/operationCore';

export type OcBoardFilterKind = 'state' | 'priority' | 'type' | 'person' | 'text';

export interface OcBoardFilterColumn {
  key: string;
  label: string;
  kind: OcBoardFilterKind;
}

interface AdvancedRow {
  id: number;
  field: string;
  operator: string;
  value: unknown;
}

const props = defineProps<{
  columns: OcBoardFilterColumn[];
  stateOptions: { value: string; title: string }[];
  priorityOptions: { value: string; title: string }[];
  typeOptions: { value: string; title: string }[];
}>();

const emit = defineEmits<{
  'update:filters': [OcBoardListFilter[]];
}>();

const { t } = useAppI18n();

// --- Hızlı filtre (kolon başına). Katalog: id[]; person: id|null; text: string. ---
const values = reactive<Record<string, unknown>>({});

// --- Gelişmiş arama (çok satırlı, AND). ---
const advancedOpen = ref(false);
const advancedRows = ref<AdvancedRow[]>([]);
let rowSeq = 0;

const OPERATORS_BY_KIND: Record<OcBoardFilterKind, string[]> = {
  state: ['in', 'nin', 'eq', 'ne'],
  priority: ['in', 'nin', 'eq', 'ne'],
  type: ['in', 'nin', 'eq', 'ne'],
  person: ['eq', 'ne'],
  text: ['contains', 'eq', 'ne', 'startsWith', 'endsWith'],
};

function defaultOperatorForKind(kind: OcBoardFilterKind): string {
  return OPERATORS_BY_KIND[kind]?.[0] ?? 'eq';
}

const columnByKey = computed(() => new Map(props.columns.map((c) => [c.key, c])));

function kindOf(field: string): OcBoardFilterKind | null {
  return columnByKey.value.get(field)?.kind ?? null;
}

function isCatalogKind(kind: OcBoardFilterKind | null): boolean {
  return kind === 'state' || kind === 'priority' || kind === 'type';
}

function catalogOptions(kind: OcBoardFilterKind): { value: string; title: string }[] {
  if (kind === 'state') return props.stateOptions;
  if (kind === 'priority') return props.priorityOptions;
  if (kind === 'type') return props.typeOptions;
  return [];
}

function textValue(key: string): string {
  const v = values[key];
  return typeof v === 'string' ? v : '';
}

function setTextValue(key: string, v: string | null) {
  values[key] = v ?? '';
}

const fieldOptions = computed(() =>
  props.columns.map((c) => ({ value: c.key, title: c.label }))
);

function operatorOptions(field: string): { value: string; title: string }[] {
  const kind = kindOf(field);
  if (!kind) return [];
  return OPERATORS_BY_KIND[kind].map((op) => ({
    value: op,
    title: t(`operationCore.board.filters.operators.${op}`),
  }));
}

function isMultiCatalogOp(op: string): boolean {
  return op === 'in' || op === 'nin';
}

function isCatalogField(field: string): boolean {
  return isCatalogKind(kindOf(field));
}

function isPersonField(field: string): boolean {
  return kindOf(field) === 'person';
}

function catalogOptionsForField(field: string): { value: string; title: string }[] {
  const kind = kindOf(field);
  return kind && isCatalogKind(kind) ? catalogOptions(kind) : [];
}

function defaultValueFor(kind: OcBoardFilterKind, op: string): unknown {
  if (isCatalogKind(kind)) return isMultiCatalogOp(op) ? [] : null;
  if (kind === 'person') return null;
  return '';
}

function addAdvancedRow() {
  advancedOpen.value = true;
  advancedRows.value.push({ id: ++rowSeq, field: '', operator: '', value: null });
}

function removeAdvancedRow(id: number) {
  advancedRows.value = advancedRows.value.filter((r) => r.id !== id);
}

function onRowFieldChange(row: AdvancedRow) {
  const kind = kindOf(row.field);
  if (!kind) {
    row.operator = '';
    row.value = null;
    return;
  }
  row.operator = defaultOperatorForKind(kind);
  row.value = defaultValueFor(kind, row.operator);
}

function onRowOperatorChange(row: AdvancedRow) {
  const kind = kindOf(row.field);
  if (!kind) return;
  // Katalog: in/nin (çoklu) ↔ eq/ne (tekli) geçişinde değer tipini uyarla.
  if (isCatalogKind(kind)) {
    row.value = defaultValueFor(kind, row.operator);
  }
}

function clearAdvanced() {
  advancedRows.value = [];
}

function clearAll() {
  for (const key of Object.keys(values)) {
    delete values[key];
  }
  clearAdvanced();
}

function quickFilters(): OcBoardListFilter[] {
  const out: OcBoardListFilter[] = [];
  for (const col of props.columns) {
    const v = values[col.key];
    if (isCatalogKind(col.kind)) {
      const ids = Array.isArray(v) ? (v as string[]).filter(Boolean) : [];
      if (ids.length) out.push({ field: col.key, operator: 'in', value: ids.join(',') });
    } else if (col.kind === 'person') {
      const id = typeof v === 'string' ? v.trim() : '';
      if (id) out.push({ field: col.key, operator: 'eq', value: id });
    } else {
      const text = typeof v === 'string' ? v.trim() : '';
      if (text) out.push({ field: col.key, operator: 'contains', value: text });
    }
  }
  return out;
}

function advancedFilters(): OcBoardListFilter[] {
  const out: OcBoardListFilter[] = [];
  for (const row of advancedRows.value) {
    const kind = kindOf(row.field);
    if (!kind || !row.operator) continue;

    let value = '';
    if (isCatalogKind(kind) && isMultiCatalogOp(row.operator)) {
      const ids = Array.isArray(row.value) ? (row.value as string[]).filter(Boolean) : [];
      if (!ids.length) continue;
      value = ids.join(',');
    } else {
      value = typeof row.value === 'string' ? row.value.trim() : '';
      if (!value) continue;
    }

    out.push({ field: row.field, operator: row.operator, value });
  }
  return out;
}

function buildFilters(): OcBoardListFilter[] {
  return [...quickFilters(), ...advancedFilters()];
}

const activeCount = computed(() => buildFilters().length);

watch(
  [values, advancedRows],
  () => {
    emit('update:filters', buildFilters());
  },
  { deep: true }
);

// Filtrelenebilir sütun seti değişirse, artık geçerli olmayan değerleri/satırları temizle.
watch(
  () => props.columns.map((c) => c.key).join('|'),
  () => {
    const allowed = new Set(props.columns.map((c) => c.key));
    for (const key of Object.keys(values)) {
      if (!allowed.has(key)) delete values[key];
    }
    advancedRows.value = advancedRows.value.filter((r) => !r.field || allowed.has(r.field));
  }
);
</script>

<template>
  <div v-if="columns.length" class="oc-board-list-filters">
    <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-2">
      <div class="text-caption font-weight-medium d-flex align-center ga-1">
        <v-icon icon="mdi-filter-variant" size="18" />
        {{ t('operationCore.board.filters.title') }}
        <v-chip v-if="activeCount" size="x-small" color="primary" variant="flat" class="ml-1">
          {{ activeCount }}
        </v-chip>
      </div>
      <div class="d-flex align-center ga-1">
        <v-btn
          size="x-small"
          variant="text"
          class="text-none"
          :prepend-icon="advancedOpen ? 'mdi-chevron-up' : 'mdi-tune-variant'"
          @click="advancedOpen = !advancedOpen"
        >
          {{ advancedOpen ? t('operationCore.board.filters.advanced.hide') : t('operationCore.board.filters.advanced.show') }}
        </v-btn>
        <v-btn
          v-if="activeCount"
          size="x-small"
          variant="text"
          class="text-none"
          @click="clearAll"
        >
          {{ t('operationCore.board.filters.clear') }}
        </v-btn>
      </div>
    </div>

    <v-row dense>
      <v-col
        v-for="col in columns"
        :key="col.key"
        cols="12"
        sm="6"
        md="4"
        lg="3"
      >
        <v-select
          v-if="col.kind === 'state' || col.kind === 'priority' || col.kind === 'type'"
          v-model="values[col.key]"
          :items="catalogOptions(col.kind)"
          item-title="title"
          item-value="value"
          :label="col.label"
          variant="outlined"
          density="compact"
          hide-details
          multiple
          chips
          closable-chips
          clearable
        />
        <OcPersonPickerAutocomplete
          v-else-if="col.kind === 'person'"
          v-model="values[col.key]"
          :label="col.label"
          density="compact"
          variant="outlined"
          :hide-details="true"
        />
        <v-text-field
          v-else
          :model-value="textValue(col.key)"
          :label="col.label"
          :placeholder="t('operationCore.board.filters.containsPlaceholder')"
          variant="outlined"
          density="compact"
          hide-details
          clearable
          @update:model-value="setTextValue(col.key, $event)"
        />
      </v-col>
    </v-row>

    <v-expand-transition>
      <div v-show="advancedOpen" class="oc-advanced-search mt-3 pa-3 rounded-lg">
        <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-2">
          <div class="text-caption font-weight-medium d-flex align-center ga-1">
            <v-icon icon="mdi-tune-variant" size="18" />
            {{ t('operationCore.board.filters.advanced.title') }}
          </div>
          <v-btn
            v-if="advancedRows.length"
            size="x-small"
            variant="text"
            class="text-none"
            @click="clearAdvanced"
          >
            {{ t('operationCore.board.filters.advanced.clear') }}
          </v-btn>
        </div>

        <p class="text-caption text-medium-emphasis mb-2">
          {{ t('operationCore.board.filters.advanced.andHint') }}
        </p>

        <p v-if="!advancedRows.length" class="text-caption text-disabled mb-2">
          {{ t('operationCore.board.filters.advanced.empty') }}
        </p>

        <v-row
          v-for="row in advancedRows"
          :key="row.id"
          dense
          align="center"
          class="mb-1"
        >
          <v-col cols="12" sm="4" md="3">
            <v-select
              v-model="row.field"
              :items="fieldOptions"
              item-title="title"
              item-value="value"
              :label="t('operationCore.board.filters.advanced.field')"
              :placeholder="t('operationCore.board.filters.advanced.selectField')"
              variant="outlined"
              density="compact"
              hide-details
              @update:model-value="onRowFieldChange(row)"
            />
          </v-col>
          <v-col cols="12" sm="3" md="3">
            <v-select
              v-model="row.operator"
              :items="operatorOptions(row.field)"
              item-title="title"
              item-value="value"
              :label="t('operationCore.board.filters.advanced.operator')"
              variant="outlined"
              density="compact"
              hide-details
              :disabled="!row.field"
              @update:model-value="onRowOperatorChange(row)"
            />
          </v-col>
          <v-col cols="11" sm="4" md="5">
            <v-select
              v-if="isCatalogField(row.field) && (row.operator === 'in' || row.operator === 'nin')"
              v-model="row.value"
              :items="catalogOptionsForField(row.field)"
              item-title="title"
              item-value="value"
              :label="t('operationCore.board.filters.advanced.value')"
              variant="outlined"
              density="compact"
              hide-details
              multiple
              chips
              closable-chips
              clearable
            />
            <v-select
              v-else-if="isCatalogField(row.field)"
              v-model="row.value"
              :items="catalogOptionsForField(row.field)"
              item-title="title"
              item-value="value"
              :label="t('operationCore.board.filters.advanced.value')"
              variant="outlined"
              density="compact"
              hide-details
              clearable
            />
            <OcPersonPickerAutocomplete
              v-else-if="isPersonField(row.field)"
              v-model="row.value"
              :label="t('operationCore.board.filters.advanced.value')"
              density="compact"
              variant="outlined"
              :hide-details="true"
            />
            <v-text-field
              v-else
              v-model="row.value"
              :label="t('operationCore.board.filters.advanced.value')"
              variant="outlined"
              density="compact"
              hide-details
              :disabled="!row.field"
            />
          </v-col>
          <v-col cols="1" class="d-flex justify-center">
            <v-btn
              icon="mdi-close"
              variant="text"
              size="small"
              density="comfortable"
              @click="removeAdvancedRow(row.id)"
            />
          </v-col>
        </v-row>

        <v-btn
          size="small"
          variant="tonal"
          color="primary"
          class="text-none mt-1"
          prepend-icon="mdi-plus"
          @click="addAdvancedRow"
        >
          {{ t('operationCore.board.filters.advanced.addCondition') }}
        </v-btn>
      </div>
    </v-expand-transition>
  </div>
</template>

<style scoped>
.oc-advanced-search {
  background-color: rgba(var(--v-theme-primary), 0.04);
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
