import { ref, watch, type Ref } from 'vue';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import type { WidgetDataResponse } from '@/services/widgetDataService';
import { useWidgetStore } from '@/stores/apps/widget';
import { fetchDashboardWidgetsBatch, clearWidgetDataDedupCache } from '@/services/widgetBatchDataService';
import {
  SIEM_CENTER_TEMPLATE_MAP,
  type SiemCenterWidgetKey,
} from '@/utils/widgets/siemCenterWidgets';
import {
  templateRecordToPreviewWidget,
  hasManifestTableColumns,
  type WidgetLike,
} from '@/utils/widgets/widgetManifestAdapter';

export interface SiemCenterTemplateSlot {
  key: SiemCenterWidgetKey;
  templateId: string;
  widget: WidgetLike;
}

export function useSiemCenterTemplateBatch(surfaceContext: Ref<SurfaceContext>) {
  const widgetStore = useWidgetStore();
  const slots = ref<SiemCenterTemplateSlot[]>([]);
  const dataByTemplateId = ref(new Map<string, WidgetDataResponse>());
  const loading = ref(false);
  const error = ref<string | null>(null);
  const missingTemplates = ref<string[]>([]);
  let runToken = 0;

  async function loadTemplates() {
    missingTemplates.value = [];
    // SIEM panel uses explicit templateId map — include inactive P1 rows (isActive = designer catalog only).
    await widgetStore.fetchWidgetTemplates({ activeOnly: false, limit: 200 });

    const next: SiemCenterTemplateSlot[] = [];
    for (const [key, templateId] of Object.entries(SIEM_CENTER_TEMPLATE_MAP) as [
      SiemCenterWidgetKey,
      string,
    ][]) {
      const record = widgetStore.getTemplateById(templateId);
      if (!record) {
        missingTemplates.value.push(templateId);
        continue;
      }
      next.push({
        key,
        templateId,
        widget: templateRecordToPreviewWidget(record),
      });
    }
    slots.value = next;
  }

  function widgetFor(key: SiemCenterWidgetKey): WidgetLike | null {
    return slots.value.find((s) => s.key === key)?.widget ?? null;
  }

  async function refresh() {
    const token = ++runToken;
    if (!slots.value.length) {
      dataByTemplateId.value = new Map();
      return;
    }

    loading.value = true;
    error.value = null;
    clearWidgetDataDedupCache();

    try {
      const widgets = slots.value.map((s) => s.widget as import('@/stores/apps/widget').Widget);
      const map = await fetchDashboardWidgetsBatch(widgets, surfaceContext.value);
      if (token !== runToken) return;
      dataByTemplateId.value = map;
    } catch (e: unknown) {
      if (token !== runToken) return;
      error.value = e instanceof Error ? e.message : 'SIEM widget verisi yuklenemedi';
      dataByTemplateId.value = new Map();
    } finally {
      if (token === runToken) loading.value = false;
    }
  }

  watch(
    surfaceContext,
    () => {
      void refresh();
    },
    { deep: true },
  );

  async function init() {
    await loadTemplates();
    await refresh();
  }

  return {
    slots,
    dataByTemplateId,
    loading,
    error,
    missingTemplates,
    widgetFor,
    init,
    refresh,
  };
}
