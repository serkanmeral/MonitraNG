<script setup lang="ts">
import OcDashboardWidget from '@/components/apps/operation-core/dashboards/OcDashboardWidget.vue';
import type {
  OcBoardCatalogs,
  OcDashboardLayoutRow,
  OcDashboardWidget as OcDashboardWidgetType,
  OcPersonDisplay,
} from '@/types/apps/operationCore';

defineOptions({ name: 'OcDashboardGrid' });

defineProps<{
  rows: OcDashboardLayoutRow[];
  widgetMap: Record<string, OcDashboardWidgetType>;
  workspaceId?: string | null;
  catalogs: OcBoardCatalogs;
  people: Record<string, OcPersonDisplay>;
  groups: Record<string, OcPersonDisplay>;
}>();
</script>

<template>
  <div class="oc-dash-grid">
    <v-row v-for="(row, rIdx) in rows" :key="rIdx" dense class="oc-dash-grid-row">
      <v-col
        v-for="(col, cIdx) in row.cols"
        :key="cIdx"
        :cols="col.span ?? 12"
        :sm="col.spanSm"
        :md="col.spanMd"
        :lg="col.spanLg"
        :xl="col.spanXl"
      >
        <!-- İç içe satırlar (nested layout) -->
        <OcDashboardGrid
          v-if="col.rows && col.rows.length"
          :rows="col.rows"
          :widget-map="widgetMap"
          :workspace-id="workspaceId"
          :catalogs="catalogs"
          :people="people"
          :groups="groups"
        />
        <!-- Widget -->
        <OcDashboardWidget
          v-else-if="col.widgetId && widgetMap[col.widgetId]"
          :widget="widgetMap[col.widgetId]"
          :workspace-id="workspaceId"
          :catalogs="catalogs"
          :people="people"
          :groups="groups"
        />
      </v-col>
    </v-row>
  </div>
</template>
