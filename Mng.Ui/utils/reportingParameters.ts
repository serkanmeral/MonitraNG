import type { AfListFilter } from '@/utils/afListFilters';
import type { ReportingReportParameter } from '@/types/apps/reporting';
import type { ReportingParameterValues } from '@/utils/reportingParameterValueKeys';
import {
  areReportingParametersReady,
  resolveReportingParameterFilterResolution,
  resolveReportingParameterSearch,
  resolveReportingParametersToFilters,
} from '@/utils/reportingParameterModel';

export function defaultReportingParameterValues(
  parameters: ReportingReportParameter[]
): ReportingParameterValues {
  const out: ReportingParameterValues = {};
  for (const p of parameters) {
    if (p.defaultValue != null && p.defaultValue !== '') {
      out[p.id] = p.defaultValue;
    }
  }
  return out;
}

/** Parametre değerlerini DG filtrelerine çevirir (gelişmiş filtre panelinden ayrı). */
export function reportingParametersToFilters(
  parameters: ReportingReportParameter[],
  values: ReportingParameterValues
): AfListFilter[] {
  return resolveReportingParametersToFilters(parameters, values);
}

export function reportingParameterSearchText(
  parameters: ReportingReportParameter[],
  values: ReportingParameterValues
): string {
  return resolveReportingParameterSearch(parameters, values);
}

export function reportingParametersReady(
  parameters: ReportingReportParameter[],
  values: ReportingParameterValues
): boolean {
  return areReportingParametersReady(parameters, values);
}

/** defaultFilters (yumuşak) + parametre filtreleri + kullanıcı gelişmiş filtreleri. */
export function mergeReportingRuntimeFilters(
  defaultFilters: AfListFilter[],
  parameterFilters: AfListFilter[],
  advancedFilters: AfListFilter[]
): AfListFilter[] {
  return [...defaultFilters, ...parameterFilters, ...advancedFilters];
}

export interface ReportingRuntimeQuery {
  filters: AfListFilter[];
  mongoMatch: Record<string, unknown> | null;
}

/** Parametre + gelişmiş filtreleri birleştirir. */
export function buildReportingRuntimeQuery(
  parameters: ReportingReportParameter[],
  values: ReportingParameterValues,
  defaultFilters: AfListFilter[],
  advancedFilters: AfListFilter[]
): ReportingRuntimeQuery {
  const resolution = resolveReportingParameterFilterResolution(parameters, values);
  const filters = mergeReportingRuntimeFilters(
    defaultFilters,
    resolution.filters,
    advancedFilters
  );
  return { filters, mongoMatch: null };
}
