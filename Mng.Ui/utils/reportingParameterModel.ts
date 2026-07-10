import type { AfListFilter } from '@/utils/afListFilters';
import type {
  ReportingParameterBinding,
  ReportingParameterOptions,
  ReportingParameterWidget,
  ReportingReportParameter,
} from '@/types/apps/reporting';
import type { ReportingYearOrDateRange } from '@/utils/reportingMongoMatch';
import {
  parseReportingQuarterValue,
  reportingParamRangeFromKey,
  reportingParamRangeToKey,
} from '@/utils/reportingParameterValueKeys';
import { reportingParameterRawValue, type ReportingParameterValues } from '@/utils/reportingParameterValueKeys';

export type NormalizedReportingParameter = ReportingReportParameter & {
  widget: ReportingParameterWidget;
  binding: ReportingParameterBinding;
  options?: ReportingParameterOptions;
};

const DEFAULT_YEAR_MIN = 2017;

function choiceFiltersFromStatusOptions(
  param: ReportingReportParameter
): ReportingParameterBinding['choices'] {
  return (param.statusOptions ?? []).map((o) => ({
    value: o.value,
    title: o.title,
    filters: o.filter ? [{ ...o.filter }] : [],
  }));
}

/** Legacy `type` → widget + binding + options (Faz A). */
export function normalizeReportingParameter(
  param: ReportingReportParameter
): NormalizedReportingParameter {
  if (param.widget && param.binding?.kind) {
    return param as NormalizedReportingParameter;
  }

  switch (param.type) {
    case 'statusTab':
      return {
        ...param,
        widget: 'buttonGroup',
        binding: {
          kind: 'choiceFilters',
          choices: choiceFiltersFromStatusOptions(param),
        },
      };
    case 'year':
      return {
        ...param,
        widget: 'number',
        binding: {
          kind: 'datePartRange',
          field: param.dateField ?? '',
          part: 'year',
          emptyMeans: 'noFilter',
        },
        options: param.options ?? {
          kind: 'yearRange',
          min: DEFAULT_YEAR_MIN,
          max: 'currentYear',
          includeAll: true,
        },
      };
    case 'person':
      return {
        ...param,
        widget: 'personPicker',
        binding: {
          kind: 'fieldEq',
          field: param.field ?? '',
        },
      };
    case 'search':
    default:
      return {
        ...param,
        widget: 'search',
        binding: { kind: 'search' },
      };
  }
}

export function normalizeReportingParameters(
  parameters: ReportingReportParameter[]
): NormalizedReportingParameter[] {
  return parameters.map(normalizeReportingParameter);
}

function resolveYearMax(max: number | 'currentYear' | undefined): number {
  if (max === 'currentYear' || max == null) return new Date().getFullYear();
  return max;
}

export function reportingParameterYearBounds(
  param: NormalizedReportingParameter
): { min: number; max: number } {
  const opts = param.options;
  if (opts?.kind === 'yearRange') {
    return {
      min: opts.min ?? DEFAULT_YEAR_MIN,
      max: resolveYearMax(opts.max),
    };
  }
  if (opts?.kind === 'quarterRange') {
    return {
      min: opts.min ?? DEFAULT_YEAR_MIN,
      max: resolveYearMax(opts.max),
    };
  }
  return { min: DEFAULT_YEAR_MIN, max: new Date().getFullYear() };
}

/** Select / yıl listesi — rapor tanımından; domain servisine bağımlı değil. */
export function buildReportingParameterSelectItems(
  param: NormalizedReportingParameter,
  allLabel: string
): { title: string; value: string }[] {
  const opts = param.options;
  if (opts?.kind === 'static') {
    const items = opts.items.map((i) => ({ title: i.title, value: i.value }));
    if (opts.includeAll) return [{ title: allLabel, value: '' }, ...items];
    return items;
  }
  if (
    opts?.kind === 'yearRange' ||
    (param.binding.kind === 'datePartRange' && param.binding.part === 'year' && param.widget === 'select')
  ) {
    const yearOpts =
      opts?.kind === 'yearRange'
        ? opts
        : { min: DEFAULT_YEAR_MIN, max: 'currentYear' as const, includeAll: true };
    const min = yearOpts.min ?? DEFAULT_YEAR_MIN;
    const max = resolveYearMax(yearOpts.max);
    const items: { title: string; value: string }[] = [];
    for (let y = max + 1; y >= min; y--) {
      items.push({ title: String(y), value: String(y) });
    }
    if (yearOpts.includeAll !== false) {
      return [{ title: allLabel, value: '' }, ...items];
    }
    return items;
  }
  return [];
}

