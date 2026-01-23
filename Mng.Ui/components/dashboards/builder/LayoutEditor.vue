<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue';
import type { DashboardLayout, LayoutRow, LayoutCol } from '@/stores/apps/dashboard';
import WidgetPickerModal from './WidgetPickerModal.vue';

const props = defineProps<{
  modelValue: DashboardLayout;
  disabled?: boolean;
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: DashboardLayout];
}>();

const layout = computed({
  get: () => props.modelValue,
  set: (v: DashboardLayout) => emit('update:modelValue', v),
});

const lbl = (key: string) => props.t?.(`dashboards.builder.layout.${key}`) ?? key;

const showWidgetPicker = ref(false);
const widgetPickerTarget = ref<{ row: number; col: number } | null>(null);

// Local rows for drag & drop - add unique keys for draggable
const initRows = (rows: LayoutRow[] = []): LayoutRow[] => {
  return rows.map((row, idx) => ({
    ...row,
    __key: `row-${Date.now()}-${idx}-${Math.random()}`,
    cols: (row.cols ?? []).map((col, colIdx) => ({
      ...col,
      __key: col.__key || `col-${idx}-${colIdx}-${Math.random()}`,
    })),
  }));
};

const localRows = ref<LayoutRow[]>(initRows(props.modelValue.rows ?? []));

// Track if update is from internal change to prevent recursive updates
const isUpdatingFromInternal = ref(false);

// Watch props.modelValue.rows and update localRows (only if not from internal update)
watch(() => props.modelValue.rows, (newRows) => {
  if (isUpdatingFromInternal.value) {
    isUpdatingFromInternal.value = false;
    return;
  }
  if (newRows && Array.isArray(newRows)) {
    const newRowsStr = JSON.stringify(newRows);
    const currentRowsStr = JSON.stringify(localRows.value);
    if (newRowsStr !== currentRowsStr) {
      localRows.value = initRows(newRows);
    }
  }
}, { immediate: true });

// Watch localRows and emit changes (skip if flag is set)
watch(localRows, (newRows) => {
  if (isUpdatingFromInternal.value) {
    // This update came from props, don't emit back
    isUpdatingFromInternal.value = false;
    return;
  }
  // This is an internal change, emit to parent
  isUpdatingFromInternal.value = true;
  // Remove __key before emitting (it's only for local draggable state)
  const rowsToEmit = newRows.map(({ __key, ...row }) => row);
  emit('update:modelValue', { type: 'rows', rows: rowsToEmit });
}, { deep: true, flush: 'post' });

function openWidgetPicker(rowIndex: number, colIndex: number) {
  widgetPickerTarget.value = { row: rowIndex, col: colIndex };
  showWidgetPicker.value = true;
}

function onWidgetSelected(widgetId: string) {
  if (!widgetPickerTarget.value) return;
  const { row: rowIdx, col: colIdx } = widgetPickerTarget.value;
  updateCol(rowIdx, colIdx, { widgetId, rows: undefined });
  widgetPickerTarget.value = null;
}

function addRow() {
  const newRow: LayoutRow = { 
    cols: [{ span: 12, widgetId: '' }],
    __key: `row-${Date.now()}-${Math.random()}`,
  };
  
  // Force reactivity by creating new array reference
  // This ensures draggable component detects the change
  const updatedRows = [...localRows.value, newRow];
  localRows.value = updatedRows;
  // Watch will automatically emit the change
}

function removeRow(rowIndex: number) {
  const rows = [...localRows.value];
  rows.splice(rowIndex, 1);
  if (rows.length === 0) rows.push({ cols: [{ span: 12, widgetId: '' }] });
  localRows.value = rows;
}

function addColumn(rowIndex: number) {
  const currentRow = localRows.value[rowIndex];
  if (!currentRow) {
    return;
  }
  
  const newCol: LayoutCol = { 
    span: 6, 
    widgetId: '',
    __key: `col-${Date.now()}-${Math.random()}`,
  };
  
  const updatedRows = localRows.value.map((r, i) => {
    if (i === rowIndex) {
      return { ...r, cols: [...r.cols, newCol] };
    }
    return r;
  });
  
  localRows.value = updatedRows;
}

