import { ref, watch, type Ref } from 'vue';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import type { WidgetDataResponse } from '@/services/widgetDataService';
import { useWidgetStore } from '@/stores/apps/widget';
import { fetchDashboardWidgetsBatch, clearWidgetDataDedupCache } from '@/services/widgetBatchDataService';

export function useDashboardWidgetBatch(
  widgetIds: Ref<string[]>,
  surfaceContext: Ref<SurfaceContext>,
) {
  const widgetStore = useWidgetStore();
  const dataByWidgetId = ref(new Map<string, WidgetDataResponse>());
  const loading = ref(false);
  const error = ref<string | null>(null);
  let runToken = 0;

  async function refresh() {
    const ids = widgetIds.value.filter((id) => id.trim());
    if (!ids.length) {
      dataByWidgetId.value = new Map();
      return;
    }

    const token = ++runToken;
    loading.value = true;
    error.value = null;

    try {
      const widgets = await Promise.all(
        ids.map((id) => widgetStore.fetchWidgetById(id)),
      );
      if (token !== runToken) return;

      const map = await fetchDashboardWidgetsBatch(widgets, surfaceContext.value);
      if (token !== runToken) return;

      dataByWidgetId.value = map;
    } catch (e: unknown) {
      if (token !== runToken) return;
      error.value = e instanceof Error ? e.message : 'Widget verisi toplu yüklenemedi';
      dataByWidgetId.value = new Map();
    } finally {
      if (token === runToken) {
        loading.value = false;
      }
    }
  }

  watch(
    [widgetIds, surfaceContext],
    () => {
      clearWidgetDataDedupCache();
      refresh();
    },
    { deep: true, immediate: true },
  );

  return {
    dataByWidgetId,
    loading,
    error,
    refresh,
  };
}
