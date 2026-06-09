<script setup lang="ts">
import { computed, provide } from 'vue';
import WidgetHost from '@/components/widgets/WidgetHost.vue';
import type { OcBoardCatalogs, OcDashboardWidget, OcPersonDisplay } from '@/types/apps/operationCore';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import {
  ocDashboardWidgetToLegacyWidget,
  ocExecutionToWidgetData,
} from '@/utils/widgets/ocDashboardWidgetAdapter';
import {
  DASHBOARD_SURFACE_CONTEXT_KEY,
  DASHBOARD_WIDGET_BATCH_MODE_KEY,
  DASHBOARD_WIDGET_DATA_KEY,
} from '@/utils/widgets/dashboardSurfaceKeys';

const props = defineProps<{
  widget: OcDashboardWidget;
  workspaceId?: string | null;
  catalogs?: OcBoardCatalogs;
  people?: Record<string, OcPersonDisplay>;
  t?: (key: string) => string;
}>();

const legacyWidget = computed(() =>
  ocDashboardWidgetToLegacyWidget(props.widget, {
    workspaceId: props.workspaceId ?? undefined,
  }),
);

const batchMap = computed(() => {
  const data = ocExecutionToWidgetData(props.widget, {
    catalogs: props.catalogs,
    people: props.people,
  });
  if (!data) return new Map<string, import('@/services/widgetDataService').WidgetDataResponse>();
  return new Map([[props.widget.key, data]]);
});

const surfaceContext = computed<SurfaceContext>(() => ({
  variables: props.workspaceId ? { workspaceId: props.workspaceId } : {},
}));

provide(DASHBOARD_SURFACE_CONTEXT_KEY, surfaceContext);
provide(DASHBOARD_WIDGET_DATA_KEY, batchMap);
provide(DASHBOARD_WIDGET_BATCH_MODE_KEY, true);
</script>

<template>
  <WidgetHost
    :widget="legacyWidget"
    :surface-context="surfaceContext"
    :t="t"
  />
</template>
