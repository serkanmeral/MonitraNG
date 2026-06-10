import type { Widget } from '@/stores/apps/widget';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import { fetchWidgetData, type WidgetDataResponse } from '@/services/widgetDataService';
import {
  adaptWidgetForRuntime,
  resolveManifestBindingForFetch,
  type WidgetLike,
} from '@/utils/widgets/widgetManifestAdapter';

const DEDUP_TTL_MS = 5000;
const dedupCache = new Map<string, { expires: number; data: WidgetDataResponse }>();

function buildDedupKey(widgetId: string, context: SurfaceContext, bindingKey: string): string {
  return [
    widgetId,
    context.timeRange?.preset,
    context.timeRange?.from,
    context.timeRange?.to,
    JSON.stringify(context.variables ?? {}),
    bindingKey,
  ].join('|');
}

export async function fetchWidgetDataWithDedup(
  widget: Widget,
  context: SurfaceContext = {},
): Promise<WidgetDataResponse> {
  const adapted = adaptWidgetForRuntime(widget as WidgetLike, context);
  const binding = resolveManifestBindingForFetch(adapted, context);
  const bindingKey = binding ? JSON.stringify(binding) : 'legacy';
  const widgetId = widget.__dataId ?? widget.dataId ?? widget.name;
  const key = buildDedupKey(widgetId, context, bindingKey);

  const hit = dedupCache.get(key);
  if (hit && hit.expires > Date.now()) {
    return hit.data;
  }

  const data = await fetchWidgetData(adapted, context);
  dedupCache.set(key, { data, expires: Date.now() + DEDUP_TTL_MS });
  return data;
}

export function clearWidgetDataDedupCache() {
  dedupCache.clear();
}

export function resolveBatchWidgetId(widget: Widget): string {
  const config = (widget.config ?? {}) as Record<string, unknown>;
  return (
    widget.__dataId ??
    widget.dataId ??
    (config.templateId as string | undefined) ??
    widget.name ??
    ''
  );
}

/** Dashboard batch map anahtarı — layout widgetId ile aynı öncelik */
export function resolveWidgetBatchDataId(
  widget: Widget,
  layoutWidgetId?: string | null,
): string {
  if (layoutWidgetId?.trim()) return layoutWidgetId.trim();
  return resolveBatchWidgetId(widget);
}

async function fetchWidgetsClientSide(
  widgets: Widget[],
  context: SurfaceContext,
): Promise<Map<string, WidgetDataResponse>> {
  const results = await Promise.allSettled(
    widgets.map(async (widget) => {
      const id = resolveBatchWidgetId(widget);
      if (!id) throw new Error('Widget id eksik');
      const data = await fetchWidgetDataWithDedup(widget, context);
      return { id, data };
    }),
  );

  const map = new Map<string, WidgetDataResponse>();
  for (const result of results) {
    if (result.status === 'fulfilled') {
      map.set(result.value.id, result.value.data);
    }
  }
  return map;
}

/** Dashboard yüzeyi — BFF batch (tercih) veya client-side paralel fetch. */
export async function fetchDashboardWidgetsBatch(
  widgets: Widget[],
  context: SurfaceContext = {},
  options: { useBff?: boolean } = {},
): Promise<Map<string, WidgetDataResponse>> {
  const useBff = options.useBff !== false;
  let map = new Map<string, WidgetDataResponse>();

  // Nuxt BFF route only exists in dev (`npm run dev`); static nginx deploy has no server handler.
  if (useBff && import.meta.client && import.meta.dev) {
    try {
      const response = await $fetch<{
        dataByWidgetId: Record<string, WidgetDataResponse>;
      }>('/api/widgets/batch', {
        method: 'POST',
        body: { widgets, context },
      });
      for (const [id, data] of Object.entries(response.dataByWidgetId ?? {})) {
        map.set(id, data);
      }
    } catch (e) {
      if (import.meta.env.DEV) {
        console.warn('Widget BFF batch fallback to client fetch:', e);
      }
    }
  }

  const missing = widgets.filter((widget) => {
    const id = resolveBatchWidgetId(widget);
    return id && !map.has(id);
  });

  if (missing.length > 0) {
    const clientMap = await fetchWidgetsClientSide(missing, context);
    for (const [id, data] of clientMap) {
      map.set(id, data);
    }
  } else if (map.size === 0) {
    map = await fetchWidgetsClientSide(widgets, context);
  }

  return map;
}
