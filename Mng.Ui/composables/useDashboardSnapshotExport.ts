import { ref } from 'vue';
import { useWidgetStore } from '@/stores/apps/widget';
import type { Dashboard } from '@/stores/apps/dashboard';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import type { DashboardSnapshotPayload } from '@/types/apps/widgetSnapshot';
import type { WidgetDataResponse } from '@/services/widgetDataService';
import {
  buildDashboardSnapshot,
  buildWidgetSnapshotItem,
  downloadJsonFile,
  downloadTextFile,
  parseExportCapabilities,
  snapshotFilename,
  widgetDataToCsv,
  widgetSupportsCsvExport,
} from '@/utils/widgets/widgetSnapshotExport';
import { adaptWidgetForRuntime, type WidgetLike } from '@/utils/widgets/widgetManifestAdapter';

export function useDashboardSnapshotExport() {
  const widgetStore = useWidgetStore();
  const exporting = ref(false);
  const exportError = ref<string | null>(null);

  async function buildSnapshot(input: {
    dashboard: Dashboard;
    widgetIds: string[];
    context: SurfaceContext;
    dataByWidgetId: Map<string, WidgetDataResponse>;
  }): Promise<DashboardSnapshotPayload> {
    const items = [];
    for (const widgetId of input.widgetIds) {
      let widget: WidgetLike;
      try {
        widget = adaptWidgetForRuntime(
          (await widgetStore.fetchWidgetById(widgetId)) as WidgetLike,
          input.context,
        );
      } catch {
        continue;
      }
      const exportCaps = parseExportCapabilities(widget);
      if (!exportCaps.supportsSnapshot) continue;
      items.push(
        buildWidgetSnapshotItem(
          widgetId,
          widget,
          input.context,
          input.dataByWidgetId.get(widgetId),
        ),
      );
    }

    return buildDashboardSnapshot({
      dashboardId: input.dashboard.__dataId ?? input.dashboard.dataId,
      slug: input.dashboard.slug,
      title: input.dashboard.title,
      context: input.context,
      widgets: items,
    });
  }

  async function exportSnapshotJson(input: {
    dashboard: Dashboard;
    widgetIds: string[];
    context: SurfaceContext;
    dataByWidgetId: Map<string, WidgetDataResponse>;
  }) {
    exporting.value = true;
    exportError.value = null;
    try {
      const payload = await buildSnapshot(input);
      if (!payload.widgets.length) {
        exportError.value = 'snapshotEmpty';
        return;
      }
      const slug = input.dashboard.slug ?? input.dashboard.title ?? 'dashboard';
      downloadJsonFile(snapshotFilename(slug, 'json'), payload);
    } catch (e: unknown) {
      exportError.value = e instanceof Error ? e.message : 'exportFailed';
    } finally {
      exporting.value = false;
    }
  }

  async function exportAllCsv(input: {
    dashboard: Dashboard;
    widgetIds: string[];
    context: SurfaceContext;
    dataByWidgetId: Map<string, WidgetDataResponse>;
  }) {
    exporting.value = true;
    exportError.value = null;
    try {
      const sections: string[] = [];
      for (const widgetId of input.widgetIds) {
        let widget: WidgetLike;
        try {
          widget = adaptWidgetForRuntime(
            (await widgetStore.fetchWidgetById(widgetId)) as WidgetLike,
            input.context,
          );
        } catch {
          continue;
        }
        if (!widgetSupportsCsvExport(widget)) continue;
        const csv = widgetDataToCsv(widget, input.dataByWidgetId.get(widgetId));
        if (!csv) continue;
        sections.push(`# ${widget.name ?? widgetId}\r\n${csv}`);
      }
      if (!sections.length) {
        exportError.value = 'csvEmpty';
        return;
      }
      const slug = input.dashboard.slug ?? input.dashboard.title ?? 'dashboard';
      downloadTextFile(snapshotFilename(slug, 'csv'), sections.join('\r\n\r\n'), 'text/csv;charset=utf-8');
    } catch (e: unknown) {
      exportError.value = e instanceof Error ? e.message : 'exportFailed';
    } finally {
      exporting.value = false;
    }
  }

  return {
    exporting,
    exportError,
    buildSnapshot,
    exportSnapshotJson,
    exportAllCsv,
  };
}