function resolveBoundDateField(
  binding: Extract<ReportingParameterBinding, { kind: 'datePartRange' | 'dateRange' }>
): string {
  return binding.field ?? '';
}

function lastDayOfMonth(year: number, month: number): number {
  return new Date(year, month, 0).getDate();
}

function quarterBounds(year: number, quarter: number): { from: string; to: string } {
  const startMonth = (quarter - 1) * 3 + 1;
  const endMonth = startMonth + 2;
  const lastDay = lastDayOfMonth(year, endMonth);
  const sm = String(startMonth).padStart(2, '0');
  const em = String(endMonth).padStart(2, '0');
  return {
    from: `${year}-${sm}-01`,
    to: `${year}-${em}-${String(lastDay).padStart(2, '0')}`,
  };
}

function resolveDatePartBounds(
  binding: Extract<ReportingParameterBinding, { kind: 'datePartRange' }>,
  raw: string
): { from: string; to: string } | null {
  if (!raw && binding.emptyMeans === 'noFilter') return null;

  if (binding.part === 'year') {
    const year = Number(raw);
    if (!Number.isFinite(year) || year < 1900) return null;
    return { from: `${year}-01-01`, to: `${year}-12-31` };
  }

  if (binding.part === 'month') {
    const m = /^(\d{4})-(\d{2})$/.exec(raw.trim());
    if (!m) return null;
    const year = Number(m[1]);
    const month = Number(m[2]);
    if (!Number.isFinite(year) || month < 1 || month > 12) return null;
    const lastDay = lastDayOfMonth(year, month);
    const mm = String(month).padStart(2, '0');
    return {
      from: `${year}-${mm}-01`,
      to: `${year}-${mm}-${String(lastDay).padStart(2, '0')}`,
    };
  }

  if (binding.part === 'quarter') {
    const parsed = parseReportingQuarterValue(raw);
    if (!parsed) return null;
    return quarterBounds(parsed.year, parsed.quarter);
  }

  return null;
}

/** Multi-field year/month/quarter → POST /query $or (no parameter coupling). */
export function datePartRangeToYearOrDateRange(
  binding: Extract<ReportingParameterBinding, { kind: 'datePartRange' }>,
  raw: string
): ReportingYearOrDateRange | null {
  if (!binding.orDateFields?.length) return null;
  const bounds = resolveDatePartBounds(binding, raw);
  if (!bounds) return null;
  const fields = [
    ...new Set(
      [...binding.orDateFields, binding.field]
        .map((f) => (f ?? '').trim())
        .filter(Boolean)
    ),
  ];
  if (!fields.length) return null;
  return { fields, from: bounds.from, to: bounds.to };
}

function datePartRangeFilters(
  binding: Extract<ReportingParameterBinding, { kind: 'datePartRange' }>,
  raw: string
): AfListFilter[] {
  if (binding.orDateFields?.length) return [];

  const bounds = resolveDatePartBounds(binding, raw);
  if (!bounds) return [];

  const field = resolveBoundDateField(binding);
  if (!field) return [];

  return [
    { field, operator: 'gte', value: bounds.from },
    { field, operator: 'lte', value: bounds.to },
  ];
}

function normalizeDateEnd(value: string): string {
  const v = value.trim();
  if (!v) return '';
  if (v.includes('T')) return v;
  return `${v}T23:59:59`;
}

