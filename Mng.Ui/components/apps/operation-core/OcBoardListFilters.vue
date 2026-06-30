<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import type { OcBoardListFilter } from '@/types/apps/operationCore';
import { collectPersonIdsFromValue } from '@/utils/ocPersonPicker';

export type OcBoardFilterKind = 'state' | 'priority' | 'type' | 'person' | 'relation' | 'group' | 'tags' | 'number' | 'date' | 'text';

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
  /** Pool relation alanları için key → option listesi (value=__dataId, title=ad). */
  relationOptionsByKey?: Record<string, { value: string; title: string }[]>;
  /** @deprecated Merkezi MngDirectoryPickerField kullanılıyor; geriye dönük uyumluluk için bırakıldı. */
  groupOptionsByKey?: Record<string, { value: string; title: string }[]>;
  /** Pool tags alanları için key → mevcut etiket değerleri (combobox önerileri; serbest giriş açık). */
  tagOptionsByKey?: Record<string, string[]>;
}>();

const emit = defineEmits<{
  'update:filters': [OcBoardListFilter[]];
  'advanced-open': [];
}>();

const { t } = useAppI18n();

const panelOpen = ref(false);
const advancedRows = ref<AdvancedRow[]>([]);
let rowSeq = 0;

watch(panelOpen, (open, wasOpen) => {
  if (open && !wasOpen) emit('advanced-open');
});

