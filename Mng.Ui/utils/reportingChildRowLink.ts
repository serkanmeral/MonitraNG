import { fetchFromDataGateway } from '@/services/apiService';
import { diCreateDatasetRowLink } from '@/services/documentIntelligenceService';

export interface ReportingChildLinkRequest {
  resourceId: string;
  datasetName: string;
  linkField: string;
  parentId: string;
  matchField: string;
  matchValue: string;
  refField?: string;
  refValue?: string;
  relationType?: string;
  sortField?: string;
}

function rowsOf(raw: unknown): Record<string, unknown>[] {
  if (Array.isArray(raw)) {
    return raw.filter((row) => row && typeof row === 'object') as Record<string, unknown>[];
  }
  if (!raw || typeof raw !== 'object') return [];
  const record = raw as Record<string, unknown>;
  const items = record.items ?? record.data ?? record.results;
  if (Array.isArray(items)) {
    return items.filter((row) => row && typeof row === 'object') as Record<string, unknown>[];
  }
  if (record.__dataId || record.dataId) return [record];
  return [];
}

function rowId(row: Record<string, unknown>): string {
  const id = row.__dataId ?? row.dataId;
  return id != null ? String(id) : '';
}

function textOf(value: unknown): string {
  if (value == null) return '';
  if (typeof value === 'string') return value.trim();
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  if (typeof value === 'object') {
    const record = value as Record<string, unknown>;
    const id = record.__dataId ?? record.dataId ?? record.id ?? record.value ?? record.key;
    if (typeof id === 'string' && id.trim()) return id.trim();
  }
  return '';
}

async function findMatchingChild(request: ReportingChildLinkRequest): Promise<string | null> {
  const filter = `${request.linkField}:eq:${request.parentId},${request.matchField}:eq:${request.matchValue}`;
  const raw = await fetchFromDataGateway(
    `/api/v1/data/${encodeURIComponent(request.datasetName)}?filter=${encodeURIComponent(filter)}&limit=50&expand=false`
  );
  const sortField = request.sortField?.trim() || 'tarih';
  const ranked = rowsOf(raw).slice().sort((a, b) => String(b[sortField] ?? '').localeCompare(String(a[sortField] ?? '')));
  const refValue = request.refValue?.trim();
  const refField = request.refField?.trim();
  const matched = refValue && refField ? ranked.filter((row) => textOf(row[refField]) === refValue) : ranked;
  const chosen = matched[0] ?? ranked[0];
  const id = chosen ? rowId(chosen) : '';
  return id || null;
}

/** Üretilen dosyayı, alt listedeki eşleşen satıra bağlar. Eşleşme yoksa false. */
export async function linkResourceToMatchingChildRow(request: ReportingChildLinkRequest): Promise<boolean> {
  const resourceId = request.resourceId.trim();
  const datasetName = request.datasetName.trim();
  const parentId = request.parentId.trim();
  if (
    !resourceId ||
    !datasetName ||
    !parentId ||
    !request.linkField.trim() ||
    !request.matchField.trim() ||
    !request.matchValue.trim()
  ) {
    return false;
  }
  let childId = await findMatchingChild(request);
  if (!childId) {
    await new Promise((resolve) => setTimeout(resolve, 700));
    childId = await findMatchingChild(request);
  }
  if (!childId) return false;
  await diCreateDatasetRowLink({
    resourceId,
    datasetName,
    dataId: childId,
    relationType: request.relationType?.trim() || 'output',
  });
  return true;
}