function dateRangeFilters(
  binding: Extract<ReportingParameterBinding, { kind: 'dateRange' }>,
  paramId: string,
  values: Record<string, string>
): AfListFilter[] {
  const from = reportingParameterRawValue(values, reportingParamRangeFromKey(paramId));
  const to = reportingParameterRawValue(values, reportingParamRangeToKey(paramId));
  if (!from && !to && binding.emptyMeans === 'noFilter') return [];

  const field = resolveBoundDateField(binding);
  if (!field) return [];

  const filters: AfListFilter[] = [];
  if (from) filters.push({ field, operator: 'gte', value: from });
  if (to) filters.push({ field, operator: 'lte', value: normalizeDateEnd(to) });
  return filters;
}

function bindingToFilters(
  param: NormalizedReportingParameter,
  raw: string,
  values: Record<string, string>
): AfListFilter[] {
  const binding = param.binding;
  if (!binding?.kind) return [];

  switch (binding.kind) {
    case 'fieldEq':
      if (!raw || !binding.field) return [];
      return [{ field: binding.field, operator: 'eq', value: raw }];
    case 'choiceFilters': {
      const choices = binding.choices ?? choiceFiltersFromStatusOptions(param) ?? [];
      const selected = raw || param.defaultValue || choices[0]?.value || '';
      const choice = choices.find((c) => c.value === selected);
      return choice?.filters?.map((f) => ({ ...f })) ?? [];
    }
    case 'datePartRange':
      return datePartRangeFilters(binding, raw);
    case 'dateRange':
      return dateRangeFilters(binding, param.id, values);
    case 'search':
      return [];
    default:
      return [];
  }
}

export interface ReportingParameterFilterResolution {
  filters: AfListFilter[];
  /** datePartRange + orDateFields — AND ile diğer filtrelerle birleşir. */
  yearOrDateRange: ReportingYearOrDateRange | null;
}

export function resolveReportingParameterFilterResolution(
  parameters: ReportingReportParameter[],
  values: Record<string, string>
): ReportingParameterFilterResolution {
  const filters: AfListFilter[] = [];
  let yearOrDateRange: ReportingYearOrDateRange | null = null;
  for (const param of normalizeReportingParameters(parameters)) {
    if (param.binding.kind === 'dateRange') {
      filters.push(...bindingToFilters(param, '', values));
      continue;
    }
    const raw = reportingParameterRawValue(values, param.id);
    if (param.binding.kind === 'datePartRange' && param.binding.orDateFields?.length) {
      if (!raw && param.binding.emptyMeans === 'noFilter') continue;
      const range = datePartRangeToYearOrDateRange(param.binding, raw);
      if (range) yearOrDateRange = range;
      continue;
    }
    if (!raw && param.binding.kind !== 'choiceFilters') continue;
    filters.push(...bindingToFilters(param, raw, values));
  }
  return { filters, yearOrDateRange };
}

export function resolveReportingParametersToFilters(
  parameters: ReportingReportParameter[],
  values: Record<string, string>
): AfListFilter[] {
  return resolveReportingParameterFilterResolution(parameters, values).filters;
}

export function resolveReportingParameterSearch(
  parameters: ReportingReportParameter[],
  values: Record<string, string>
): string {
  for (const param of normalizeReportingParameters(parameters)) {
    if (param.binding.kind !== 'search') continue;
    return reportingParameterRawValue(values, param.id);
  }
  return '';
}

function isParameterValueFilled(
  param: NormalizedReportingParameter,
  values: Record<string, string>
): boolean {
  if (param.binding.kind === 'dateRange') {
    const from = reportingParameterRawValue(values, reportingParamRangeFromKey(param.id));
    const to = reportingParameterRawValue(values, reportingParamRangeToKey(param.id));
    return Boolean(from || to);
  }
  return Boolean(reportingParameterRawValue(values, param.id));
}

export function areReportingParametersReady(
  parameters: ReportingReportParameter[],
  values: Record<string, string>
): boolean {
  for (const param of parameters) {
    if (!param.required) continue;
    if (!isParameterValueFilled(normalizeReportingParameter(param), values)) return false;
  }
  return true;
}
