import type { ReportingReportParameter } from '@/types/apps/reporting';
import { defaultReportingColumnLinkQueryKey } from '@/utils/reportingColumnLink';
import {
  reportingParameterRawValue,
  type ReportingParameterValues,
} from '@/utils/reportingParameterValueKeys';

export const REPORTING_BROWSE_SHARE_PATH = '/apps/reporting/browse';
export const REPORTING_EMBED_SHARE_PATH = '/apps/reporting/embed';

/** Parametre değerlerini deep-link query’ye çevirir (person → personId). */
export function buildReportingShareQuery(options: {
  reportId: string;
  parameters: ReportingReportParameter[];
  values: ReportingParameterValues;
}): Record<string, string> {
  const reportId = options.reportId.trim();
  const query: Record<string, string> = {};
  if (reportId) query.reportId = reportId;

  for (const param of options.parameters) {
    const raw = reportingParameterRawValue(options.values, param.id);
    if (!raw) continue;
    const key =
      param.type === 'person' || param.id === 'person'
        ? defaultReportingColumnLinkQueryKey('person')
        : param.id;
    query[key] = raw;
  }
  return query;
}

export function buildReportingShareHref(options: {
  reportId: string;
  parameters: ReportingReportParameter[];
  values: ReportingParameterValues;
  basePath?: string;
  origin?: string;
}): string | null {
  const reportId = options.reportId.trim();
  if (!reportId) return null;
  const base = (options.basePath ?? REPORTING_BROWSE_SHARE_PATH).trim() || REPORTING_BROWSE_SHARE_PATH;
  const q = buildReportingShareQuery(options);
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(q)) {
    if (v) params.set(k, v);
  }
  const path = `${base}?${params.toString()}`;
  const origin = (options.origin ?? (typeof window !== 'undefined' ? window.location.origin : '')).replace(
    /\/$/,
    ''
  );
  return origin ? `${origin}${path}` : path;
}
