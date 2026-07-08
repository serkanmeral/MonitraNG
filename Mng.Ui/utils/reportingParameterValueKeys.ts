export type ReportingParameterValues = Record<string, string>;

export const REPORTING_PARAM_RANGE_FROM_SUFFIX = '__from';
export const REPORTING_PARAM_RANGE_TO_SUFFIX = '__to';

/** Parametre değeri — number widget vb. için string'e normalize edilir. */
export function reportingParameterRawValue(
  values: ReportingParameterValues,
  id: string
): string {
  const v = values[id];
  if (v == null || v === '') return '';
  return String(v).trim();
}

export function reportingParamRangeFromKey(paramId: string): string {
  return `${paramId}${REPORTING_PARAM_RANGE_FROM_SUFFIX}`;
}

export function reportingParamRangeToKey(paramId: string): string {
  return `${paramId}${REPORTING_PARAM_RANGE_TO_SUFFIX}`;
}

/** Çeyrek değeri: YYYY-Qn */
export function parseReportingQuarterValue(raw: string): { year: number; quarter: number } | null {
  const m = /^(\d{4})-Q([1-4])$/i.exec(raw.trim());
  if (!m) return null;
  return { year: Number(m[1]), quarter: Number(m[2]) };
}

export function formatReportingQuarterValue(year: number, quarter: number): string {
  return `${year}-Q${quarter}`;
}
