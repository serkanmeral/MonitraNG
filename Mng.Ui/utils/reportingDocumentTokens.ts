import type {
  ReportingDocumentBinding,
  ReportingDocumentContextType,
  ReportingReportParameter,
} from '@/types/apps/reporting';
import type { AfListFilter } from '@/utils/afListFilters';
import { buildReportingFiltersSummary } from '@/utils/reportingDocumentBindings';
import { reportingParameterRawValue } from '@/utils/reportingParameterValueKeys';
import { normalizeReportingParameters } from '@/utils/reportingParameterModel';

export interface ReportingDocumentTokenContext {
  reportTitle: string;
  bindingLabel: string;
  filtersSummary?: string;
  rowCount?: number | string;
  rowId?: string;
  userName?: string;
  parameterValues?: Record<string, string>;
  parameters?: ReportingReportParameter[];
  fields?: Record<string, string | number | boolean | null | undefined>;
  now?: Date;
}

const TOKEN_RE = /\{\{([^}]+)\}\}/g;

function pad2(n: number): string {
  return n < 10 ? `0${n}` : String(n);
}

/** Basit tarih formatı: yyyy MM dd HH mm ss (ayırıcı serbest). */
export function formatReportingDocumentNow(date: Date, pattern: string): string {
  const p = pattern.trim() || 'yyyy-MM-dd HH:mm';
  const yyyy = String(date.getFullYear());
  const MM = pad2(date.getMonth() + 1);
  const dd = pad2(date.getDate());
  const HH = pad2(date.getHours());
  const mm = pad2(date.getMinutes());
  const ss = pad2(date.getSeconds());
  return p
    .replace(/yyyy/g, yyyy)
    .replace(/MM/g, MM)
    .replace(/dd/g, dd)
    .replace(/HH/g, HH)
    .replace(/mm/g, mm)
    .replace(/ss/g, ss);
}

function lookupField(ctx: ReportingDocumentTokenContext, key: string): string {
  const k = key.trim();
  if (!k) return '';

  if (k === 'reportTitle') return ctx.reportTitle ?? '';
  if (k === 'bindingLabel' || k === 'label') return ctx.bindingLabel ?? '';
  if (k === 'filtersSummary') return ctx.filtersSummary ?? '';
  if (k === 'rowCount') return ctx.rowCount == null ? '' : String(ctx.rowCount);
  if (k === 'rowId') return ctx.rowId ?? '';
  if (k === 'userName') return ctx.userName ?? '';

  if (k.startsWith('param.')) {
    return reportingParameterRawValue(ctx.parameterValues ?? {}, k.slice(6));
  }

  if (ctx.parameterValues && Object.prototype.hasOwnProperty.call(ctx.parameterValues, k)) {
    return reportingParameterRawValue(ctx.parameterValues, k);
  }

  if (ctx.parameters?.length) {
    const param = normalizeReportingParameters(ctx.parameters).find((p) => p.id === k);
    if (param) return reportingParameterRawValue(ctx.parameterValues ?? {}, param.id);
  }

  if (ctx.fields) {
    const v = ctx.fields[k];
    if (v != null && v !== '') return String(v);
    const hit = Object.entries(ctx.fields).find(
      ([fk]) => fk.toLowerCase() === k.toLowerCase()
    );
    if (hit && hit[1] != null && hit[1] !== '') return String(hit[1]);
  }

  return '';
}

function resolveTokenInner(inner: string, ctx: ReportingDocumentTokenContext): string {
  const s = inner.trim();
  const now = ctx.now ?? new Date();

  if (s === 'now' || s.startsWith('now:')) {
    const fmt = s === 'now' ? 'yyyy-MM-dd HH:mm' : s.slice(4).trim();
    return formatReportingDocumentNow(now, fmt);
  }

  for (const alt of s.split('|').map((x) => x.trim()).filter(Boolean)) {
    if (alt === 'now' || alt.startsWith('now:')) {
      const fmt = alt === 'now' ? 'yyyy-MM-dd HH:mm' : alt.slice(4).trim();
      return formatReportingDocumentNow(now, fmt);
    }
    const v = lookupField(ctx, alt);
    if (v) return v;
  }
  return '';
}

/** Kalıp içindeki {{token}} / {{now:fmt}} / {{a|b}} ifadelerini çözer. */
export function resolveReportingDocumentPattern(
  pattern: string,
  ctx: ReportingDocumentTokenContext
): string {
  if (!pattern?.trim()) return '';
  return pattern
    .replace(TOKEN_RE, (_m, inner: string) => resolveTokenInner(inner, ctx))
    .replace(/\s+/g, ' ')
    .trim();
}

export function defaultDocumentNamePattern(
  contextType: ReportingDocumentContextType
): string {
  switch (contextType) {
    case 'parentRow':
      return '{{reportTitle}} · {{egitimNo|baslik|rowId}} {{now:yyyy-MM-dd HH:mm}}';
    case 'childRow':
      return '{{bindingLabel}} · {{personName|rowId}} · {{egitimNo}} {{now:yyyy-MM-dd HH:mm}}';
    default:
      return '{{reportTitle}} {{now:yyyy-MM-dd HH:mm}}';
  }
}

export function resolveReportingDocumentName(
  binding: ReportingDocumentBinding,
  ctx: ReportingDocumentTokenContext
): string {
  const pattern =
    binding.documentNamePattern?.trim() || defaultDocumentNamePattern(binding.contextType);
  const name = resolveReportingDocumentPattern(pattern, ctx);
  return name || binding.label || 'document';
}

export function resolveReportingGeneratedAt(
  binding: ReportingDocumentBinding,
  ctx: ReportingDocumentTokenContext
): string {
  const pattern = binding.generatedAtPattern?.trim() || 'yyyy-MM-dd HH:mm';
  return formatReportingDocumentNow(ctx.now ?? new Date(), pattern);
}

export function buildReportingDocumentTokenContext(options: {
  reportTitle: string;
  binding: ReportingDocumentBinding;
  parameterValues?: Record<string, string>;
  parameters?: ReportingReportParameter[];
  advancedFilters?: AfListFilter[];
  fields?: Record<string, string | number | boolean | null | undefined>;
  rowCount?: number | string;
  rowId?: string;
  userName?: string;
  now?: Date;
}): ReportingDocumentTokenContext {
  const filtersSummary =
    options.parameters && options.parameterValues
      ? buildReportingFiltersSummary({
          parameters: options.parameters,
          parameterValues: options.parameterValues,
          advancedFilters: options.advancedFilters ?? [],
        })
      : undefined;

  return {
    reportTitle: options.reportTitle,
    bindingLabel: options.binding.label,
    filtersSummary,
    parameterValues: options.parameterValues,
    parameters: options.parameters,
    fields: options.fields,
    rowCount: options.rowCount,
    rowId: options.rowId,
    userName: options.userName,
    now: options.now ?? new Date(),
  };
}
