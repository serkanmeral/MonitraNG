import type { PresentationKind } from '@/types/apps/widgetManifest';
import type { Widget } from '@/stores/apps/widget';

export interface PresentationPresetDefinition {
  id: string;
  kind: PresentationKind;
  /** Mevcut WidgetRenderer bileşen anahtarı */
  legacyType: Widget['type'];
  component: 'StatCard' | 'ChartWidget' | 'TableWidget' | 'BannerWidget' | 'GaugeWidget' | 'MapWidget';
  defaultConfig: Record<string, unknown>;
}

/** docs/odak/widgets/PRESENTATION_PRESETS.md */
export const PRESENTATION_PRESETS: Record<string, PresentationPresetDefinition> = {
  'stat-simple': {
    id: 'stat-simple',
    kind: 'stat',
    legacyType: 'card',
    component: 'StatCard',
    defaultConfig: { format: 'number', icon: 'mdi-chart-box', color: 'primary' },
  },
  'stat-sparkline': {
    id: 'stat-sparkline',
    kind: 'stat',
    legacyType: 'card',
    component: 'StatCard',
    defaultConfig: {
      format: 'number',
      icon: 'mdi-chart-line',
      color: 'primary',
      sparkline: { type: 'area', height: 40 },
    },
  },
  'chart-line-smooth': {
    id: 'chart-line-smooth',
    kind: 'chart',
    legacyType: 'chart',
    component: 'ChartWidget',
    defaultConfig: {
      type: 'line',
      height: 280,
      chartOptions: { stroke: { curve: 'smooth', width: 2 } },
    },
  },
  'chart-area-gradient': {
    id: 'chart-area-gradient',
    kind: 'chart',
    legacyType: 'chart',
    component: 'ChartWidget',
    defaultConfig: {
      type: 'area',
      height: 280,
      chartOptions: {
        stroke: { curve: 'smooth', width: 2 },
        fill: { type: 'gradient' },
      },
    },
  },
  'chart-bar': {
    id: 'chart-bar',
    kind: 'chart',
    legacyType: 'chart',
    component: 'ChartWidget',
    defaultConfig: { type: 'bar', height: 280 },
  },
  'chart-donut-breakup': {
    id: 'chart-donut-breakup',
    kind: 'chart',
    legacyType: 'chart',
    component: 'ChartWidget',
    defaultConfig: { type: 'donut', height: 280 },
  },
  'chart-combo-bar-line': {
    id: 'chart-combo-bar-line',
    kind: 'chart',
    legacyType: 'chart',
    component: 'ChartWidget',
    defaultConfig: { type: 'line', height: 280, dualAxis: true },
  },
  'chart-pie': {
    id: 'chart-pie',
    kind: 'chart',
    legacyType: 'chart',
    component: 'ChartWidget',
    defaultConfig: { type: 'pie', height: 280 },
  },
  'table-compact': {
    id: 'table-compact',
    kind: 'table',
    legacyType: 'table',
    component: 'TableWidget',
    defaultConfig: { dense: true, pageSize: 10, columns: [] },
  },
  'table-drilldown': {
    id: 'table-drilldown',
    kind: 'table',
    legacyType: 'table',
    component: 'TableWidget',
    defaultConfig: { dense: true, pageSize: 10, drillDown: true, columns: [] },
  },
  'table-inbox': {
    id: 'table-inbox',
    kind: 'table',
    legacyType: 'table',
    component: 'TableWidget',
    defaultConfig: {
      presentationStyle: 'inbox',
      density: 'comfortable',
      hover: true,
      pageSize: 10,
      columns: [],
    },
  },
  'list-activity': {
    id: 'list-activity',
    kind: 'list',
    legacyType: 'table',
    component: 'ListActivityWidget',
    defaultConfig: {
      dense: true,
      pageSize: 10,
      titleField: 'title',
      subtitleField: 'subtitle',
      timeField: 'lastSeenAt',
      columns: [],
    },
  },
  'list-inbox': {
    id: 'list-inbox',
    kind: 'list',
    legacyType: 'table',
    component: 'ListActivityWidget',
    defaultConfig: {
      presentationStyle: 'inbox',
      severityField: 'severity',
      titleField: 'dedupKey',
      useAlarmSummary: true,
      timeField: 'lastSeenAt',
      pageSize: 15,
    },
  },
  'banner-info': {
    id: 'banner-info',
    kind: 'banner',
    legacyType: 'banner',
    component: 'BannerWidget',
    defaultConfig: { type: 'info', variant: 'tonal', showIcon: true },
  },
  'banner-warning': {
    id: 'banner-warning',
    kind: 'banner',
    legacyType: 'banner',
    component: 'BannerWidget',
    defaultConfig: { type: 'warning', variant: 'tonal', showIcon: true },
  },
  'gauge-threshold': {
    id: 'gauge-threshold',
    kind: 'gauge',
    legacyType: 'gauge',
    component: 'GaugeWidget',
    defaultConfig: { min: 0, max: 100 },
  },
  'map-assets': {
    id: 'map-assets',
    kind: 'map',
    legacyType: 'map',
    component: 'MapWidget',
    defaultConfig: {},
  },
};

export function getPresentationPreset(presetId: string | undefined): PresentationPresetDefinition | undefined {
  if (!presetId) return undefined;
  return PRESENTATION_PRESETS[presetId];
}

export function legacyTypeFromKind(kind: PresentationKind): Widget['type'] {
  switch (kind) {
    case 'stat':
      return 'card';
    case 'list':
      return 'table';
    default:
      return kind as Widget['type'];
  }
}

export function resolvePresetConfig(
  presetId: string | undefined,
  manifestConfig?: Record<string, unknown> | null,
  definitionOverrides?: Record<string, unknown> | null
): Record<string, unknown> {
  const preset = getPresentationPreset(presetId);
  const base = preset?.defaultConfig ? { ...preset.defaultConfig } : {};
  if (manifestConfig) Object.assign(base, manifestConfig);
  if (definitionOverrides) Object.assign(base, definitionOverrides);
  return base;
}

export function listPresetsForKind(kind: PresentationKind): PresentationPresetDefinition[] {
  return Object.values(PRESENTATION_PRESETS).filter((p) => p.kind === kind);
}