function removeColumn(rowIndex: number, colIndex: number) {
  const rows = (localRows.value ?? []).map((r, i) => {
    if (i !== rowIndex) return { ...r };
    const cols = r.cols.filter((_, j) => j !== colIndex);
    if (cols.length === 0) return { ...r, cols: [{ span: 12, widgetId: '' }] };
    return { ...r, cols };
  });
  localRows.value = rows;
}

// Column drag handlers
function handleColumnDragEnd(rowIndex: number, event: any) {
  // Column order already updated by draggable
  // The computed setter will handle the emit
}

// Column local state for drag & drop
function setLocalCols(rowIndex: number, cols: LayoutCol[]) {
  const rows = [...localRows.value];
  rows[rowIndex] = { ...rows[rowIndex], cols };
  localRows.value = rows;
}

function updateCol(rowIndex: number, colIndex: number, patch: Partial<LayoutCol>) {
  const rows = (localRows.value ?? []).map((r, i) => {
    if (i !== rowIndex) return { ...r };
    const cols = r.cols.map((c, j) => (j === colIndex ? { ...c, ...patch } : { ...c }));
    return { ...r, cols };
  });
  localRows.value = rows;
}

function rowSpanTotal(row: LayoutRow): number {
  return (row.cols ?? []).reduce((s, c) => s + (c.span ?? 12), 0);
}

function clampSpan(v: number) {
  return Math.max(1, Math.min(12, Number.isNaN(v) ? 12 : Math.round(v)));
}
</script>

