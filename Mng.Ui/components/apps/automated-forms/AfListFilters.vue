<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcPersonPickerAutocomplete from '@/components/apps/operation-core/OcPersonPickerAutocomplete.vue';
import type { AfFilterColumn, AfListFilter, AfListFilterKind } from '@/utils/afListFilters';

interface AdvancedRow {
  id: number;
  field: string;
  operator: string;
  value: unknown;
}

const props = defineProps<{
  columns: AfFilterColumn[];
  relationOptionsByKey?: Record<string, { value: string; title: string }[]>;
  groupOptionsByKey?: Record<string, { value: string; title: string }[]>;
}>();

const emit = defineEmits<{
  'update:filters': [AfListFilter[]];
  'advanced-open': [];
}>();

const { t } = useAppI18n();

const panelOpen = ref(false);
const advancedRows = ref<AdvancedRow[]>([]);
let rowSeq = 0;

watch(panelOpen, (open, wasOpen) => {
  if (open && !wasOpen) emit('advanced-open');
});

const OPERATORS_BY_KIND: Record<AfListFilterKind, string[]> = {
  text: ['contains', 'eq', 'ne', 'startsWith', 'endsWith'],
  number: ['eq', 'ne', 'gt', 'gte', 'lt', 'lte'],
  bool: ['eq', 'ne'],
  date: ['gte', 'lte', 'gt', 'lt', 'ne', 'eq'],
  select: ['in', 'nin', 'eq', 'ne'],
  relation: ['in', 'nin', 'eq', 'ne'],
  person: ['eq', 'ne'],
  group: ['in', 'nin', 'eq', 'ne'],
};

const boolOptions = computed(() => [
  { value: 'true', title: t('automated-forms.view.cellFormat.yes') },
  { value: 'false', title: t('automated-forms.view.cellFormat.no') },
]);

function defaultOperatorForKind(kind: AfListFilterKind): string {
  return OPERATORS_BY_KIND[kind]?.[0] ?? 'eq';
}

const columnByKey = computed(() => new Map(props.columns.map((c) => [c.key, c])));

function kindOf(field: string): AfListFilterKind | null {
  return columnByKey.value.get(field)?.kind ?? null;
}

