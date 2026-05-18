import { fetchFromDataGateway } from '@/services/apiService';

export const TM_DATASETS = {
  projects: 'tm_projects',
  boards: 'tm_boards',
  issues: 'tm_issues',
  issueComments: 'tm_issue_comments',
  issueTypes: 'tm_issue_types',
  statuses: 'tm_statuses',
  priorities: 'tm_priorities',
  labels: 'tm_labels',
  sprints: 'tm_sprints',
  fieldDefinitions: 'tm_field_definitions',
} as const;

function parseListResponse(response: unknown): unknown[] {
  if (Array.isArray(response)) return response;
  if (response && typeof response === 'object' && 'items' in response && Array.isArray((response as any).items))
    return (response as any).items;
  if (response && typeof response === 'object' && 'data' in response && Array.isArray((response as any).data))
    return (response as any).data;
  return [];
}

function buildQuery(params: {
  skip?: number;
  limit?: number;
  sort?: string;
  filter?: string;
  search?: string;
  /** DG: kayıtta `__history` alanını dahil et (varsayılan false) */
  showHistory?: boolean;
}): string {
  const q = new URLSearchParams();
  q.set('skip', String(params.skip ?? 0));
  q.set('limit', String(params.limit ?? 500));
  if (params.sort) q.set('sort', params.sort);
  if (params.filter) q.set('filter', params.filter);
  if (params.search) q.set('search', params.search);
  if (params.showHistory) q.set('showHistory', 'true');
  return q.toString();
}

export async function tmListDataset(
  dataset: string,
  options?: { skip?: number; limit?: number; sort?: string; filter?: string; search?: string; showHistory?: boolean }
) {
  const qs = buildQuery(options ?? {});
  const url = `/api/v1/data/${encodeURIComponent(dataset)}?${qs}`;
  const raw = await fetchFromDataGateway(url, 'GET');
  return parseListResponse(raw);
}

export async function tmGetById(dataset: string, dataId: string, options?: { showHistory?: boolean }) {
  const q = new URLSearchParams();
  if (options?.showHistory) q.set('showHistory', 'true');
  const qs = q.toString();
  const url = `/api/v1/data/${encodeURIComponent(dataset)}/${encodeURIComponent(dataId)}${qs ? `?${qs}` : ''}`;
  return fetchFromDataGateway(url, 'GET');
}

export async function tmCreate(dataset: string, body: Record<string, unknown>) {
  const url = `/api/v1/data/${encodeURIComponent(dataset)}`;
  return fetchFromDataGateway(url, 'POST', body);
}

export async function tmUpdate(dataset: string, dataId: string, body: Record<string, unknown>) {
  const url = `/api/v1/data/${encodeURIComponent(dataset)}/${encodeURIComponent(dataId)}`;
  return fetchFromDataGateway(url, 'PUT', body);
}

export async function tmDelete(dataset: string, dataId: string) {
  const url = `/api/v1/data/${encodeURIComponent(dataset)}/${encodeURIComponent(dataId)}`;
  return fetchFromDataGateway(url, 'DELETE');
}