<template>
  <div class="layout-editor">
    <div class="text-subtitle-1 font-weight-medium mb-3">
      {{ t?.('dashboards.builder.layout.sectionTitle') ?? 'Layout' }}
    </div>

    <div class="draggable-rows">
      <div
        v-for="(row, rowIdx) in localRows"
        :key="row.__key || `row-${rowIdx}`"
        class="layout-row mb-4"
      >
          <v-card variant="outlined" class="overflow-hidden layout-row-card">
        <div class="d-flex align-center pa-2 border-b bg-surface-variant">
          <v-icon class="drag-handle-row mr-2" size="20" color="medium-emphasis" style="cursor: move;">
            mdi-drag-vertical
          </v-icon>
          <span class="text-body-2 font-weight-medium">
            {{ lbl('row') }} {{ rowIdx + 1 }}
          </span>
          <v-spacer />
          <v-btn
            v-if="(localRows?.length ?? 0) > 1"
            icon
            size="x-small"
            variant="text"
            color="error"
            :disabled="disabled"
            @click="removeRow(rowIdx)"
          >
            <v-icon size="18">mdi-delete-outline</v-icon>
            <v-tooltip activator="parent" location="top">{{ lbl('removeRow') }}</v-tooltip>
          </v-btn>
        </div>
        <v-card-text class="pa-3">
          <div class="d-flex flex-wrap ga-2">
            <div
              v-for="(col, colIdx) in row.cols"
              :key="col.__key || `col-${rowIdx}-${colIdx}`"
              class="layout-col-wrapper"
              style="flex: 0 0 auto; min-width: 280px; max-width: 100%;"
            >
              <v-card variant="tonal" class="pa-3 layout-col-card">
                <div class="d-flex justify-space-between align-center mb-2">
                  <div class="d-flex align-center ga-1">
                    <v-icon class="drag-handle-col" size="16" color="medium-emphasis" style="cursor: move;">
                      mdi-drag-horizontal
                    </v-icon>
                    <span class="text-caption font-weight-medium">{{ lbl('col') }} {{ colIdx + 1 }}</span>
                  </div>
                  <v-btn
                    v-if="row.cols.length > 1"
                    icon
                    size="x-small"
                    variant="text"
                    color="error"
                    :disabled="disabled"
                    @click="removeColumn(rowIdx, colIdx)"
                  >
                    <v-icon size="16">mdi-close</v-icon>
                  </v-btn>
                </div>
                <div class="d-flex flex-wrap ga-2 mb-2">
                  <v-text-field
                    :model-value="col.span ?? 12"
                    type="number"
                    :label="lbl('span')"
                    variant="outlined"
                    density="compact"
                    hide-details
                    min="1"
                    max="12"
                    style="max-width: 64px;"
                    :disabled="disabled"
                    @update:model-value="(v) => updateCol(rowIdx, colIdx, { span: clampSpan(Number(v)) })"
                  />
                  <v-text-field
                    :model-value="col.spanSm ?? ''"
                    type="number"
                    :label="'sm'"
                    variant="outlined"
                    density="compact"
                    hide-details
                    min="1"
                    max="12"
                    style="max-width: 56px;"
                    :disabled="disabled"
                    placeholder="—"
                    @update:model-value="(v) => updateCol(rowIdx, colIdx, { spanSm: v === '' || v == null ? undefined : clampSpan(Number(v)) })"
                  />
                  <v-text-field
                    :model-value="col.spanMd ?? ''"
                    type="number"
                    :label="'md'"
                    variant="outlined"
                    density="compact"
                    hide-details
                    min="1"
                    max="12"
                    style="max-width: 56px;"
                    :disabled="disabled"
                    placeholder="—"
                    @update:model-value="(v) => updateCol(rowIdx, colIdx, { spanMd: v === '' || v == null ? undefined : clampSpan(Number(v)) })"
                  />
                  <v-text-field
                    :model-value="col.spanLg ?? ''"
                    type="number"
                    :label="'lg'"
                    variant="outlined"
                    density="compact"
                    hide-details
                    min="1"
                    max="12"
                    style="max-width: 56px;"
                    :disabled="disabled"
                    placeholder="—"
                    @update:model-value="(v) => updateCol(rowIdx, colIdx, { spanLg: v === '' || v == null ? undefined : clampSpan(Number(v)) })"
                  />
                  <v-text-field
                    :model-value="col.spanXl ?? ''"
                    type="number"
                    :label="'xl'"
                    variant="outlined"
                    density="compact"
                    hide-details
                    min="1"
                    max="12"
                    style="max-width: 56px;"
                    :disabled="disabled"
                    placeholder="—"
                    @update:model-value="(v) => updateCol(rowIdx, colIdx, { spanXl: v === '' || v == null ? undefined : clampSpan(Number(v)) })"
                  />
                </div>
                <!-- Nested rows or widget -->
                <div v-if="col.rows && col.rows.length" class="nested-rows mt-2">
                  <v-card variant="outlined" class="pa-2 bg-surface">
                    <div class="d-flex justify-space-between align-center mb-2">
                      <div class="text-caption text-medium-emphasis">
                        {{ t?.('dashboards.builder.layout.nestedRows') ?? 'İç içe satırlar' }}
                      </div>
                      <v-btn
                        size="x-small"
                        variant="text"
                        color="error"
                        :disabled="disabled"
                        @click="updateCol(rowIdx, colIdx, { rows: undefined, widgetId: '' })"
                      >
                        <v-icon start size="14">mdi-close</v-icon>
                        {{ lbl('removeNestedRows') }}
                      </v-btn>
                    </div>
                    <dashboards-builder-layout-editor
                      :model-value="{ type: 'rows', rows: col.rows }"
                      :disabled="disabled"
                      :t="t"
                      @update:model-value="(v) => updateCol(rowIdx, colIdx, { rows: v.rows })"
                    />
                  </v-card>
                </div>
                <div v-else>
                  <div class="d-flex align-center ga-2 mb-2">
                    <v-text-field
                      :model-value="col.widgetId ?? ''"
                      :label="lbl('widgetId')"
                      :placeholder="t?.('dashboards.builder.layout.widgetPlaceholder') ?? 'Widget ID (opsiyonel)'"
                      variant="outlined"
                      density="compact"
                      hide-details
                      :disabled="disabled"
                      style="flex: 1;"
                      @update:model-value="(v) => updateCol(rowIdx, colIdx, { widgetId: (v ?? '').trim() || undefined })"
                    />
                    <v-btn
                      icon
                      size="small"
                      variant="tonal"
                      color="primary"
                      :disabled="disabled"
                      @click="openWidgetPicker(rowIdx, colIdx)"
                    >
                      <v-icon size="18">mdi-widgets</v-icon>
                      <v-tooltip activator="parent" location="top">{{ lbl('selectWidget') }}</v-tooltip>
                    </v-btn>
                  </div>
                  <div class="d-flex ga-1">
                    <v-btn
                      size="x-small"
                      variant="tonal"
                      color="secondary"
                      :disabled="disabled"
                      @click="() => updateCol(rowIdx, colIdx, { rows: [{ cols: [{ span: 12, widgetId: '' }] }], widgetId: undefined })"
                    >
                      <v-icon start size="14">mdi-view-grid-plus</v-icon>
                      {{ t?.('dashboards.builder.layout.addNestedRows') ?? 'İç satır ekle' }}
                    </v-btn>
                    <v-btn
                      v-if="col.widgetId"
                      size="x-small"
                      variant="text"
                      color="error"
                      :disabled="disabled"
                      @click="updateCol(rowIdx, colIdx, { widgetId: undefined })"
                    >
                      <v-icon start size="14">mdi-close</v-icon>
                      {{ lbl('removeWidget') }}
                    </v-btn>
                  </div>
                </div>
              </v-card>
              </div>
            </div>
          <v-btn
            size="small"
            variant="tonal"
            color="primary"
            class="mt-2"
            :disabled="disabled"
            @click="addColumn(rowIdx)"
          >
            <v-icon start size="18">mdi-plus</v-icon>
            {{ lbl('addCol') }}
          </v-btn>
          <v-alert
            v-if="rowSpanTotal(row) > 12"
            type="warning"
            variant="tonal"
            density="compact"
            class="mt-2"
          >
            {{ lbl('spanTotalWarning') }}
          </v-alert>
        </v-card-text>
        </v-card>
      </div>
    </div>

    <v-btn
      variant="outlined"
      color="primary"
      block
      :disabled="disabled"
      @click="addRow"
    >
      <v-icon start size="20">mdi-plus</v-icon>
      {{ lbl('addRow') }}
    </v-btn>

    <!-- Widget Picker Modal -->
    <dashboards-builder-widget-picker-modal
      v-model="showWidgetPicker"
      :disabled="disabled"
      :t="t"
      @select="onWidgetSelected"
    />
  </div>
</template>

<style scoped>
.layout-editor {
  position: relative;
}

.layout-row-card {
  transition: box-shadow 0.2s, transform 0.2s;
}

.layout-row-card:hover {
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
}

.layout-col-card {
  transition: box-shadow 0.2s, transform 0.2s;
  min-width: 280px;
}

.layout-col-card:hover {
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.draggable-rows {
  min-height: 50px;
}

.ghost-row {
  opacity: 0.5;
  background-color: rgba(var(--v-theme-primary), 0.1);
  border: 2px dashed rgba(var(--v-theme-primary), 0.3);
}

.ghost-col {
  opacity: 0.5;
  background-color: rgba(var(--v-theme-primary), 0.1);
  border: 2px dashed rgba(var(--v-theme-primary), 0.3);
}

.drag-handle-row,
.drag-handle-col {
  transition: color 0.2s;
}

.drag-handle-row:hover,
.drag-handle-col:hover {
  color: rgb(var(--v-theme-primary)) !important;
}

.layout-col-wrapper {
  transition: transform 0.2s;
}

.layout-col-wrapper:hover {
  transform: translateY(-2px);
}
</style>
