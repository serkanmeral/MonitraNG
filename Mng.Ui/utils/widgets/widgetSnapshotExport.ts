import type { Widget } from '@/stores/apps/widget';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import type {
  DashboardSnapshotPayload,
  ExportCapabilities,
  WidgetSnapshotItem,
} from '@/types/apps/widgetSnapshot';
import type { WidgetDataResponse } from '@/services/widgetDataService';
import {
  parseQueryRef,
  resolveManifestBindingForFetch,
  type WidgetLike,
} from '@/utils/widgets/widgetManifestAdapter';

const DEFAULT_EXPORT: ExportCapabilities = {
  supportsPdf: false,
  supportsCsv: false,
  supportsPng: false,
  supportsSnapshot: true,
};

export function parseExportCapabilities(widget: WidgetLike): ExportCapabilities {
  const config = widget.config as Record<string, unknown> | undefined;
  const manifest = (config?.manifest ?? widget.manifest) as { export?: Partial<ExportCapabilities> } | undefined;
  const raw = manifest?.export;
  if (!raw || typeof raw !== 'object') return { ...DEFAULT_EXPORT };
  return {
    supportsPdf: raw.supportsPdf === true,
    supportsCsv: raw.supportsCsv === true,
    supportsPng: raw.supportsPng === true,
    supportsSnapshot: raw.supportsSnapshot !== false,
    snapshotTtlSeconds:
      typeof raw.snapshotTtlSeconds === 'number' ? raw.snapshotTtlSeconds : undefined,
  };
}

export function widgetSupportsCsvExport(widget: WidgetLike): boolean {
  const caps = parseExportCapabilities(widget);
  if (caps.supportsCsv) return true;
  return widget.type === 'table';
}

function manifestFromWidget(widget: WidgetLike): Record<string, unknown> | undefined {
  const config = widget.config as Record<string, unknown> | undefined;
  return (config?.manifest ?? widget.manifest) as Record<string, unknown> | undefined;
}

export function buildWidgetSnapshotItem(
  widgetId: string,
  widget: WidgetLike,
  context: SurfaceContext,
  data: WidgetDataResponse | null | undefined,
): WidgetSnapshotItem {
  const manifest = manifestFromWidget(widget);
  const binding = resolveManifestBindingForFetch(widget, context);
  const config = widget.config as Record<string, unknown> | undefined;
  const capturedAt = new Date().toISOString();

  let dataBinding: WidgetSnapshotItem['dataBinding'] = {
    kind: 'legacy',
    parameters: {},
  };

  if (binding) {
    dataBinding = {
      kind: binding.kind,
      queryRef: binding.queryRef,
      serviceRef: binding.serviceRef,
      parameters: binding.parameters ?? {},
    };
    if (binding.queryRef) {
      const parsed = parseQueryRef(binding.queryRef);
      if (parsed) {
        dataBinding.dataset = parsed.dataset;
        dataBinding.queryName = parsed.queryName;
      }
    }
  } else if (widget.dataSource?.type === 'data' && widget.dataSource.predefined?.queryName) {
    dataBinding = {
      kind: 'queryRef',
      dataset: widget.dataSource.dataset,
      queryName: widget.dataSource.predefined.queryName,
      parameters: (widget.dataSource.predefined.parameters as Record<string, unknown>) ?? {},
    };
  }

  const presentation = manifest?.presentation as Record<string, unknown> | undefined;

  return {
    widgetId,
    name: widget.name,
    templateId: (config?.templateId as string | undefined) ?? (manifest?.templateId as string | undefined),
    templateVersion: manifest?.templateVersion as string | undefined,
    manifestVersion: manifest?.manifestVersion as string | undefined,
    presentation: {
      kind: (presentation?.kind as string | undefined) ?? widget.type,
      preset:
        (presentation?.preset as string | undefined) ??
        (presentation?.defaultPreset as string | undefined),
      type: widget.type,
    },
    dataBinding,
    export: parseExportCapabilities(widget),
    resolvedContext: context,
    data: data?.data ?? data ?? null,
    capturedAt,
  };
}

export function buildDashboardSnapshot(input: {
  dashboardId?: string;
  slug?: string;
  title?: string;
  context: SurfaceContext;
  widgets: WidgetSnapshotItem[];
  surface?: 'dashboard' | 'report';
}): DashboardSnapshotPayload {
  return {
    snapshotVersion: '1.0',
    surface: input.surface ?? 'dashboard',
    dashboard: {
      id: input.dashboardId,
      slug: input.slug,
      title: input.title,
    },
    context: input.context,
    widgets: input.widgets,
    capturedAt: new Date().toISOString(),
  };
}

function flattenRows(data: unknown): Record<string, unknown>[] {
  if (Array.isArray(data)) {
    return data.filter((row) => row && typeof row === 'object') as Record<string, unknown>[];
  }
  if (data && typeof data === 'object') {
    const obj = data as Record<string, unknown>;
    for (const key of ['items', 'rows', 'buckets', 'series', 'data']) {
      const val = obj[key];
      if (Array.isArray(val)) {
        return val.filter((row) => row && typeof row === 'object') as Record<string, unknown>[];
      }
    }
  }
  return [];
}

function escapeCsvCell(value: unknown): string {
  const text = value == null ? '' : String(value);
  if (/[",\n\r]/.test(text)) {
    return `"${text.replace(/"/g, '""')}"`;
  }
  return text;
}

export function widgetDataToCsv(widget: WidgetLike, data: WidgetDataResponse | null | undefined): string | null {
  const rows = flattenRows(data?.data ?? data);
  if (!rows.length) return null;

  const headers = Array.from(
    rows.reduce((set, row) => {
      Object.keys(row).forEach((k) => set.add(k));
      return set;
    }, new Set<string>()),
  );

  const lines = [
    headers.map(escapeCsvCell).join(','),
    ...rows.map((row) => headers.map((h) => escapeCsvCell(row[h])).join(',')),
  ];
  return lines.join('\r\n');
}

export function downloadTextFile(filename: string, content: string, mimeType: string) {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

export function downloadJsonFile(filename: string, payload: unknown) {
  downloadTextFile(filename, JSON.stringify(payload, null, 2), 'application/json;charset=utf-8');
}

export function snapshotFilename(slug: string, extension: string): string {
  const safe = slug.replace(/[^a-zA-Z0-9_-]+/g, '-').replace(/^-+|-+$/g, '') || 'dashboard';
  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  return `${safe}-snapshot-${stamp}.${extension}`;
}

export type { Widget };
