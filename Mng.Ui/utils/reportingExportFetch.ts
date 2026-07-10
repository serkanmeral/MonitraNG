import type { AfListFilter } from '@/utils/afListFilters';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import type { ReportingExpandConfig } from '@/types/apps/reporting';
import { fetchReportingPreview } from '@/services/reportingService';

/** Soft cap — üstünde onay + ilk N satır. */
export const REPORTING_EXPORT_SOFT_CAP = 5000;
export const REPORTING_EXPORT_PAGE_SIZE = 500;

export async function fetchReportingExportRows(options: {
  datasetName: string;
  listConfig: OdakHubListConfig;
  expandConfig?: ReportingExpandConfig | null;
  canViewColumn?: (fieldName: string) => boolean;
  advancedFilters?: AfListFilter[];
  mongoMatch?: Record<string, unknown> | null;
  search?: string;
  sortField?: string;
  sortDesc?: boolean;
  softCap?: number;
  pageSize?: number;
}): Promise<{ rows: Record<string, unknown>[]; totalCount: number; truncated: boolean }> {
  const softCap = options.softCap ?? REPORTING_EXPORT_SOFT_CAP;
  const pageSize = Math.max(1, Math.min(options.pageSize ?? REPORTING_EXPORT_PAGE_SIZE, softCap));

  const first = await fetchReportingPreview({
    datasetName: options.datasetName,
    listConfig: options.listConfig,
    expandConfig: options.expandConfig,
    canViewColumn: options.canViewColumn,
    advancedFilters: options.advancedFilters,
    mongoMatch: options.mongoMatch,
    search: options.search,
    sortField: options.sortField,
    sortDesc: options.sortDesc,
    skip: 0,
    limit: pageSize,
    expand: true,
    showQuery: false,
  });

  const totalCount = first.totalCount;
  const target = Math.min(totalCount, softCap);
  const rows = [...first.rows];

  while (rows.length < target) {
    const skip = rows.length;
    const limit = Math.min(pageSize, target - rows.length);
    const page = await fetchReportingPreview({
      datasetName: options.datasetName,
      listConfig: options.listConfig,
      expandConfig: options.expandConfig,
      canViewColumn: options.canViewColumn,
      advancedFilters: options.advancedFilters,
      mongoMatch: options.mongoMatch,
      search: options.search,
      sortField: options.sortField,
      sortDesc: options.sortDesc,
      skip,
      limit,
      expand: true,
      showQuery: false,
    });
    if (!page.rows.length) break;
    rows.push(...page.rows);
    if (page.rows.length < limit) break;
  }

  return {
    rows: rows.slice(0, softCap),
    totalCount,
    truncated: totalCount > softCap,
  };
}
