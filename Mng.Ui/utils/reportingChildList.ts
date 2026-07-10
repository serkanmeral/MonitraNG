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

export function buildReportingChildListFilters(
  parentRow: Record<string, unknown>,
  childList: ReportingExpandChildListConfig
): AfListFilter[] {
  const parentValue = resolveReportingExpandParentValue(
    parentRow,
    childList.parentField ?? '__dataId'
  );
  if (!parentValue) return [];
  return [{ field: childList.linkField, operator: 'eq', value: parentValue }];
}

export async function fetchReportingChildList(options: {
  parentRow: Record<string, unknown>;
  childList: ReportingExpandChildListConfig;
  canViewColumn?: (fieldName: string) => boolean;
}): Promise<{ rows: Record<string, unknown>[]; totalCount: number }> {
  const filters = buildReportingChildListFilters(options.parentRow, options.childList);
  if (!filters.length) {
    return { rows: [], totalCount: 0 };
  }

  const { field: sortField, desc: sortDesc } = parseSortField(options.childList.sort);
  const limit = options.childList.limit ?? 500;

  const result = await fetchReportingPreview({
    datasetName: options.childList.datasetName,
    listConfig: options.childList.listConfig,
    canViewColumn: options.canViewColumn,
    advancedFilters: filters,
    sortField,
    sortDesc,
    skip: 0,
    limit,
    expand: options.childList.expand !== false,
  });

  return { rows: result.rows, totalCount: result.totalCount };
}

export { buildReportingSort };
