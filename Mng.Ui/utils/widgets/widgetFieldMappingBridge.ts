import type { ManifestDataBinding, PresentationKind } from '@/types/apps/widgetManifest';
import { bindingWantsScenarioRollup } from '@/utils/alarm/alarmScenarioRollupNormalize';

/** alarm.recent-table — Alarm Merkezi inbox satır stili */
export const ALARM_RECENT_TABLE_COLUMNS = [
  { key: 'severity', title: 'Önem', format: 'severity-chip', width: '100px', align: 'center' as const },
  { key: 'status', title: 'Durum', format: 'status-chip', width: '120px' },
  { key: 'count', title: 'Adet', format: 'number', align: 'right' as const, width: '72px' },
  { key: 'lastSeenAt', title: 'Son görülme', format: 'datetime-relative', width: '160px' },
  { key: 'dedupKey', title: 'Özet', format: 'alarm-summary' },
];

/** siem.recent-events-table — SIEM olay inbox stili (ham id gizli) */
export const SIEM_RECENT_TABLE_COLUMNS = [
  { key: 'timestamp', title: 'Zaman', format: 'datetime-relative', width: '168px' },
  { key: 'eventAction', title: 'Olay', format: 'siem-event-action', width: '220px' },
  { key: 'eventOutcome', title: 'Sonuç', format: 'siem-event-outcome', width: '100px' },
  { key: 'actorUser', title: 'Kullanıcı', width: '140px' },
  { key: 'networkSrcIp', title: 'Kaynak IP', width: '130px' },
  { key: 'sourceHost', title: 'Kaynak', width: '140px' },
];

export const SEVERITY_LABELS: Record<number, string> = {
  0: 'Bilgi',
  1: 'Düşük',
  2: 'Orta',
  3: 'Yüksek',
  4: 'Kritik',
};

export function formatSeverityLabel(value: unknown): string {
  if (value == null || value === '') return '—';
  const n = Number(value);
  if (!Number.isNaN(n) && SEVERITY_LABELS[n] != null) return SEVERITY_LABELS[n];
  return String(value);
}

/** fieldMap → ChartWidget / StatCard config (şablondan runtime'a) */
export function applyFieldMapToPresentationConfig(
  kind: PresentationKind,
  fieldMap: Record<string, string> | undefined,
  config: Record<string, unknown>,
  templateId?: string,
): Record<string, unknown> {
  const out: Record<string, unknown> = { ...config };
  const fm = fieldMap ?? {};

  if (kind === 'stat') {
    if (!out.valueField) out.valueField = 'value';
  }

  if (kind === 'chart') {
    const xAxis = { ...((out.xAxis as Record<string, unknown>) ?? {}) };
    const yAxis = { ...((out.yAxis as Record<string, unknown>) ?? {}) };

    if (!xAxis.field) {
      xAxis.field =
        fm.series ??
        fm.x ??
        fm.category ??
        fm.bucket ??
        fm.timestamp ??
        fm.label;
    }
    if (!yAxis.field) {
      yAxis.field = fm.value ?? fm.y ?? fm.count;
    }

    const xField = String(xAxis.field ?? '');
    if (xField === 'severity' && !xAxis.labelFormat) {
      xAxis.labelFormat = 'severity';
    }
    if ((xField === 'bucket' || xField === 'timestamp' || xField === 'hour') && !xAxis.labelFormat) {
      xAxis.labelFormat = 'datetime';
    }

    out.xAxis = xAxis;
    out.yAxis = yAxis;
  }

  if ((kind === 'table' || kind === 'list') && templateId === 'alarm.recent-table') {
    out.columns = ALARM_RECENT_TABLE_COLUMNS;
    if (!out.pagination) {
      out.pagination = {
        enabled: true,
        itemsPerPage: (out.pageSize as number | undefined) ?? 10,
        itemsPerPageOptions: [10, 20, 50],
      };
    }
    if (!out.search) {
      out.search = { enabled: true, placeholder: 'Ara...' };
    }
    out.itemValue = out.itemValue ?? 'id';
    out.presentationStyle = out.presentationStyle ?? 'inbox';
    out.density = out.density ?? 'comfortable';
    out.hover = out.hover ?? true;
  }

  if ((kind === 'table' || kind === 'list') && templateId === 'siem.recent-events-table') {
    out.columns = SIEM_RECENT_TABLE_COLUMNS;
    if (!out.pagination) {
      out.pagination = {
        enabled: true,
        itemsPerPage: (out.pageSize as number | undefined) ?? 15,
        itemsPerPageOptions: [10, 15, 25, 50],
      };
    }
    if (!out.search) {
      out.search = { enabled: true, placeholder: 'Olay ara...' };
    }
    out.itemValue = out.itemValue ?? 'id';
    out.presentationStyle = out.presentationStyle ?? 'inbox';
    out.density = out.density ?? 'compact';
    out.hover = out.hover ?? true;
  }

  if (templateId === 'siem.scenario-cards') {
    out.composite = true;
    if (out.compact === undefined) out.compact = true;
  }

  return out;
}