const OPERATORS_BY_KIND: Record<OcBoardFilterKind, string[]> = {
  state: ['in', 'nin', 'eq', 'ne'],
  priority: ['in', 'nin', 'eq', 'ne'],
  type: ['in', 'nin', 'eq', 'ne'],
  person: ['eq', 'ne'],
  relation: ['in', 'nin', 'eq', 'ne'],
  group: ['in', 'nin', 'eq', 'ne'],
  tags: ['in', 'nin', 'eq', 'ne'],
  number: ['eq', 'ne', 'gt', 'gte', 'lt', 'lte'],
  date: ['gte', 'lte', 'gt', 'lt', 'ne', 'eq'],
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

function isSelectKind(kind: OcBoardFilterKind | null): boolean {
  return isCatalogKind(kind) || kind === 'relation';
}

function isGroupField(field: string): boolean {
  return kindOf(field) === 'group';
}

function catalogOptions(kind: OcBoardFilterKind): { value: string; title: string }[] {
  if (kind === 'state') return props.stateOptions;
  if (kind === 'priority') return props.priorityOptions;
  if (kind === 'type') return props.typeOptions;
  return [];
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
  return isSelectKind(kindOf(field));
}

function isPersonField(field: string): boolean {
  return kindOf(field) === 'person';
}

function isTagsField(field: string): boolean {
  return kindOf(field) === 'tags';
}

function tagComboItems(field: string): string[] {
  return props.tagOptionsByKey?.[field] ?? [];
}

function tagValueList(value: unknown): string[] {
  if (!Array.isArray(value)) return [];
  return value.map((v) => String(v).trim()).filter((s) => s.length > 0);
}

function isNumberField(field: string): boolean {
  return kindOf(field) === 'number';
}

function isDateField(field: string): boolean {
  return kindOf(field) === 'date';
}

function toIsoUtc(raw: string): string | null {
  const d = new Date(raw);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

function scalarFilterValue(kind: OcBoardFilterKind, operator: string, raw: unknown): string | null {
  if (isSelectKind(kind) && isMultiCatalogOp(operator)) return null;
  if (isSelectKind(kind)) {
    if (Array.isArray(raw)) {
      const ids = raw.filter((v) => v != null && String(v).trim() !== '').map((v) => String(v).trim());
      return ids[0] ?? null;
    }
    if (raw == null || raw === '') return null;
    return String(raw).trim() || null;
  }
  if (kind === 'tags' && isMultiCatalogOp(operator)) return null;
  if (kind === 'tags') {
    const tags = tagValueList(raw);
    return tags[0] ?? null;
  }
  if (kind === 'person') {
    const ids = collectPersonIdsFromValue(raw);
    return ids[0] ?? null;
  }
  if (kind === 'number') {
    if (raw == null || raw === '') return null;
    return String(raw).trim() || null;
  }
  if (kind === 'date') {
    const s = typeof raw === 'string' ? raw.trim() : '';
    return s ? toIsoUtc(s) : null;
  }
  const text = typeof raw === 'string' ? raw.trim() : raw == null ? '' : String(raw).trim();
  return text || null;
}

function catalogOptionsForField(field: string): { value: string; title: string }[] {
  const kind = kindOf(field);
  if (!kind) return [];
  if (isCatalogKind(kind)) return catalogOptions(kind);
  if (kind === 'relation') return props.relationOptionsByKey?.[field] ?? [];
  return [];
}

function defaultValueFor(kind: OcBoardFilterKind, op: string): unknown {
  if (isSelectKind(kind)) return isMultiCatalogOp(op) ? [] : null;
  if (kind === 'tags') return isMultiCatalogOp(op) ? [] : '';
  if (kind === 'person') return null;
  return '';
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
  if (isSelectKind(kind) || kind === 'tags') {
    row.value = defaultValueFor(kind, row.operator);
  }
}

function clearAll() {
  advancedRows.value = [];
}

function togglePanel() {
  panelOpen.value = !panelOpen.value;
  if (panelOpen.value && !advancedRows.value.length) {
    addAdvancedRow();
  }
}

function buildFilters(): OcBoardListFilter[] {
  const out: OcBoardListFilter[] = [];
  for (const row of advancedRows.value) {
    const kind = kindOf(row.field);
    if (!kind || !row.operator) continue;

    let value = '';
    if (isSelectKind(kind) && isMultiCatalogOp(row.operator)) {
      const ids = Array.isArray(row.value) ? (row.value as string[]).filter(Boolean) : [];
      if (!ids.length) continue;
      value = ids.join(',');
    } else if (kind === 'tags' && isMultiCatalogOp(row.operator)) {
      const tags = tagValueList(row.value);
      if (!tags.length) continue;
      value = tags.join(',');
    } else {
      const scalar = scalarFilterValue(kind, row.operator, row.value);
      if (!scalar) continue;
      value = scalar;
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
  <div
    v-if="columns.length"
    class="oc-board-list-filters"
    :class="{ 'oc-board-list-filters--open': panelOpen }"
  >
    <div class="d-flex align-center flex-wrap ga-1">
      <v-btn
        size="small"
        variant="text"
        class="text-none px-2"
        :color="panelOpen || activeCount ? 'primary' : undefined"
        @click="togglePanel"
      >
        <v-icon icon="mdi-filter-variant" size="18" start />
        {{ t('operationCore.board.filters.advanced.title') }}
        <v-chip
          v-if="activeCount"
          size="x-small"
          color="primary"
          variant="flat"
          class="ml-1"
        >
          {{ activeCount }}
        </v-chip>
        <v-icon
          :icon="panelOpen ? 'mdi-chevron-up' : 'mdi-chevron-down'"
          size="18"
          end
        />
      </v-btn>
      <v-btn
        v-if="panelOpen"
        size="x-small"
        variant="tonal"
        color="primary"
        class="text-none"
        prepend-icon="mdi-plus"
        @click="addAdvancedRow"
      >
        {{ t('operationCore.board.filters.advanced.addCondition') }}
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

    <v-expand-transition>
      <div v-show="panelOpen" class="oc-advanced-search mt-1 pa-2 rounded-lg">
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
            <v-combobox
              v-else-if="isTagsField(row.field) && (row.operator === 'in' || row.operator === 'nin')"
              v-model="row.value"
              :items="tagComboItems(row.field)"
              :label="t('operationCore.board.filters.advanced.value')"
              variant="outlined"
              density="compact"
              hide-details
              multiple
              chips
              closable-chips
              clearable
            />
            <v-combobox
              v-else-if="isTagsField(row.field)"
              v-model="row.value"
              :items="tagComboItems(row.field)"
              :label="t('operationCore.board.filters.advanced.value')"
              variant="outlined"
              density="compact"
              hide-details
              clearable
            />
            <MngDirectoryPickerField
              v-else-if="isGroupField(row.field) && isMultiCatalogOp(row.operator)"
              v-model="row.value"
              entity="group"
              multiple
              :label="t('operationCore.board.filters.advanced.value')"
              density="compact"
              variant="outlined"
              hide-details
            />
            <MngDirectoryPickerField
              v-else-if="isGroupField(row.field)"
              v-model="row.value"
              entity="group"
              :label="t('operationCore.board.filters.advanced.value')"
              density="compact"
              variant="outlined"
              hide-details
            />
            <MngDirectoryPickerField
              v-else-if="isPersonField(row.field)"
              v-model="row.value"
              entity="user"
              :label="t('operationCore.board.filters.advanced.value')"
              density="compact"
              variant="outlined"
              hide-details
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
              type="datetime-local"
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
      </div>
    </v-expand-transition>
  </div>
</template>

<style scoped>
.oc-board-list-filters--open {
  flex: 1 1 100%;
}

.oc-advanced-search {
  background-color: rgba(var(--v-theme-primary), 0.04);
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
