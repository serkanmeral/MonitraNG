import { afListFiltersToQueryString, type AfListFilter } from '@/utils/afListFilters';

/** DG GET /api/v1/data/{dataset} — tek sözleşme (server-side list). */
export interface ReportingDataGatewayQueryOptions {
  datasetName: string;
  fields: string[];
  skip?: number;
  limit?: number;
  sort?: string;
  expand?: boolean;
  search?: string;
  /** Parametre + gelişmiş filtre — AfListFilter[] olarak birleştirilmiş. */
  filters?: AfListFilter[];
  showQuery?: boolean;
}

export function buildReportingDataGatewayQueryString(
  options: Omit<ReportingDataGatewayQueryOptions, 'datasetName'>
): string {
  const q = new URLSearchParams();
  q.set('skip', String(options.skip ?? 0));
  q.set('limit', String(options.limit ?? 50));

  if (options.sort?.trim()) {
    q.set('sort', options.sort.trim());
  }

  const filter = afListFiltersToQueryString(options.filters ?? []);
  if (filter) {
    q.set('filter', filter);
  }

  const search = options.search?.trim();
  if (search) {
    q.set('search', search);
  }

  if (options.fields.length) {
    q.set('fields', options.fields.join(','));
  }

  if (options.expand) {
    q.set('expand', 'true');
  }

  if (options.showQuery) {
    q.set('showQuery', 'true');
  }

  return q.toString();
}

export function buildReportingDataGatewayListPath(
  datasetName: string,
  queryString: string
): string {
  const name = datasetName.trim();
  if (!name) {
    throw new Error('datasetName is required');
  }
  return `/api/v1/data/${encodeURIComponent(name)}?${queryString}`;
}
