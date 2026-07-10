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

/** Single AfListFilter → MongoDB match fragment (FilterParser parity for text ops). */
export function afListFilterToMongoCondition(filter: AfListFilter): Record<string, unknown> {
  const { field, operator, value } = filter;
  const op = (operator ?? '').trim().toLowerCase();

  switch (op) {
    case 'eq':
      return { [field]: value };
    case 'ne':
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
    case 'contains':
      return {
        [field]: { $regex: escapeReportingSearchRegex(String(value)), $options: 'i' },
      };
    case 'startswith':
      return {
        [field]: {
          $regex: `^${escapeReportingSearchRegex(String(value))}`,
          $options: 'i',
        },
      };
    case 'endswith':
      return {
        [field]: {
          $regex: `${escapeReportingSearchRegex(String(value))}$`,
          $options: 'i',
        },
      };
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

/** Escape user search for Mongo $regex (DG AddSearch parity on main text fields). */
export function escapeReportingSearchRegex(term: string): string {
  return term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * Pre-expansion text search $match — same idea as DG AddSearch on schema text fields.
 * Relation/person search is not included (requires DG pre-lookup).
 */
export function buildReportingSearchMongoMatch(
  search: string | null | undefined,
  textFieldNames: string[] | null | undefined
): Record<string, unknown> | null {
  const term = (search ?? '').trim();
  const fields = (textFieldNames ?? []).map((f) => f.trim()).filter(Boolean);
  if (!term || !fields.length) return null;
  const pattern = escapeReportingSearchRegex(term);
  return {
    $or: fields.map((field) => ({
      [field]: { $regex: pattern, $options: 'i' },
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
