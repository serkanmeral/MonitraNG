import { fetchFromDataGateway } from '@/services/apiService';
import { ocListDatasetPage } from '@/services/operationCoreService';
import { ODAK_SIPARIS_CONFIG, type OdakPackageRow } from '@/utils/odakSiparisConfig';

export function packageDataId(row: OdakPackageRow | Record<string, unknown>): string {
  const r = row as Record<string, unknown>;
  return String(r.__dataId ?? r.dataId ?? '');
}

export function packageDisplayNo(row: OdakPackageRow | Record<string, unknown>): string {
  const r = row as OdakPackageRow;
  if (r.packageNo?.trim()) return r.packageNo.trim();
  return packageDataId(row) || '—';
}

export function packageStatusLabel(status: unknown): string {
  if (status === 'closed') return 'Kapali';
  if (status === 'open') return 'Acik';
  return status != null ? String(status) : '—';
}

function resolveRelationId(raw: unknown): string {
  if (raw == null) return '';
  if (typeof raw === 'string') return raw;
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    return String(o.__dataId ?? o.dataId ?? o.id ?? '');
  }
  return String(raw);
}

export function customerLabelFromRow(
  row: OdakPackageRow | Record<string, unknown>,
  customerLabels: Record<string, string>
): string {
  const raw = (row as OdakPackageRow).customerId;
  if (raw != null && typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    const unvan = o.unvan ?? o.Unvan;
    if (unvan != null && String(unvan).trim()) return String(unvan);
  }
  const id = resolveRelationId(raw);
  if (id && customerLabels[id]) return customerLabels[id];
  return id || '—';
}

export async function fetchCustomerLabelMap(): Promise<Record<string, string>> {
  const map: Record<string, string> = {};
  try {
    const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.customersDataset, {
      limit: 3000,
      sort: 'unvan:asc',
    });
    for (const row of resp.items ?? []) {
      const o = row as Record<string, unknown>;
      const id = packageDataId(o);
      const unvan = o.unvan != null ? String(o.unvan) : '';
      if (id && unvan) map[id] = unvan;
    }
  } catch {
    // Liste yine gosterilir
  }
  return map;
}

export interface OdakPackageListQuery {
  statusTab: 'open' | 'closed' | 'all';
  skip?: number;
  limit?: number;
  search?: string;
  packageNo?: string;
}

export function buildPackageListFilter(query: OdakPackageListQuery): string | undefined {
  const parts: string[] = [];
  if (query.statusTab === 'open') parts.push('status:eq:open');
  else if (query.statusTab === 'closed') parts.push('status:eq:closed');
  if (query.packageNo?.trim()) {
    parts.push(`packageNo:contains:${query.packageNo.trim()}`);
  }
  return parts.length ? parts.join(',') : undefined;
}

export async function fetchOdakPackagesPage(
  query: OdakPackageListQuery
): Promise<{ items: OdakPackageRow[]; total: number }> {
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.packagesDataset, {
    skip: query.skip ?? 0,
    limit: query.limit ?? 20,
    sort: 'packageNo:desc',
    filter: buildPackageListFilter(query),
    search: query.search?.trim() || undefined,
  });
  return {
    items: (resp.items ?? []) as OdakPackageRow[],
    total: resp.total ?? resp.items?.length ?? 0,
  };
}

export async function fetchOdakPackageById(packageId: string): Promise<OdakPackageRow | null> {
  if (!packageId) return null;
  const url = `/api/v1/data/${encodeURIComponent(ODAK_SIPARIS_CONFIG.packagesDataset)}/${encodeURIComponent(packageId)}?expand=true`;
  const raw = await fetchFromDataGateway(url, 'GET');
  const item = Array.isArray(raw) ? raw[0] : raw;
  if (!item || typeof item !== 'object') return null;
  return item as OdakPackageRow;
}

export interface OdakPackageLineStats {
  lineCount: number;
  customerPoNos: string;
  customerProjectNos: string;
  descriptions: string[];
}

function mergeLabels(existing: string, add: string): string {
  if (!add.trim()) return existing;
  const set = new Set([...existing.split(', ').filter(Boolean), add.trim()]);
  const arr = [...set];
  return arr.slice(0, 3).join(', ') + (arr.length > 3 ? '…' : '');
}

/** Kalemlerden paket basina ozet (liste sutunlari + kalem bazli arama). */
export async function fetchPackageLineStatsMap(
  packageIds: string[]
): Promise<Map<string, OdakPackageLineStats>> {
  const result = new Map<string, OdakPackageLineStats>();
  if (!packageIds.length) return result;

  for (const id of packageIds) {
    result.set(id, { lineCount: 0, customerPoNos: '', customerProjectNos: '', descriptions: [] });
  }

  const chunks: string[][] = [];
  for (let i = 0; i < packageIds.length; i += 8) {
    chunks.push(packageIds.slice(i, i + 8));
  }

  for (const chunk of chunks) {
    const filter = chunk.map((id) => `parentPackageId eq '${id}'`).join(' or ');
    try {
      const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.linesDataset, {
        filter,
        limit: 2000,
      });
      for (const row of resp.items ?? []) {
        const rec = row as Record<string, unknown>;
        const parentRaw = rec.parentPackageId;
        const parentId =
          typeof parentRaw === 'string'
            ? parentRaw
            : parentRaw && typeof parentRaw === 'object'
              ? String(
                  (parentRaw as Record<string, unknown>).__dataId ??
                    (parentRaw as Record<string, unknown>).dataId ??
                    ''
                )
              : '';
        if (!parentId || !result.has(parentId)) continue;

        const stats = result.get(parentId)!;
        stats.lineCount += 1;
        const po = rec.customerPoNo != null ? String(rec.customerPoNo) : '';
        const proj = rec.customerProjectNo != null ? String(rec.customerProjectNo) : '';
        const desc = rec.description != null ? String(rec.description) : '';
        if (po) stats.customerPoNos = mergeLabels(stats.customerPoNos, po);
        if (proj) stats.customerProjectNos = mergeLabels(stats.customerProjectNos, proj);
        if (desc.trim()) stats.descriptions.push(desc.trim());
        result.set(parentId, stats);
      }
    } catch {
      // Liste yine de gosterilir
    }
  }

  return result;
}
