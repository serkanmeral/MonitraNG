import type { AfListFilter } from '@/utils/afListFilters';

export interface ReportingYearOrDateRange {
  fields: string[];
  from: string;
  to: string;
}

function parseInValues(value: string): string[] {
  const trimmed = value.trim();
  if (!trimmed) return [];
  if (trimmed.startsWith('[')) {
    try {
      const parsed = JSON.parse(trimmed) as unknown;
      if (Array.isArray(parsed)) {
        return parsed.map((v) => String(v).trim()).filter(Boolean);
      }
    } catch {
      /* fall through */
    }
  }
  return trimmed.split(',').map((v) => v.trim()).filter(Boolean);
}

/** Single AfListFilter → MongoDB match fragment. */
export function afListFilterToMongoCondition(filter: AfListFilter): Record<string, unknown> {
  const { field, operator, value } = filter;
  switch (operator) {
    case 'eq':
      return { [field]: value };
    case 'neq':
      return { [field]: { $ne: value } };
    case 'gte':
      return { [field]: { $gte: value } };
    case 'lte':
      return { [field]: { $lte: value } };
    case 'gt':
      return { [field]: { $gt: value } };
    case 'lt':
      return { [field]: { $lt: value } };
    case 'in':
      return { [field]: { $in: parseInValues(value) } };
    case 'nin':
      return { [field]: { $nin: parseInValues(value) } };
    default:
      return { [field]: { [`$${operator}`]: value } };
  }
}

export function yearOrDateRangeMongoMatch(range: ReportingYearOrDateRange): Record<string, unknown> {
  return {
    $or: range.fields.map((field) => ({
      [field]: { $gte: range.from, $lte: range.to },
    })),
  };
}

/** AfListFilter[] + optional year OR → MongoDB $match (POST /query body). */
export function buildReportingMongoMatch(
  filters: AfListFilter[],
  yearOrDateRange?: ReportingYearOrDateRange | null
): Record<string, unknown> | null {
  const active = filters.filter((f) => f.field && f.operator && f.value !== undefined && f.value !== '');
  const conditions: Record<string, unknown>[] = active.map(afListFilterToMongoCondition);

  if (yearOrDateRange?.fields.length) {
    conditions.push(yearOrDateRangeMongoMatch(yearOrDateRange));
  }

  if (!conditions.length) return null;
  if (conditions.length === 1) return conditions[0]!;
  return { $and: conditions };
}
