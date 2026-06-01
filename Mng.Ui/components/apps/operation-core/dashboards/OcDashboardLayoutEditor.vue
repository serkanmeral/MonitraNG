<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcDashboardLayout, OcDashboardLayoutCol, OcDashboardLayoutRow } from '@/types/apps/operationCore';

defineOptions({ name: 'OcDashboardLayoutEditor' });

const props = defineProps<{
  modelValue: OcDashboardLayout;
  /** Bu panoda tanımlı widget key'leri (kolon seçimi bu listeden yapılır). */
  widgetKeys: string[];
  disabled?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: OcDashboardLayout];
}>();

const { t } = useAppI18n();
const lbl = (key: string) => t(`operationCore.dashboards.editor.layout.${key}`);

const rows = computed<OcDashboardLayoutRow[]>(() => props.modelValue.rows ?? []);

const widgetKeyItems = computed(() =>
  props.widgetKeys.map((k) => ({ title: k, value: k }))
);

function emitRows(next: OcDashboardLayoutRow[]) {
  emit('update:modelValue', { type: 'rows', rows: next });
}

function clampSpan(v: number) {
  return Math.max(1, Math.min(12, Number.isNaN(v) ? 12 : Math.round(v)));
}

function rowSpanTotal(row: OcDashboardLayoutRow): number {
  return (row.cols ?? []).reduce((s, c) => s + (c.span ?? 12), 0);
}

function addRow() {
  emitRows([...rows.value, { cols: [{ span: 12 }] }]);
}

function removeRow(rowIdx: number) {
  const next = rows.value.filter((_, i) => i !== rowIdx);
  if (!next.length) next.push({ cols: [{ span: 12 }] });
  emitRows(next);
}

function addColumn(rowIdx: number) {
  const next = rows.value.map((r, i) =>
    i === rowIdx ? { ...r, cols: [...r.cols, { span: 6 } as OcDashboardLayoutCol] } : r
  );
  emitRows(next);
}

function removeColumn(rowIdx: number, colIdx: number) {
  const next = rows.value.map((r, i) => {
    if (i !== rowIdx) return r;
    const cols = r.cols.filter((_, j) => j !== colIdx);
    return { ...r, cols: cols.length ? cols : [{ span: 12 }] };
  });
  emitRows(next);
}

function updateCol(rowIdx: number, colIdx: number, patch: Partial<OcDashboardLayoutCol>) {
  const next = rows.value.map((r, i) => {
    if (i !== rowIdx) return r;
    const cols = r.cols.map((c, j) => (j === colIdx ? { ...c, ...patch } : c));
    return { ...r, cols };
  });
  emitRows(next);
}

function addNested(rowIdx: number, colIdx: number) {
  updateCol(rowIdx, colIdx, { rows: [{ cols: [{ span: 12 }] }], widgetId: undefined });
}

function removeNested(rowIdx: number, colIdx: number) {
  updateCol(rowIdx, colIdx, { rows: undefined });
}

function onNestedUpdate(rowIdx: number, colIdx: number, v: OcDashboardLayout) {
  updateCol(rowIdx, colIdx, { rows: v.rows });
}
</script>

