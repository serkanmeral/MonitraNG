import { fetchFromDataGateway } from '@/services/apiService';
import { ocListDatasetPage } from '@/services/operationCoreService';
import { afListFiltersToQueryString, type AfListFilter } from '@/utils/afListFilters';
import { ODAK_SIPARIS_CONFIG, type OdakPackageRow } from '@/utils/odakSiparisConfig';
import { personLabelFromRow } from '@/utils/odakSiparisPackagePersonnel';

/** VDataTable slot item may be raw row or `{ raw: row }` depending on table variant. */
export function resolveDataTableRow<T extends Record<string, unknown>>(
  item: T | { raw?: T } | null | undefined
): T {
  if (item != null && typeof item === 'object' && 'raw' in item && item.raw && typeof item.raw === 'object') {
    return item.raw as T;
  }
  return (item ?? {}) as T;
}

export function packageDataId(row: OdakPackageRow | Record<string, unknown>): string {
  const r = resolveDataTableRow(row as Record<string, unknown>);
  return String(r.__dataId ?? r.dataId ?? '');
}

export function packageDisplayNo(row: OdakPackageRow | Record<string, unknown>): string {
  const r = row as OdakPackageRow;
  if (r.packageNo?.trim()) return r.packageNo.trim();
  return packageDataId(row) || '—';
}

export function packageStatusLabel(status: unknown): string {
  if (status === 'closed') return 'Kapalı';
  if (status === 'open') return 'Açık';
  return status != null ? String(status) : '—';
}

export function formatOdakDate(v: unknown): string {
  if (!v) return '—';
  try {
    return new Date(String(v)).toLocaleDateString('tr-TR');
  } catch {
    return String(v);
  }
}

export function formatOdakNumber(v: unknown): string {
  if (v == null || v === '') return '—';
  return String(v);
}

const ODAK_CURRENCY_SYMBOLS: Record<string, string> = {
  TRY: '₺',
  USD: '$',
  EUR: '€',
  GBP: '£',
};

export function odakCurrencySymbol(currency: unknown): string {
  const key = String(currency ?? 'TRY')
    .trim()
    .toUpperCase();
  return ODAK_CURRENCY_SYMBOLS[key] ?? key;
}