export interface FieldMappingValidation {
  ok: boolean;
  warnings: string[];
}

export function validatePresentationFieldMapping(
  kind: PresentationKind,
  config: Record<string, unknown>,
): FieldMappingValidation {
  const warnings: string[] = [];

  if (kind === 'stat') {
    if (!config.valueField) {
      warnings.push('fieldMapping.warnStatValue');
    }
  }

  if (kind === 'chart') {
    const xAxis = config.xAxis as { field?: string } | undefined;
    const yAxis = config.yAxis as { field?: string } | undefined;
    if (!xAxis?.field) warnings.push('fieldMapping.warnChartX');
    if (!yAxis?.field) warnings.push('fieldMapping.warnChartY');
  }

  if (kind === 'table' || kind === 'list') {
    const cols = config.columns;
    if (!Array.isArray(cols) || cols.length === 0) {
      warnings.push('fieldMapping.warnTableColumns');
    }
  }

  return { ok: warnings.length === 0, warnings };
}

export type ManifestResponseShape = 'stat' | 'rows';

export function responseShapeFromKind(kind: PresentationKind): ManifestResponseShape {
  return kind === 'stat' ? 'stat' : 'rows';
}

/** Alarm snapshot — presentation kind'a göre stat vs tablo/grafik satırları */
export function shouldReturnStatShape(binding: ManifestDataBinding): boolean {
  if (bindingWantsScenarioRollup(binding)) return false;
  if (binding.responseShape === 'stat') return true;
  if (binding.responseShape === 'rows') return false;
  if (binding.fieldMap?.rows === 'scenarioRollup' || binding.fieldMap?.rows === 'openAlarms') {
    return false;
  }
  return Boolean(binding.fieldMap?.value && !binding.fieldMap?.rows);
}

/** Designer / runtime override birleştirme */
export function mergePresentationConfigOverrides(
  base: Record<string, unknown>,
  overrides?: Record<string, unknown> | null,
): Record<string, unknown> {
  if (!overrides || typeof overrides !== 'object') return base;
  const merged = { ...base };
  for (const key of ['valueField', 'format', 'decimalPlaces', 'icon', 'color', 'type', 'height', 'pageSize', 'itemValue']) {
    if (overrides[key] !== undefined) merged[key] = overrides[key];
  }
  if (overrides.xAxis && typeof overrides.xAxis === 'object') {
    merged.xAxis = { ...(merged.xAxis as object), ...overrides.xAxis };
  }
  if (overrides.yAxis && typeof overrides.yAxis === 'object') {
    merged.yAxis = { ...(merged.yAxis as object), ...overrides.yAxis };
  }
  if (Array.isArray(overrides.columns)) {
    merged.columns = overrides.columns;
  }
  if (overrides.pagination) merged.pagination = overrides.pagination;
  if (overrides.search) merged.search = overrides.search;
  return merged;
}

export function extractPresentationConfigForDesigner(config: Record<string, unknown>): Record<string, unknown> {
  return {
    valueField: config.valueField,
    format: config.format,
    xAxis: config.xAxis ? { ...(config.xAxis as object) } : undefined,
    yAxis: config.yAxis ? { ...(config.yAxis as object) } : undefined,
    columns: Array.isArray(config.columns) ? [...config.columns] : undefined,
    pageSize: config.pageSize,
  };
}
