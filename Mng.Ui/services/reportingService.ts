import type { AfListFilter } from '@/utils/afListFilters';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import { fetchFromDataGateway } from '@/services/apiService';
import {
  buildReportingDataGatewayListPath,
  buildReportingDataGatewayQueryString,
} from '@/utils/reportingDataGatewayQuery';
import {
  reportingColumnListKey,
  reportingQueryFieldNamesFromColumns,
  visibleReportingColumnKeys,
} from '@/utils/reportingListConfig';
import { expandLayoutFieldNames } from '@/utils/reportingExpandLayout';
import type { ReportingExpandConfig, ReportingPreviewResult } from '@/types/apps/reporting';

function mergeReportingFieldNames(
  listConfig: OdakHubListConfig,
  expandConfig?: ReportingExpandConfig | null,
  canViewColumn?: (fieldName: string) => boolean
): string[] {
  const visibleKeys = new Set(visibleReportingColumnKeys(listConfig, canViewColumn));
  const columnsForQuery = listConfig.columns.filter(
    (c) => c.visible && visibleKeys.has(reportingColumnListKey(c))
  );
  const fields = new Set(
    reportingQueryFieldNamesFromColumns(
      columnsForQuery.length ? columnsForQuery : listConfig.columns.filter((c) => c.visible)
    )
  );

  if (expandConfig?.enabled) {
    for (const name of expandLayoutFieldNames(expandConfig)) {
      const root = name.includes('.') ? name.split('.')[0]! : name;
      if (!canViewColumn || canViewColumn(name) || canViewColumn(root)) {
        fields.add(root);
      }
    }
  }

  return [...fields];
}

export function buildReportingSort(sortField: string, sortDesc: boolean): string | undefined {
  if (!sortField) return undefined;
  return sortDesc ? `-${sortField}` : sortField;
}

export async function fetchReportingPreview(options: {
  datasetName: string;
  listConfig: OdakHubListConfig;
  expandConfig?: ReportingExpandConfig | null;
  canViewColumn?: (fieldName: string) => boolean;
  advancedFilters?: AfListFilter[];
  /** POST /query match — orDateFields yıl filtresi vb. için. */
  mongoMatch?: Record<string, unknown> | null;
  search?: string;
  sortField?: string;
  sortDesc?: boolean;
  skip?: number;
  limit?: number;
  expand?: boolean;
  /** Include aggregate pipeline in DG response (adds showQuery=true to URL). Default: false. */
  showQuery?: boolean;
}): Promise<ReportingPreviewResult> {
  const datasetName = options.datasetName?.trim();
  if (!datasetName) {
    throw new Error('datasetName is required');
  }

  const queryFields = mergeReportingFieldNames(
    options.listConfig,
    options.expandConfig,
    options.canViewColumn
  );
  if (!queryFields.length) {
    throw new Error('At least one visible column is required');
  }

  const sort = buildReportingSort(options.sortField ?? '', options.sortDesc ?? true);
  const includeDgQuery = options.showQuery === true;
  // Keep date operands as ISO date strings — DG DatetimeMatchFilterExpander coerces them.
  // Do not send Extended JSON $date here (BsonValue.Create leaves them as subdocs → empty results).
  const mongoMatch = options.mongoMatch ?? null;

  if (mongoMatch) {
    const baseQuery = buildReportingDataGatewayQueryString({
      fields: queryFields,
      skip: options.skip,
      limit: options.limit,
      sort,
      search: options.search,
      expand: options.expand,
      showQuery: includeDgQuery,
    });
    const postPath = `/api/v1/data/${encodeURIComponent(datasetName)}/query?${baseQuery}`;
    const requestUrl = postPath;

    if (includeDgQuery) {
      const queryPayload = await fetchFromDataGateway(postPath, 'POST', { match: mongoMatch });
      const dataQuery = buildReportingDataGatewayQueryString({
        fields: queryFields,
        skip: options.skip,
        limit: options.limit,
        sort,
        search: options.search,
        expand: options.expand,
        showQuery: false,
      });
      const dataPath = `/api/v1/data/${encodeURIComponent(datasetName)}/query?${dataQuery}`;
      const dataPayload = await fetchFromDataGateway(dataPath, 'POST', { match: mongoMatch });
      const rows = Array.isArray(dataPayload) ? (dataPayload as Record<string, unknown>[]) : [];
      const totalCount = (dataPayload as { _totalCount?: number })?._totalCount ?? rows.length;
      const legacyQuery =
        queryPayload && typeof queryPayload === 'object' && 'query' in queryPayload
          ? (queryPayload as { query?: unknown }).query
          : queryPayload;
      return {
        rows,
        totalCount,
        dgQuery: legacyQuery,
        requestUrl,
      };
    }

    const payload = await fetchFromDataGateway(postPath, 'POST', { match: mongoMatch });
    const rows = Array.isArray(payload) ? (payload as Record<string, unknown>[]) : [];
    const totalCount = (payload as { _totalCount?: number })?._totalCount ?? rows.length;
    return { rows, totalCount, requestUrl };
  }

  const queryString = buildReportingDataGatewayQueryString({
    fields: queryFields,
    skip: options.skip,
    limit: options.limit,
    sort,
    filters: options.advancedFilters ?? [],
    search: options.search,
    expand: options.expand,
    showQuery: includeDgQuery,
  });

  const url = buildReportingDataGatewayListPath(datasetName, queryString);
  const payload = await fetchFromDataGateway(url, 'GET');

  if (
    payload &&
    typeof payload === 'object' &&
    !Array.isArray(payload) &&
    'query' in payload &&
    'data' in payload
  ) {
    const wrapped = payload as {
      query?: unknown;
      data?: Record<string, unknown>[];
      totalCount?: number;
    };
    const rows = Array.isArray(wrapped.data) ? wrapped.data : [];
    const totalCount =
      typeof wrapped.totalCount === 'number' ? wrapped.totalCount : rows.length;
    return {
      rows,
      totalCount,
      dgQuery: wrapped.query,
      requestUrl: url,
    };
  }

  // Legacy DG: showQuery returned pipeline only — fetch rows in a second request.
  if (
    includeDgQuery &&
    payload &&
    typeof payload === 'object' &&
    !Array.isArray(payload) &&
    'query' in payload
  ) {
    const legacyQuery = (payload as { query?: unknown }).query;
    const dataQueryString = buildReportingDataGatewayQueryString({
      fields: queryFields,
      skip: options.skip,
      limit: options.limit,
      sort,
      filters: options.advancedFilters ?? [],
      search: options.search,
      expand: options.expand,
      showQuery: false,
    });
    const dataUrl = buildReportingDataGatewayListPath(datasetName, dataQueryString);
    const dataPayload = await fetchFromDataGateway(dataUrl, 'GET');
    const rows = Array.isArray(dataPayload) ? (dataPayload as Record<string, unknown>[]) : [];
    const totalCount = (dataPayload as { _totalCount?: number })?._totalCount ?? rows.length;
    return {
      rows,
      totalCount,
      dgQuery: legacyQuery,
      requestUrl: url,
    };
  }

  const rows = Array.isArray(payload) ? (payload as Record<string, unknown>[]) : [];
  const totalCount = (payload as { _totalCount?: number })?._totalCount ?? rows.length;

  return { rows, totalCount, requestUrl: url };
}
