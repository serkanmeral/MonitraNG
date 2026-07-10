import type { AfListFilter } from '@/utils/afListFilters';
import { buildReportingMongoMatch } from '@/utils/reportingMongoMatch';
import type {
  ReportingSummaryConfig,
  ReportingSummaryMetric,
  ReportingSummaryPlacement,
  ReportingSummaryValueFormat,
} from '@/types/apps/reporting';
import { fetchFromDataGateway } from '@/services/apiService';

export function emptyReportingSummaryConfig(): ReportingSummaryConfig {
  return { placement: 'none', metrics: [] };
}

export function normalizeReportingSummaryConfig(raw: unknown): ReportingSummaryConfig {
  if (!raw || typeof raw !== 'object') return emptyReportingSummaryConfig();
  const o = raw as Record<string, unknown>;
  const placement = normalizePlacement(o.placement ?? o.Placement);
  const metricsRaw = o.metrics ?? o.Metrics;
  const metrics: ReportingSummaryMetric[] = [];
  if (Array.isArray(metricsRaw)) {
    for (const item of metricsRaw) {
      const m = normalizeMetric(item);
      if (m) metrics.push(m);
    }
  }
  return { placement, metrics };
}

function normalizePlacement(raw: unknown): ReportingSummaryPlacement {
  const v = String(raw ?? 'none').trim().toLowerCase();
  if (v === 'cards' || v === 'footer' || v === 'both' || v === 'none') return v;
  return 'none';
}

function normalizeMetric(raw: unknown): ReportingSummaryMetric | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const id = String(o.id ?? o.Id ?? '').trim();
  const label = String(o.label ?? o.Label ?? '').trim();
  const kindRaw = String(o.kind ?? o.Kind ?? '').trim().toLowerCase();
  const kind = kindRaw === 'sum' ? 'sum' : kindRaw === 'count' ? 'count' : null;
  if (!id || !label || !kind) return null;
  const field = String(o.field ?? o.Field ?? '').trim() || undefined;
  if (kind === 'sum' && !field) return null;
  const formatRaw = String(o.format ?? o.Format ?? '').trim().toLowerCase();
  const format: ReportingSummaryValueFormat | undefined =
    formatRaw === 'integer' || formatRaw === 'number' ? formatRaw : undefined;
  return { id, label, kind, field, format };
}

export function reportingSummaryHasVisibleMetrics(config?: ReportingSummaryConfig | null): boolean {
  if (!config?.metrics?.length) return false;
  return config.placement !== 'none';
}

export function reportingSummaryShowCards(config?: ReportingSummaryConfig | null): boolean {
  if (!reportingSummaryHasVisibleMetrics(config)) return false;
  return config!.placement === 'cards' || config!.placement === 'both';
}

export function reportingSummaryShowFooter(config?: ReportingSummaryConfig | null): boolean {
  if (!reportingSummaryHasVisibleMetrics(config)) return false;
  return config!.placement === 'footer' || config!.placement === 'both';
}

/** ISO-like date strings → Extended JSON Date for raw aggregate $match. */
export function coerceReportingAggregateMatchDates(
  match: Record<string, unknown> | null
): Record<string, unknown> | null {
  if (!match) return null;
  return coerceNode(match) as Record<string, unknown>;
}

function looksLikeDateString(value: string): boolean {
  return /^\d{4}-\d{2}-\d{2}/.test(value.trim());
}

function toExtendedJsonDate(value: string): { $date: string } {
  const trimmed = value.trim();
  if (trimmed.includes('T')) return { $date: trimmed };
  return { $date: `${trimmed}T00:00:00.000Z` };
}

function coerceNode(node: unknown): unknown {
  if (Array.isArray(node)) return node.map(coerceNode);
  if (!node || typeof node !== 'object') return node;
  const o = node as Record<string, unknown>;
  const out: Record<string, unknown> = {};
  for (const [key, val] of Object.entries(o)) {
    if (key.startsWith('$') && (key === '$and' || key === '$or')) {
      out[key] = coerceNode(val);
      continue;
    }
    if (val && typeof val === 'object' && !Array.isArray(val)) {
      const nested = val as Record<string, unknown>;
      const ops = ['$gte', '$lte', '$gt', '$lt', '$eq', '$ne'] as const;
      let touched = false;
      const next: Record<string, unknown> = {};
      for (const [op, opVal] of Object.entries(nested)) {
        if (
          ops.includes(op as (typeof ops)[number]) &&
          typeof opVal === 'string' &&
          looksLikeDateString(opVal)
        ) {
          next[op] = toExtendedJsonDate(opVal);
          touched = true;
        } else {
          next[op] = coerceNode(opVal);
        }
      }
      out[key] = touched ? next : coerceNode(val);
      continue;
    }
    if (typeof val === 'string' && looksLikeDateString(val) && !key.startsWith('$')) {
      // eq shorthand: { field: "2017-01-01" } — rare for dates; leave as string unless operator form
      out[key] = val;
      continue;
    }
    out[key] = coerceNode(val);
  }
  return out;
}

export function buildReportingSummaryPipeline(
  metrics: ReportingSummaryMetric[],
  filters: AfListFilter[]
): Record<string, unknown>[] {
  const active = metrics.filter((m) => m.kind === 'count' || (m.kind === 'sum' && m.field?.trim()));
  const groupFields: Record<string, unknown> = { _id: null };
  for (const m of active) {
    if (m.kind === 'count') {
      groupFields[m.id] = { $sum: 1 };
    } else if (m.kind === 'sum' && m.field) {
      groupFields[m.id] = {
        $sum: {
          $convert: {
            input: `$${m.field}`,
            to: 'double',
            onError: 0,
            onNull: 0,
          },
        },
      };
    }
  }

  const pipeline: Record<string, unknown>[] = [];
  const match = coerceReportingAggregateMatchDates(buildReportingMongoMatch(filters));
  if (match) {
    pipeline.push({ $match: match });
  }
  pipeline.push({ $group: groupFields });
  return pipeline;
}

export function formatReportingSummaryValue(
  value: unknown,
  format?: ReportingSummaryValueFormat
): string {
  const n = typeof value === 'number' ? value : Number(value);
  if (!Number.isFinite(n)) return '—';
  if (format === 'integer') {
    return Math.round(n).toLocaleString('tr-TR');
  }
  if (Number.isInteger(n)) return n.toLocaleString('tr-TR');
  return n.toLocaleString('tr-TR', { maximumFractionDigits: 2 });
}

export type ReportingSummaryValues = Record<string, number>;

export async function fetchReportingSummary(options: {
  datasetName: string;
  metrics: ReportingSummaryMetric[];
  filters: AfListFilter[];
}): Promise<ReportingSummaryValues> {
  const datasetName = options.datasetName?.trim();
  const metrics = options.metrics ?? [];
  if (!datasetName || !metrics.length) return {};

  const pipeline = buildReportingSummaryPipeline(metrics, options.filters ?? []);
  const url = `/api/v1/data/${encodeURIComponent(datasetName)}/aggregate`;
  const raw = await fetchFromDataGateway(url, 'POST', { pipeline });
  const rows = Array.isArray(raw) ? raw : raw != null ? [raw] : [];
  const doc = (rows[0] ?? {}) as Record<string, unknown>;

  const out: ReportingSummaryValues = {};
  for (const m of metrics) {
    const v = doc[m.id];
    const n = typeof v === 'number' ? v : Number(v);
    out[m.id] = Number.isFinite(n) ? n : 0;
  }
  return out;
}
