import type { ReportingExpandChildListConfig } from '@/types/apps/reporting';
import { fetchReportingPreview, buildReportingSort } from '@/services/reportingService';
import { resolveReportingExpandParentValue } from '@/utils/reportingExpandLayout';
import type { AfListFilter } from '@/utils/afListFilters';

function parseSortField(sort?: string): { field: string; desc: boolean } {
  const raw = sort?.trim() ?? '';
  if (!raw) return { field: '', desc: false };
  if (raw.startsWith('-')) return { field: raw.slice(1), desc: true };
  return { field: raw, desc: false };
}

function utcCalendarDay(value: string): string | null {
  const match = /^(\d{4}-\d{2}-\d{2})(?:$|T)/.exec(value.trim());
  if (!match) return null;
  const [year, month, day] = match[1].split('-').map(Number);
  if (!year || month < 1 || month > 12 || day < 1 || day > 31) return null;
  const probe = new Date(Date.UTC(year, month - 1, day));
  if (
    probe.getUTCFullYear() !== year ||
    probe.getUTCMonth() !== month - 1 ||
    probe.getUTCDate() !== day
  ) {
    return null;
  }
  return match[1];
}

type ChildLink = {
  linkField: string;
  parentField: string;
  parentValue: string;
  compare: 'eq' | 'withinYear' | 'before';
};

function childLinks(
  parentRow: Record<string, unknown>,
  childList: ReportingExpandChildListConfig
): ChildLink[] | null {
  const links = [
    {
      linkField: childList.linkField,
      parentField: childList.parentField ?? '__dataId',
      compare: 'eq' as const,
    },
    ...(childList.extraLinks ?? []),
  ];
  const resolved: ChildLink[] = [];
  for (const link of links) {
    const linkField = link.linkField?.trim() ?? '';
    const parentField = link.parentField?.trim() ?? '';
    if (!linkField || !parentField) return null;
    const parentValue = resolveReportingExpandParentValue(parentRow, parentField);
    if (!parentValue) return null;
    const compare = link.compare === 'withinYear' || link.compare === 'before' ? link.compare : 'eq';
    resolved.push({ linkField, parentField, parentValue, compare });
  }
  return resolved;
}

function sameUtcDay(raw: unknown, day: string): boolean {
  if (raw == null || raw === '') return false;
  if (raw instanceof Date) return raw.toISOString().startsWith(day);
  return String(raw).trim().startsWith(day);
}

function utcMillis(value: unknown): number | null {
  if (value instanceof Date) {
    return Date.UTC(value.getUTCFullYear(), value.getUTCMonth(), value.getUTCDate());
  }
  const day = utcCalendarDay(String(value ?? ''));
  if (!day) return null;
  const parsed = Date.parse(`${day}T00:00:00.000Z`);
  return Number.isNaN(parsed) ? null : parsed;
}

function plusOneYear(millis: number): number {
  const date = new Date(millis);
  date.setUTCFullYear(date.getUTCFullYear() + 1);
  return date.getTime();
}

/** Period links stay on the client so a stored date and a stored text of the same day both match. */
function rowMatchesDateLinks(row: Record<string, unknown>, links: ChildLink[]): boolean {
  return links.every((link) => {
    if (link.compare === 'withinYear' || link.compare === 'before') {
      const start = utcMillis(row[link.linkField]);
      const period = utcMillis(link.parentValue);
      if (start == null || period == null) return false;
      if (link.compare === 'before') return start < period;
      return start >= period && start < plusOneYear(period);
    }
    const day = utcCalendarDay(link.parentValue);
    if (!day) return String(row[link.linkField] ?? '') === link.parentValue;
    return sameUtcDay(row[link.linkField], day);
  });
}

export function buildReportingChildListFilters(
  parentRow: Record<string, unknown>,
  childList: ReportingExpandChildListConfig
): AfListFilter[] {
  const links = childLinks(parentRow, childList);
  if (!links) return [];
  const filters: AfListFilter[] = [...(childList.filters ?? [])];
  for (const link of links) {
    if (link.compare !== 'eq' || utcCalendarDay(link.parentValue)) continue;
    filters.push({ field: link.linkField, operator: 'eq', value: link.parentValue });
  }
  return filters;
}

export async function fetchReportingChildList(options: {
  parentRow: Record<string, unknown>;
  childList: ReportingExpandChildListConfig;
  canViewColumn?: (fieldName: string) => boolean;
}): Promise<{ rows: Record<string, unknown>[]; totalCount: number }> {
  const links = childLinks(options.parentRow, options.childList);
  if (!links) {
    return { rows: [], totalCount: 0 };
  }
  const dateLinks = links.filter(
    (link) => link.compare !== 'eq' || Boolean(utcCalendarDay(link.parentValue))
  );
  const filters = buildReportingChildListFilters(options.parentRow, options.childList);
  if (!filters.length) {
    return { rows: [], totalCount: 0 };
  }

  const { field: sortField, desc: sortDesc } = parseSortField(options.childList.sort);
  const limit = options.childList.limit ?? 500;

  const queryColumns = [...options.childList.listConfig.columns];
  for (const link of dateLinks) {
    if (queryColumns.some((column) => column.fieldName === link.linkField && column.visible)) continue;
    queryColumns.push({
      fieldName: link.linkField,
      title: link.linkField,
      visible: true,
      order: 1000 + queryColumns.length,
      sortable: false,
      filterable: false,
    });
  }
  const result = await fetchReportingPreview({
    datasetName: options.childList.datasetName,
    listConfig: { ...options.childList.listConfig, columns: queryColumns },
    canViewColumn: options.canViewColumn,
    advancedFilters: filters,
    sortField,
    sortDesc,
    skip: 0,
    limit,
    expand: options.childList.expand !== false,
  });

  if (!dateLinks.length) {
    return { rows: result.rows, totalCount: result.totalCount };
  }
  const rows = result.rows.filter((row) => rowMatchesDateLinks(row, dateLinks));
  return { rows, totalCount: rows.length };
}

export { buildReportingSort };