<template>
  <div class="oc-layout-editor">
    <div
      v-for="(row, rowIdx) in rows"
      :key="`row-${rowIdx}`"
      class="mb-3"
    >
      <v-card variant="outlined" class="overflow-hidden">
        <div class="d-flex align-center pa-2 bg-surface-light border-b">
          <span class="text-body-2 font-weight-medium">{{ lbl('row') }} {{ rowIdx + 1 }}</span>
          <v-spacer />
          <v-btn
            v-if="rows.length > 1"
            icon="mdi-delete-outline"
            size="x-small"
            variant="text"
            color="error"
            :disabled="disabled"
            @click="removeRow(rowIdx)"
          />
        </div>
        <v-card-text class="pa-3">
          <div class="d-flex flex-wrap ga-2">
            <div
              v-for="(col, colIdx) in row.cols"
              :key="`col-${rowIdx}-${colIdx}`"
              style="flex: 1 1 260px; min-width: 240px"
            >
              <v-card variant="tonal" class="pa-3">
                <div class="d-flex justify-space-between align-center mb-2">
                  <span class="text-caption font-weight-medium">{{ lbl('col') }} {{ colIdx + 1 }}</span>
                  <v-btn
                    v-if="row.cols.length > 1"
                    icon="mdi-close"
                    size="x-small"
                    variant="text"
                    color="error"
                    :disabled="disabled"
                    @click="removeColumn(rowIdx, colIdx)"
                  />
                </div>

                <div class="d-flex ga-2 mb-2">
                  <v-text-field
                    :model-value="col.span ?? 12"
                    type="number"
                    label="span"
                    variant="outlined"
                    density="compact"
                    hide-details
                    min="1"
                    max="12"
                    style="max-width: 70px"
                    :disabled="disabled"
                    @update:model-value="(v) => updateCol(rowIdx, colIdx, { span: clampSpan(Number(v)) })"
                  />
                  <v-text-field
                    :model-value="col.spanMd ?? ''"
                    type="number"
                    label="md"
                    placeholder="—"
                    variant="outlined"
                    density="compact"
                    hide-details
                    min="1"
                    max="12"
                    style="max-width: 64px"
                    :disabled="disabled"
                    @update:model-value="(v) => updateCol(rowIdx, colIdx, { spanMd: v === '' || v == null ? undefined : clampSpan(Number(v)) })"
                  />
                  <v-text-field
                    :model-value="col.spanLg ?? ''"
                    type="number"
                    label="lg"
                    placeholder="—"
                    variant="outlined"
                    density="compact"
                    hide-details
                    min="1"
                    max="12"
                    style="max-width: 64px"
                    :disabled="disabled"
                    @update:model-value="(v) => updateCol(rowIdx, colIdx, { spanLg: v === '' || v == null ? undefined : clampSpan(Number(v)) })"
                  />
                </div>

                <!-- Nested rows veya widget -->
                <div v-if="col.rows && col.rows.length">
                  <div class="d-flex justify-space-between align-center mb-1">
                    <span class="text-caption text-medium-emphasis">{{ lbl('nestedRows') }}</span>
                    <v-btn
                      size="x-small"
                      variant="text"
                      color="error"
                      :disabled="disabled"
                      @click="removeNested(rowIdx, colIdx)"
                    >
                      <v-icon start size="14">mdi-close</v-icon>
                      {{ lbl('removeNested') }}
                    </v-btn>
                  </div>
                  <OcDashboardLayoutEditor
                    :model-value="{ type: 'rows', rows: col.rows }"
                    :widget-keys="widgetKeys"
                    :disabled="disabled"
                    @update:model-value="(v) => onNestedUpdate(rowIdx, colIdx, v)"
                  />
                </div>
                <div v-else>
                  <v-select
                    :model-value="col.widgetId ?? null"
                    :items="widgetKeyItems"
                    item-title="title"
                    item-value="value"
                    :label="lbl('widget')"
                    :placeholder="lbl('widgetPlaceholder')"
                    variant="outlined"
                    density="compact"
                    hide-details
                    clearable
                    :disabled="disabled"
                    @update:model-value="(v) => updateCol(rowIdx, colIdx, { widgetId: v || undefined })"
                  />
                  <v-btn
                    size="x-small"
                    variant="text"
                    color="secondary"
                    class="mt-2"
                    :disabled="disabled"
                    @click="addNested(rowIdx, colIdx)"
                  >
                    <v-icon start size="14">mdi-view-grid-plus</v-icon>
                    {{ lbl('addNested') }}
                  </v-btn>
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
            {{ lbl('spanWarning') }}
          </v-alert>
        </v-card-text>
      </v-card>
    </div>

    <v-btn variant="outlined" color="primary" block :disabled="disabled" @click="addRow">
      <v-icon start size="20">mdi-plus</v-icon>
      {{ lbl('addRow') }}
    </v-btn>
  </div>
</template>
