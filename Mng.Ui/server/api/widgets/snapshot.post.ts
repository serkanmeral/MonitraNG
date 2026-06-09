import type { DashboardSnapshotPayload } from '@/types/apps/widgetSnapshot';

/**
 * Reporting Servis hook — manifest + çözülmüş parametreler + snapshot verisi.
 * Faz 5: UI client export ile aynı payload sözleşmesi; ileride Reporting Servis tüketir.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody<DashboardSnapshotPayload>(event);

  if (!body || body.snapshotVersion !== '1.0') {
    throw createError({ statusCode: 400, statusMessage: 'snapshotVersion must be 1.0' });
  }
  if (!Array.isArray(body.widgets)) {
    throw createError({ statusCode: 400, statusMessage: 'widgets array required' });
  }

  const exportable = body.widgets.filter((w) => w.export?.supportsSnapshot !== false);

  return {
    ok: true,
    snapshotVersion: body.snapshotVersion,
    surface: body.surface ?? 'dashboard',
    dashboard: body.dashboard,
    context: body.context,
    widgetCount: exportable.length,
    widgets: exportable,
    capturedAt: body.capturedAt ?? new Date().toISOString(),
    /** Reporting Servis PDF/PNG render için manifest + data yeterli */
    reportingReady: exportable.length > 0,
  };
});
