import { getCookie } from 'h3';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import type { Widget } from '@/stores/apps/widget';
import type { WidgetDataResponse } from '@/services/widgetDataService';
import { fetchDashboardWidgetsBatchOnServer } from '../../utils/widgetBatchServerFetch';

/**
 * Nuxt BFF — dashboard widget batch fetch (Faz 2 / D5).
 * Tek round-trip: manifest binding + gateway veri çekimi sunucuda paralel.
 */
export default defineEventHandler(async (event) => {
  if (!getCookie(event, 'access_token')) {
    throw createError({ statusCode: 401, statusMessage: 'Unauthorized' });
  }

  const body = await readBody<{
    widgets?: Widget[];
    context?: SurfaceContext;
  }>(event);

  if (!body?.widgets?.length) {
    throw createError({ statusCode: 400, statusMessage: 'widgets array required' });
  }

  const dataByWidgetId = await fetchDashboardWidgetsBatchOnServer(
    event,
    body.widgets,
    body.context ?? {},
  );

  return {
    ok: true,
    count: Object.keys(dataByWidgetId).length,
    dataByWidgetId,
  };
});
