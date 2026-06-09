/**
 * Widget snapshot / reporting hook — docs/odak/widgets/MANIFEST_SCHEMA.md §8
 */
import type { SurfaceContext } from '@/types/apps/widgetManifest';

export interface ExportCapabilities {
  supportsPdf: boolean;
  supportsCsv: boolean;
  supportsPng: boolean;
  supportsSnapshot: boolean;
  snapshotTtlSeconds?: number;
}

export interface WidgetSnapshotItem {
  widgetId: string;
  name?: string;
  templateId?: string;
  templateVersion?: string;
  manifestVersion?: string;
  presentation: {
    kind: string;
    preset?: string;
    type?: string;
  };
  dataBinding: {
    kind: 'queryRef' | 'serviceRef' | 'static' | 'legacy';
    queryRef?: string;
    serviceRef?: string;
    dataset?: string;
    queryName?: string;
    parameters: Record<string, unknown>;
  };
  export?: ExportCapabilities;
  resolvedContext: SurfaceContext;
  data: unknown;
  capturedAt: string;
}

export interface DashboardSnapshotPayload {
  snapshotVersion: '1.0';
  surface: 'dashboard' | 'report';
  dashboard: {
    id?: string;
    slug?: string;
    title?: string;
  };
  context: SurfaceContext;
  widgets: WidgetSnapshotItem[];
  capturedAt: string;
}
