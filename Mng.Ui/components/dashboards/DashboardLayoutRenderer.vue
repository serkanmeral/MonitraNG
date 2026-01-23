<script setup lang="ts">
import type { LayoutRow, LayoutCol } from '@/stores/apps/dashboard';
import WidgetRenderer from '@/components/widgets/WidgetRenderer.vue';

const props = defineProps<{
  rows: LayoutRow[];
  t?: (key: string) => string;
}>();

const placeholder = (col: LayoutCol) => {
  const id = (col.widgetId ?? '').trim();
  if (id) return id;
  return props.t?.('dashboards.view.emptyWidget') ?? 'Boş alan';
};
</script>

<template>
  <div>
    <v-row
      v-for="(row, rowIdx) in rows"
      :key="rowIdx"
      :align="row.align"
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
          :t="t"
        />
        <!-- Widget renderer -->
        <widgets-widget-renderer
          v-else-if="col.widgetId && col.widgetId.trim()"
          :widget-id="col.widgetId"
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
