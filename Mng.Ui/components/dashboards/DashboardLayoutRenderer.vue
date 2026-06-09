<script setup lang="ts">
import type { LayoutRow, LayoutCol, WidgetConfigOverrides } from '@/stores/apps/dashboard';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import WidgetRenderer from '@/components/widgets/WidgetRenderer.vue';
import WidgetWithSettings from './WidgetWithSettings.vue';

const props = defineProps<{
  rows: LayoutRow[];
  canEdit?: boolean;
  surfaceContext?: SurfaceContext;
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  'update:widgetOverrides': [payload: { rowIdx: number; colIdx: number; overrides: WidgetConfigOverrides }];
}>();

const placeholder = (col: LayoutCol) => {
  const id = (col.widgetId ?? '').trim();
  if (id) return id;
  return props.t?.('dashboards.view.emptyWidget') ?? 'Boş alan';
};

function onOverridesChange(payload: { rowIdx: number; colIdx: number; overrides: WidgetConfigOverrides }) {
  emit('update:widgetOverrides', payload);
}
</script>

<template>
  <div>
    <v-row
      v-for="(row, rowIdx) in rows"
      :key="rowIdx"
      :align="row.align ?? 'start'"
      :justify="row.justify"
      :no-gutters="row.noGutters"
      :dense="row.dense"
    >
      <v-col
        v-for="(col, colIdx) in row.cols"
        :key="colIdx"
        :cols="col.span ?? 12"
        :sm="col.spanSm"
        :md="col.spanMd"
        :lg="col.spanLg"
        :xl="col.spanXl"
        :order="col.order"
        :align-self="col.alignSelf"
      >
        <!-- Nested rows -->
        <dashboards-dashboard-layout-renderer
          v-if="col.rows && col.rows.length"
          :rows="col.rows"
          :can-edit="canEdit"
          :surface-context="surfaceContext"
          :t="t"
          @update:widget-overrides="emit('update:widgetOverrides', $event)"
        />
        <!-- Widget with settings (canEdit = dashboard'da widget ayarları değiştirilebilir) -->
        <dashboards-widget-with-settings
          v-else-if="canEdit && col.widgetId && col.widgetId.trim()"
          :widget-id="col.widgetId"
          :widget-overrides="col.widgetOverrides"
          :surface-context="surfaceContext"
          :row-idx="rowIdx"
          :col-idx="colIdx"
          :can-edit="canEdit"
          :t="t"
          @update:overrides="onOverridesChange"
        />
        <!-- Widget renderer (canEdit yoksa veya monitoring değilse) -->
        <widgets-widget-renderer
          v-else-if="col.widgetId && col.widgetId.trim()"
          :widget-id="col.widgetId"
          :config-overrides="col.widgetOverrides"
          :surface-context="surfaceContext"
          :t="t"
        />
        <!-- Empty placeholder -->
        <v-card
          v-else
          variant="outlined"
          class="pa-4"
          min-height="80"
        >
          <div class="text-body-2 text-medium-emphasis">
            {{ placeholder(col) }}
          </div>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>
