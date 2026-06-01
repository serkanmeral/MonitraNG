<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcDashboardSummaryCard from '@/components/apps/operation-core/dashboards/OcDashboardSummaryCard.vue';
import OcDashboardListWidget from '@/components/apps/operation-core/dashboards/OcDashboardListWidget.vue';
import type {
  OcBoardCatalogs,
  OcDashboardWidget,
  OcPersonDisplay,
} from '@/types/apps/operationCore';

const props = defineProps<{
  widget: OcDashboardWidget;
  catalogs: OcBoardCatalogs;
  people: Record<string, OcPersonDisplay>;
  groups: Record<string, OcPersonDisplay>;
}>();

const { t } = useAppI18n();

const kind = computed(() => (props.widget.widgetType || '').toLowerCase());
</script>

<template>
  <OcDashboardSummaryCard v-if="kind === 'summarycard'" :widget="widget" />

  <OcDashboardListWidget
    v-else-if="kind === 'list'"
    :widget="widget"
    :catalogs="catalogs"
    :people="people"
    :groups="groups"
  />

  <v-card v-else-if="kind === 'chart'" variant="outlined" class="rounded-lg h-100 d-flex align-center justify-center" style="min-height: 220px">
    <div class="text-center pa-4 text-medium-emphasis">
      <v-icon icon="mdi-chart-box-outline" size="40" class="mb-2 opacity-60" />
      <p class="text-body-2 mb-0">{{ t('operationCore.dashboards.chartSoon') }}</p>
    </div>
  </v-card>

  <v-card v-else variant="outlined" class="rounded-lg h-100 d-flex align-center justify-center" style="min-height: 132px">
    <div class="text-center pa-4 text-medium-emphasis">
      <v-icon icon="mdi-help-circle-outline" size="32" class="mb-2 opacity-60" />
      <p class="text-caption mb-0">{{ t('operationCore.dashboards.unknownWidget', { type: widget.widgetType }) }}</p>
    </div>
  </v-card>
</template>
