<script setup lang="ts">
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
  const next: DashboardLayout = {
    type: 'rows',
    rows: [...(layout.value.rows ?? []), { cols: [{ span: 12, widgetId: '' }] }],
  };
  emit('update:modelValue', next);
}

function removeRow(rowIndex: number) {
  const rows = [...(layout.value.rows ?? [])];
  rows.splice(rowIndex, 1);
  if (rows.length === 0) rows.push({ cols: [{ span: 12, widgetId: '' }] });
  emit('update:modelValue', { type: 'rows', rows });
}

function addColumn(rowIndex: number) {
  const rows = (layout.value.rows ?? []).map((r, i) =>
    i === rowIndex ? { ...r, cols: [...r.cols, { span: 6, widgetId: '' }] } : { ...r }
  );
  emit('update:modelValue', { type: 'rows', rows });
}

function removeColumn(rowIndex: number, colIndex: number) {
  const rows = (layout.value.rows ?? []).map((r, i) => {
    if (i !== rowIndex) return { ...r };
    const cols = r.cols.filter((_, j) => j !== colIndex);
    if (cols.length === 0) return { ...r, cols: [{ span: 12, widgetId: '' }] };
    return { ...r, cols };
  });
  emit('update:modelValue', { type: 'rows', rows });
}

function updateCol(rowIndex: number, colIndex: number, patch: Partial<LayoutCol>) {
  const rows = (layout.value.rows ?? []).map((r, i) => {
    if (i !== rowIndex) return { ...r };
    const cols = r.cols.map((c, j) => (j === colIndex ? { ...c, ...patch } : { ...c }));
    return { ...r, cols };
  });
  emit('update:modelValue', { type: 'rows', rows });
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

    <div v-for="(row, rowIdx) in layout.rows" :key="rowIdx" class="layout-row mb-4">
      <v-card variant="outlined" class="overflow-hidden">
        <div class="d-flex align-center pa-2 border-b bg-surface-variant">
          <span class="text-body-2 font-weight-medium">
            {{ lbl('row') }} {{ rowIdx + 1 }}
          </span>
          <v-spacer />
          <v-btn
            v-if="(layout.rows?.length ?? 0) > 1"
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
          <v-row dense>
            <v-col
              v-for="(col, colIdx) in row.cols"
              :key="colIdx"
              cols="12"
              md="6"
              lg="4"
            >
              <v-card variant="tonal" class="pa-3">
                <div class="d-flex justify-space-between align-center mb-2">
                  <span class="text-caption font-weight-medium">{{ lbl('col') }} {{ colIdx + 1 }}</span>
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
            </v-col>
          </v-row>
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