/** Birim/toplam maliyet — satır para cinsine göre sembol + TR sayı biçimi. */
export function formatOdakCurrencyAmount(value: unknown, currency?: unknown): string {
  if (value == null || value === '') return '—';
  const num = Number(value);
  if (Number.isNaN(num)) return String(value);
  const formatted = num.toLocaleString('tr-TR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
  return `${odakCurrencySymbol(currency)} ${formatted}`;
}

export function customerIdFromRow(row: OdakPackageRow | Record<string, unknown>): string {
  return resolveRelationId((row as OdakPackageRow).customerId);
}

export function customerFormRoute(customerId: string): string {
  return customerHubRoute(customerId);
}

export function customerHubRoute(customerId?: string): string {
  const base = '/apps/odak-siparis/customers';
  if (!customerId) return base;
  return `${base}?edit=${encodeURIComponent(customerId)}`;
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

/** DG filter — relation field için tek veya çoklu id (parentPackageId vb.). */
export function buildRelationInFilter(relationField: string, ids: string[]): string | undefined {
  const unique = [...new Set(ids.map((id) => id.trim()).filter(Boolean))];
  if (!unique.length) return undefined;
  if (unique.length === 1) return `${relationField}:eq:${unique[0]}`;
  return `${relationField}:in:${JSON.stringify(unique)}`;
}

/** DG filter — tek paketin kalemleri (field:operator:value). */
export function buildLinesByParentPackageFilter(parentPackageId: string): string {
  return `parentPackageId:eq:${parentPackageId}`;
}

/** DG filter — birden fazla paket (line stats chunk). */
export function buildLinesByParentPackagesFilter(parentPackageIds: string[]): string | undefined {
  return buildRelationInFilter('parentPackageId', parentPackageIds);
}

/** Sunucu filtresi yanlis donerse client-side guvenlik agi. */
export function lineBelongsToPackage(row: Record<string, unknown>, packageId: string): boolean {
  if (!packageId) return false;
  return resolveRelationId(row.parentPackageId) === packageId;
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

/** Shipment list/expand — müşteri önce kayıtta, yoksa bağlı iş paketinden. */
export function shipmentCustomerLabel(
  shipment: Record<string, unknown>,
  packageRow: OdakPackageRow | null | undefined,
  customerLabels: Record<string, string>
): string {
  const directId = customerIdFromRow(shipment);
  if (directId) return customerLabelFromRow(shipment, customerLabels);
  if (packageRow) return customerLabelFromRow(packageRow, customerLabels);
  return '—';
}

function parentPackageIdFromRow(row: Record<string, unknown>): string {
  return resolveRelationId((row as OdakPackageRow).parentPackageId);
}

/** Shipment list table — paketId → müşteri etiketi map ile (N+1 paket fetch yok). */
export function shipmentListCustomerLabel(
  shipment: Record<string, unknown>,
  customerLabels: Record<string, string>,
  packageCustomerLabels: Record<string, string>
): string {
  const directId = customerIdFromRow(shipment);
  if (directId) return customerLabelFromRow(shipment, customerLabels);
  const pkgId = parentPackageIdFromRow(shipment);
  if (pkgId && packageCustomerLabels[pkgId]) return packageCustomerLabels[pkgId];
  return '—';
}

export function contactLabelFromRow(
  row: OdakPackageRow | Record<string, unknown>,
  field: 'customerContactId' | 'designContactId' | 'manufactureContactId'
): string {
  const raw = (row as OdakPackageRow)[field];
  if (raw != null && typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    const name = o.ad ?? o.Ad ?? o.name ?? o.unvan;
    if (name != null && String(name).trim()) return String(name).trim();
    const email = o.email ?? o.Email;
    if (email != null && String(email).trim()) return String(email).trim();
  }
  const id = resolveRelationId(raw);
  return id || '—';
}

export interface PackageListCellContext {
  customerLabels: Record<string, string>;
  personLabels: Record<string, string>;
  lineStats: Map<string, OdakPackageLineStats>;
}

/** Liste sütun key → hücre ham metni (format uygulanmadan). */
export function packageListCellRaw(
  item: OdakPackageRow,
  listKey: string,
  ctx: PackageListCellContext
): string {
  const id = packageDataId(item);
  const stats = ctx.lineStats.get(id);
  switch (listKey) {
    case 'displayNo':
      return packageDisplayNo(item);
    case 'name':
      return String(item.name ?? '');
    case 'customer':
      return customerLabelFromRow(item, ctx.customerLabels);
    case 'customerContact':
      return contactLabelFromRow(item, 'customerContactId');
    case 'designContact':
      return personLabelFromRow(item.designContactId, ctx.personLabels);
    case 'manufactureContact':
      return personLabelFromRow(item.manufactureContactId, ctx.personLabels);
    case 'customerPo':
      return stats?.customerPoNos || '—';
    case 'projectNo':
      return stats?.customerProjectNos || '—';
    case 'poVersion':
      return String(item.poVersion ?? '');
    case 'partCount':
      return formatOdakNumber(item.partCount);
    case 'stockCount':
      return formatOdakNumber(item.stockCount);
    case 'shippedCount':
      return formatOdakNumber(item.shippedCount);
    case 'lineCount': {
      if (item.lineCount != null && item.lineCount >= 0) return String(item.lineCount);
      const fromStats = stats?.lineCount;
      return fromStats != null && fromStats > 0 ? String(fromStats) : '—';
    }
    case 'statusLabel':
      return packageStatusLabel(item.status);
    case 'beginDate':
      return formatOdakDate(item.beginDate);
    case 'deliveryDate':
      return formatOdakDate(item.deliveryDate);
    case 'closedAt':
      return formatOdakDate(item.closedAt);
    case 'deliveryAddress':
      return String(item.deliveryAddress ?? '').trim() || '—';
    case 'notes':
      return String(item.notes ?? '').trim() || '—';
    default:
      return '—';
  }
}

const CUSTOMER_LABEL_CACHE_TTL_MS = 600_000;

let customerLabelCache: { map: Record<string, string>; expiresAt: number } | null = null;
let customerLabelInflight: Promise<Record<string, string>> | null = null;
let customerRelationOptionsCache: { items: { value: string; title: string }[]; expiresAt: number } | null = null;
let packageRelationOptionsCache: { items: { value: string; title: string }[]; expiresAt: number } | null = null;
let packageCustomerLabelCache: {
  map: Record<string, string>;
  customerLabelsRef: string;
  expiresAt: number;
} | null = null;
let packageCustomerLabelInflight: Promise<Record<string, string>> | null = null;
const PACKAGE_RELATION_OPTIONS_CACHE_TTL_MS = 5 * 60 * 1000;
const PACKAGE_CUSTOMER_LABEL_CACHE_TTL_MS = 5 * 60 * 1000;

async function loadCustomerLabelMap(customersOnly = false): Promise<Record<string, string>> {
  const map: Record<string, string> = {};
  try {
    const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.customersDataset, {
      limit: 3000,
      sort: 'unvan:asc',
      filter: customersOnly ? 'isMusteri:eq:true' : undefined,
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

/** Oturum cache — liste + detay tekrar tekrar 3000 musteri cekmesin (P3). */
export async function fetchCustomerLabelMap(forceRefresh = false): Promise<Record<string, string>> {
  const now = Date.now();
  if (!forceRefresh && customerLabelCache && customerLabelCache.expiresAt > now) {
    return customerLabelCache.map;
  }
  if (!forceRefresh && customerLabelInflight) {
    return customerLabelInflight;
  }

  customerLabelInflight = (async () => {
    const map = await loadCustomerLabelMap();
    customerLabelCache = { map, expiresAt: Date.now() + CUSTOMER_LABEL_CACHE_TTL_MS };
    return map;
  })();

  try {
    return await customerLabelInflight;
  } finally {
    customerLabelInflight = null;
  }
}

export function invalidateOdakSiparisCustomerCache(): void {
  customerLabelCache = null;
  customerRelationOptionsCache = null;
  packageCustomerLabelCache = null;
}

/** is_paketleri listesinden packageId → müşteri unvan map (sevkiyat listesi sütunu). */
export async function fetchPackageCustomerLabelMap(
  customerLabels: Record<string, string>,
  forceRefresh = false
): Promise<Record<string, string>> {
  const labelsKey = Object.keys(customerLabels).sort().join('|');
  const now = Date.now();
  if (
    !forceRefresh &&
    packageCustomerLabelCache &&
    packageCustomerLabelCache.expiresAt > now &&
    packageCustomerLabelCache.customerLabelsRef === labelsKey
  ) {
    return packageCustomerLabelCache.map;
  }
  if (!forceRefresh && packageCustomerLabelInflight) {
    return packageCustomerLabelInflight;
  }

  packageCustomerLabelInflight = (async () => {
    const map: Record<string, string> = {};
    try {
      const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.packagesDataset, {
        limit: 5000,
      });
      for (const row of resp.items ?? []) {
        const pkgId = packageDataId(row as Record<string, unknown>);
        const cid = customerIdFromRow(row as OdakPackageRow);
        if (!pkgId || !cid) continue;
        map[pkgId] = customerLabels[cid] ?? cid;
      }
    } catch {
      // Liste yine gosterilir
    }
    packageCustomerLabelCache = {
      map,
      customerLabelsRef: labelsKey,
      expiresAt: Date.now() + PACKAGE_CUSTOMER_LABEL_CACHE_TTL_MS,
    };
    return map;
  })();

  try {
    return await packageCustomerLabelInflight;
  } finally {
    packageCustomerLabelInflight = null;
  }
}

export function invalidateOdakSiparisPackageRelationCache(): void {
  packageRelationOptionsCache = null;
}

/** Combobox / autocomplete — unvan A→Z (Türkçe locale). */
export function customerLabelMapToRelationOptions(
  map: Record<string, string>
): { value: string; title: string }[] {
  return Object.entries(map)
    .map(([value, title]) => ({ value, title }))
    .sort((a, b) => a.title.localeCompare(b.title, 'tr', { numeric: true, sensitivity: 'base' }));
}

export async function fetchCustomerRelationOptions(
  forceRefresh = false
): Promise<{ value: string; title: string }[]> {
  const now = Date.now();
  if (!forceRefresh && customerRelationOptionsCache && customerRelationOptionsCache.expiresAt > now) {
    return customerRelationOptionsCache.items;
  }

  const map = await loadCustomerLabelMap(true);
  const items = customerLabelMapToRelationOptions(map);
  customerRelationOptionsCache = { items, expiresAt: now + CUSTOMER_LABEL_CACHE_TTL_MS };
  return items;
}

/** Gelişmiş filtre — parentPackageId relation combobox (paket no + ad). */
export function packageRowsToRelationOptions(
  rows: OdakPackageRow[]
): { value: string; title: string }[] {
  return rows
    .map((row) => {
      const value = packageDataId(row);
      if (!value) return null;
      const no = packageDisplayNo(row);
      const name = row.name?.trim();
      const title = name ? `${no} — ${name}` : no;
      return { value, title };
    })
    .filter((item): item is { value: string; title: string } => item != null)
    .sort((a, b) => a.title.localeCompare(b.title, 'tr', { numeric: true, sensitivity: 'base' }));
}

export async function fetchPackageRelationOptions(
  forceRefresh = false
): Promise<{ value: string; title: string }[]> {
  const now = Date.now();
  if (!forceRefresh && packageRelationOptionsCache && packageRelationOptionsCache.expiresAt > now) {
    return packageRelationOptionsCache.items;
  }
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.packagesDataset, {
    sort: 'packageNo',
    limit: 5000,
  });
  const items = packageRowsToRelationOptions((resp.items ?? []) as OdakPackageRow[]);
  packageRelationOptionsCache = { items, expiresAt: now + PACKAGE_RELATION_OPTIONS_CACHE_TTL_MS };
  return items;
}

/** Müşteriye bağlı iş paketi kimlikleri (sevkiyat listesi müşteri filtresi). */
export async function fetchPackageIdsByCustomerId(customerId: string): Promise<string[]> {
  const id = customerId.trim();
  if (!id) return [];
  // DG relation filter is unreliable — scan packages client-side (see DgMigrationCommon.ps1).
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.packagesDataset, {
    limit: 5000,
  });
  return ((resp.items ?? []) as OdakPackageRow[])
    .filter((row) => customerIdFromRow(row) === id)
    .map((row) => packageDataId(row))
    .filter(Boolean);
}

export interface OdakPackageListSort {
  key: string;
  order: 'asc' | 'desc';
}

/** Tablo sütun anahtarı → DG dataset alanı (odak-is-paketleri-form listConfig). */
export const PACKAGE_LIST_SORT_FIELD_MAP: Record<string, string> = {
  displayNo: 'packageNo',
  name: 'name',
  customer: 'customerId.unvan',
  statusLabel: 'status',
  beginDate: 'beginDate',
  deliveryDate: 'deliveryDate',
  closedAt: 'closedAt',
  lineCount: 'lineCount',
  partCount: 'partCount',
  stockCount: 'stockCount',
  shippedCount: 'shippedCount',
  poVersion: 'poVersion',
};

export function buildPackageListSort(sortBy?: OdakPackageListSort[]): string {
  const primary = sortBy?.[0];
  if (!primary) return '-packageNo';
  const field = PACKAGE_LIST_SORT_FIELD_MAP[primary.key];
  if (!field) return '-packageNo';
  return primary.order === 'desc' ? `-${field}` : field;
}

export interface OdakPackageListQuery {
  statusTab: 'open' | 'closed' | 'all';
  skip?: number;
  limit?: number;
  search?: string;
  sortBy?: OdakPackageListSort[];
  /** @deprecated use advancedFilters */
  packageNo?: string;
  advancedFilters?: AfListFilter[];
}

export function buildPackageListFilter(query: OdakPackageListQuery): string | undefined {
  const parts: string[] = [];
  if (query.statusTab === 'open') parts.push('status:eq:open');
  else if (query.statusTab === 'closed') parts.push('status:eq:closed');

  const advanced = afListFiltersToQueryString(query.advancedFilters ?? []);
  if (advanced) parts.push(advanced);

  // Geriye uyumluluk
  if (query.packageNo?.trim() && !advanced.includes('packageNo:')) {
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
    sort: buildPackageListSort(query.sortBy),
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

/** Kalem bazli liste filtresi (export + liste ortak). */
export interface OdakPackageLineAdvFilter {
  customerPo?: string;
  customerProjectNo?: string;
  customerPoItem?: string;
  productDesc?: string;
}

export function filterPackagesByLineAdv(
  list: OdakPackageRow[],
  lineAdv: OdakPackageLineAdvFilter,
  lineStats: Map<string, OdakPackageLineStats>
): OdakPackageRow[] {
  let result = list;
  const po = lineAdv.customerPo?.trim().toLowerCase() ?? '';
  const proj = lineAdv.customerProjectNo?.trim().toLowerCase() ?? '';
  const poItem = lineAdv.customerPoItem?.trim().toLowerCase() ?? '';
  const desc = lineAdv.productDesc?.trim().toLowerCase() ?? '';

  if (po) {
    result = result.filter((item) => {
      const id = packageDataId(item);
      return (lineStats.get(id)?.customerPoNos ?? '').toLowerCase().includes(po);
    });
  }

  if (proj || poItem || desc) {
    result = result.filter((item) => {
      const stats = lineStats.get(packageDataId(item));
      if (!stats) return false;
      if (proj && !stats.customerProjectNos.toLowerCase().includes(proj)) return false;
      if (poItem && !stats.customerPoNos.toLowerCase().includes(poItem)) return false;
      if (desc && !stats.descriptions.some((d) => d.toLowerCase().includes(desc))) return false;
      return true;
    });
  }

  return result;
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

  const chunkItemLists = await Promise.all(
    chunks.map(async (chunk) => {
      const filter = buildLinesByParentPackagesFilter(chunk);
      if (!filter) return [];
      try {
        const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.linesDataset, {
          filter,
          limit: 2000,
        });
        return (resp.items ?? []).filter((row) => {
          const rec = row as Record<string, unknown>;
          const parentId = resolveRelationId(rec.parentPackageId);
          return parentId && chunk.includes(parentId);
        });
      } catch {
        return [];
      }
    })
  );

  for (const rows of chunkItemLists) {
    for (const row of rows) {
      const rec = row as Record<string, unknown>;
      const parentId = resolveRelationId(rec.parentPackageId);
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
  }

  return result;
}