function isSelectKind(kind: AfListFilterKind | null): boolean {
  return kind === 'select' || kind === 'relation' || kind === 'group';
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

function isMultiSelectOp(op: string): boolean {
  return op === 'in' || op === 'nin';
}

function isSelectField(field: string): boolean {
  return isSelectKind(kindOf(field));
}

function isPersonField(field: string): boolean {
  return kindOf(field) === 'person';
}

function isNumberField(field: string): boolean {
  return kindOf(field) === 'number';
}

function isDateField(field: string): boolean {
  return kindOf(field) === 'date';
}

function isBoolField(field: string): boolean {
  return kindOf(field) === 'bool';
}

function sortFilterSelectOptions(items: { value: string; title: string }[]): { value: string; title: string }[] {
  return [...items].sort((a, b) =>
    a.title.localeCompare(b.title, 'tr', { numeric: true, sensitivity: 'base' })
  );
}

function selectOptionsForField(field: string): { value: string; title: string }[] {
  const kind = kindOf(field);
  const col = columnByKey.value.get(field);
  if (kind === 'select') return col?.selectItems ?? [];
  if (kind === 'relation') {
    return sortFilterSelectOptions(props.relationOptionsByKey?.[field] ?? []);
  }
  if (kind === 'group') {
    return sortFilterSelectOptions(props.groupOptionsByKey?.[field] ?? []);
  }
  return [];
}

function defaultValueFor(kind: AfListFilterKind, op: string): unknown {
  if (isSelectKind(kind)) return isMultiSelectOp(op) ? [] : null;
  if (kind === 'person') return null;
  if (kind === 'bool') return null;
  return '';
}

function toIsoUtc(raw: string): string | null {
  const d = new Date(raw);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

function formatDateFilterValue(raw: string): string | null {
  const trimmed = raw.trim();
  if (!trimmed) return null;
  // Takvim günü (YYYY-MM-DD) — DG ISO string alanları ile uyumlu
  if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) return trimmed;
  const iso = toIsoUtc(trimmed);
  if (!iso) return null;
  // datetime-local → yalnızca gün kısmını kullan (timezone kayması önlenir)
  return iso.slice(0, 10);
}

function addAdvancedRow() {
  panelOpen.value = true;
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
  if (isSelectKind(kind) || kind === 'bool') {
    row.value = defaultValueFor(kind, row.operator);
  }
}

function clearAll() {
  advancedRows.value = [];
}

function buildFilters(): AfListFilter[] {
  const out: AfListFilter[] = [];
  for (const row of advancedRows.value) {
    const kind = kindOf(row.field);
    if (!kind || !row.operator) continue;

    let value = '';
    if (isSelectKind(kind) && isMultiSelectOp(row.operator)) {
      const ids = Array.isArray(row.value) ? (row.value as string[]).filter(Boolean) : [];
      if (!ids.length) continue;
      value = ids.join(',');
    } else if (kind === 'bool') {
      const b = typeof row.value === 'string' ? row.value : '';
      if (b !== 'true' && b !== 'false') continue;
      value = b;
    } else if (kind === 'date') {
      const raw = typeof row.value === 'string' ? row.value.trim() : '';
      const day = formatDateFilterValue(raw);
      if (!day) continue;
      value = day;
    } else if (kind === 'person') {
      const id = typeof row.value === 'string' ? row.value.trim() : '';
      if (!id) continue;
      value = id;
    } else {
      value = typeof row.value === 'string' ? row.value.trim() : String(row.value ?? '').trim();
      if (!value) continue;
    }

    out.push({ field: row.field, operator: row.operator, value });
  }
  return out;
}

const activeCount = computed(() => buildFilters().length);

watch(
  advancedRows,
  () => {
    emit('update:filters', buildFilters());
  },
  { deep: true }
);

watch(
  () => props.columns.map((c) => c.key).join('|'),
  () => {
    const allowed = new Set(props.columns.map((c) => c.key));
    advancedRows.value = advancedRows.value.filter((r) => !r.field || allowed.has(r.field));
  }
);
</script>

<template>
  <div v-if="columns.length" class="af-list-filters mb-4">
    <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-2">
      <div class="text-caption font-weight-medium d-flex align-center ga-1">
        <v-icon icon="mdi-tune-variant" size="18" />
        {{ t('operationCore.board.filters.advanced.title') }}
        <v-chip v-if="activeCount" size="x-small" color="primary" variant="flat" class="ml-1">
          {{ activeCount }}
        </v-chip>
      </div>
      <div class="d-flex align-center ga-1">
        <v-btn
          size="x-small"
          variant="text"
          class="text-none"
          :prepend-icon="panelOpen ? 'mdi-chevron-up' : 'mdi-chevron-down'"
          @click="panelOpen = !panelOpen"
        >
          {{ panelOpen ? t('operationCore.board.filters.advanced.hide') : t('operationCore.board.filters.advanced.show') }}
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

    <v-expand-transition>
      <div v-show="panelOpen" class="af-advanced-search pa-3 rounded-lg">
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
              v-if="isSelectField(row.field) && isMultiSelectOp(row.operator)"
              v-model="row.value"
              :items="selectOptionsForField(row.field)"
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
              v-else-if="isSelectField(row.field)"
              v-model="row.value"
              :items="selectOptionsForField(row.field)"
              item-title="title"
              item-value="value"
              :label="t('operationCore.board.filters.advanced.value')"
              variant="outlined"
              density="compact"
              hide-details
              clearable
            />
            <v-select
              v-else-if="isBoolField(row.field)"
              v-model="row.value"
              :items="boolOptions"
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
              v-else-if="isNumberField(row.field)"
              v-model="row.value"
              type="number"
              :label="t('operationCore.board.filters.advanced.value')"
              variant="outlined"
              density="compact"
              hide-details
              :disabled="!row.field"
            />
            <v-text-field
              v-else-if="isDateField(row.field)"
              v-model="row.value"
              type="date"
              :label="t('operationCore.board.filters.advanced.value')"
              variant="outlined"
              density="compact"
              hide-details
              :disabled="!row.field"
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
.af-advanced-search {
  background-color: rgba(var(--v-theme-primary), 0.04);
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
