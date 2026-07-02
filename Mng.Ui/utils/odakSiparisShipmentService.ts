import { fetchFromDataGateway } from '@/services/apiService';
import { ocCreate, ocDelete, ocListDatasetPage, ocUpdate } from '@/services/operationCoreService';
import { afListFiltersToQueryString, type AfListFilter } from '@/utils/afListFilters';
import {
  ODAK_QCF_STATUS_OPTIONS,
  ODAK_RECORD_SCOPE_OPTIONS,
  ODAK_SHIPMENT_STATUS_OPTIONS,
  ODAK_SIPARIS_CONFIG,
  type OdakLineRow,
  type OdakRecordScope,
  type OdakShipmentLineRow,
  type OdakShipmentRow,
} from '@/utils/odakSiparisConfig';
import { fromDateInputValue, toDateInputValue } from '@/utils/odakSiparisDateUtils';
import {
  filterShipmentPayloadByFieldAccess,
  shipmentRecordForPolicyEval,
} from '@/utils/odakSiparisFieldPolicies';
import { loadOdakShipmentFieldPoliciesOnly } from '@/utils/odakSiparisHubSettingsService';
import { useAuthStore } from '@/stores/auth';
import { lineDataId, listLinesForPackage } from '@/utils/odakSiparisLineService';
import { dispatchOdakPackageNotification } from '@/utils/odakSiparisNotificationDispatch';
import { formatOdakDate, fetchCustomerLabelMap, fetchOdakPackageById, fetchPackageIdsByCustomerId, packageDataId } from '@/utils/odakSiparisService';

export type OdakShipmentDialogMode = 'view' | 'create' | 'edit';

function resolveRelationId(raw: unknown): string {
  if (raw == null) return '';
  if (typeof raw === 'string') return raw.trim();
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    return String(o.__dataId ?? o.dataId ?? o.id ?? '').trim();
  }
  return String(raw).trim();
}

export function shipmentDataId(row: OdakShipmentRow | Record<string, unknown>): string {
  return packageDataId(row);
}

/** Native UI kayitlari — legacyShipmentId unique index (max 32) icin benzersiz id. */
export function newNativeLegacyShipmentId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID().replace(/-/g, '');
  }
  return `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`.slice(0, 32);
}

export function shipmentLineDataId(row: OdakShipmentLineRow | Record<string, unknown>): string {
  return packageDataId(row);
}

export function buildShipmentsByParentPackageFilter(parentPackageId: string): string {
  return `parentPackageId:eq:${parentPackageId}`;
}

export function buildShipmentsByRecordScopeFilter(scope: OdakRecordScope): string {
  return `recordScope:eq:${scope}`;
}

export type OdakShipmentScopeTab = 'all' | 'package' | 'general';
export type OdakShipmentStatusTab = 'planned' | 'completed' | 'all';

export interface OdakShipmentListQuery {
  scopeTab?: OdakShipmentScopeTab;
  statusTab?: OdakShipmentStatusTab;
  search?: string;
  page?: number;
  limit?: number;
  sort?: string;
  advancedFilters?: AfListFilter[];
}

/** Default filters for the global shipments list (Genel scope + planned/completed). */
export const ODAK_GLOBAL_SHIPMENTS_DEFAULT_FILTERS: AfListFilter[] = [
  { field: 'recordScope', operator: 'in', value: 'Genel' },
  { field: 'status', operator: 'in', value: 'Planlandi,Tamamlandi' },
];

export function deriveScopeTabFromAdvancedFilters(filters: AfListFilter[]): OdakShipmentScopeTab {
  const scopeFilter = filters.find((f) => f.field === 'recordScope' && f.value?.trim());
  if (!scopeFilter) return 'all';

  const values = scopeFilter.value
    .split(',')
    .map((v) => v.trim())
    .filter(Boolean);

  if (scopeFilter.operator === 'eq') {
    if (values[0] === 'Genel') return 'general';
    if (values[0] === 'Paketli') return 'package';
    return 'all';
  }

  if (scopeFilter.operator === 'in') {
    const hasGenel = values.includes('Genel');
    const hasPaketli = values.includes('Paketli');
    if (hasGenel && !hasPaketli) return 'general';
    if (hasPaketli && !hasGenel) return 'package';
    return 'all';
  }

  if (scopeFilter.operator === 'nin') {
    const excludesGenel = values.includes('Genel');
    const excludesPaketli = values.includes('Paketli');
    if (excludesGenel && !excludesPaketli) return 'package';
    if (excludesPaketli && !excludesGenel) return 'general';
    return 'all';
  }

  if (scopeFilter.operator === 'ne') {
    if (values[0] === 'Genel') return 'package';
    if (values[0] === 'Paketli') return 'general';
  }

  return 'all';
}

export function normalizeRecordScope(value: unknown): OdakRecordScope {
  const key = String(value ?? '').trim();
  if (key === 'Genel') return 'Genel';
  return 'Paketli';
}

export function recordScopeLabel(value: unknown): string {
  const normalized = normalizeRecordScope(value);
  return ODAK_RECORD_SCOPE_OPTIONS.find((o) => o.value === normalized)?.title ?? normalized;
}

export function buildShipmentListFilter(query: OdakShipmentListQuery): string | undefined {
  return composeShipmentListFilterString({
    scopeTab: query.scopeTab ?? 'all',
    statusTab: query.statusTab ?? 'all',
    search: query.search,
    advancedFilters: query.advancedFilters ?? [],
  });
}

const SHIPMENT_PACKAGE_IN_CHUNK_SIZE = 35;
const SHIPMENT_CUSTOMER_MERGE_FETCH_CAP = 5000;

interface ShipmentFilterComposeInput {
  scopeTab: OdakShipmentScopeTab;
  statusTab: OdakShipmentStatusTab;
  search?: string;
  advancedFilters: AfListFilter[];
  extraClauses?: string[];
  skipWaybillIfAdvanced?: boolean;
}

function composeShipmentListFilterString(input: ShipmentFilterComposeInput): string | undefined {
  const parts: string[] = [];
  if (input.scopeTab === 'package') parts.push('recordScope:eq:Paketli');
  if (input.scopeTab === 'general') parts.push('recordScope:eq:Genel');
  if (input.statusTab === 'planned') parts.push('status:eq:Planlandi');
  if (input.statusTab === 'completed') parts.push('status:eq:Tamamlandi');

  const advanced = afListFiltersToQueryString(input.advancedFilters);
  if (advanced) parts.push(advanced);
  if (input.extraClauses?.length) parts.push(...input.extraClauses);

  const search = (input.search ?? '').trim();
  const skipWaybill = input.skipWaybillIfAdvanced ?? true;
  if (search && (!skipWaybill || !advanced.includes('waybillNo:'))) {
    parts.push(`waybillNo:contains:${search}`);
  }
  return parts.length ? parts.join(',') : undefined;
}

function chunkStringIds(ids: string[], chunkSize: number): string[][] {
  const chunks: string[][] = [];
  for (let i = 0; i < ids.length; i += chunkSize) {
    chunks.push(ids.slice(i, i + chunkSize));
  }
  return chunks;
}

function buildRelationInFilterClause(field: string, ids: string[]): string | undefined {
  const unique = [...new Set(ids.map((id) => id.trim()).filter(Boolean))];
  if (!unique.length) return undefined;
  if (unique.length === 1) return `${field}:eq:${unique[0]}`;
  return `${field}:in:${JSON.stringify(unique)}`;
}

function shipmentDateSortKey(row: OdakShipmentRow): number {
  const raw = row.shipmentDate;
  if (!raw) return 0;
  const t = new Date(String(raw)).getTime();
  return Number.isNaN(t) ? 0 : t;
}

function mergeShipmentRowsSorted(rows: OdakShipmentRow[]): OdakShipmentRow[] {
  const byId = new Map<string, OdakShipmentRow>();
  for (const row of rows) {
    const id = shipmentDataId(row);
    if (id) byId.set(id, row);
  }
  return [...byId.values()].sort((a, b) => shipmentDateSortKey(b) - shipmentDateSortKey(a));
}

async function resolveCustomerIdForShipmentFilter(rawValue: string): Promise<string> {
  const trimmed = rawValue.trim();
  if (!trimmed) return '';
  if (/^[a-f0-9]{24}$/i.test(trimmed) || /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(trimmed)) {
    return trimmed;
  }
  const map = await fetchCustomerLabelMap();
  const exact = Object.entries(map).find(
    ([, title]) => title.localeCompare(trimmed, 'tr', { sensitivity: 'base' }) === 0
  );
  if (exact) return exact[0];
  const partial = Object.entries(map).find(([, title]) =>
    title.toLocaleLowerCase('tr-TR').includes(trimmed.toLocaleLowerCase('tr-TR'))
  );
  return partial?.[0] ?? trimmed;
}

function stripRecordScopeFilters(filters: AfListFilter[]): AfListFilter[] {
  return filters.filter((f) => f.field !== 'recordScope');
}

interface ResolvedShipmentCustomerFilter {
  filters: AfListFilter[];
  packageIds: string[];
  customerId: string;
  scopeTab: OdakShipmentScopeTab;
  forceEmpty: boolean;
}

async function resolveShipmentCustomerFilter(
  query: OdakShipmentListQuery
): Promise<ResolvedShipmentCustomerFilter | null> {
  const scopeTab =
    query.scopeTab && query.scopeTab !== 'all'
      ? query.scopeTab
      : deriveScopeTabFromAdvancedFilters(query.advancedFilters ?? []);
  const customerFilter = (query.advancedFilters ?? []).find((f) => f.field === 'customerId' && f.value?.trim());
  if (!customerFilter) return null;

  const rest = (query.advancedFilters ?? []).filter((f) => f !== customerFilter);
  const customerId = await resolveCustomerIdForShipmentFilter(customerFilter.value);
  if (!customerId) {
    return { filters: rest, packageIds: [], customerId: '', scopeTab, forceEmpty: true };
  }

  const packageIds = await fetchPackageIdsByCustomerId(customerId);

  // Legacy sevkiyatlarda customerId yalnizca bazi genel kayitlarda; cogu musteri is paketinde.
  // Kapsam=Genel olsa bile musteri filtresi paket baglantisi uzerinden aranir.
  if (scopeTab === 'general') {
    return {
      filters: rest,
      packageIds,
      customerId,
      scopeTab,
      forceEmpty: packageIds.length === 0,
    };
  }

  if (scopeTab === 'package' && !packageIds.length) {
    return { filters: rest, packageIds: [], customerId, scopeTab, forceEmpty: true };
  }

  return {
    filters: rest,
    packageIds,
    customerId,
    scopeTab,
    forceEmpty: false,
  };
}

async function fetchOdakShipmentsPageSimple(
  query: OdakShipmentListQuery,
  advancedFilters: AfListFilter[]
): Promise<{ items: OdakShipmentRow[]; total: number }> {
  const limit = query.limit ?? 20;
  const page = query.page ?? 1;
  const filter = buildShipmentListFilter({ ...query, advancedFilters });
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.shipmentsDataset, {
    filter,
    sort: query.sort ?? '-shipmentDate',
    skip: (page - 1) * limit,
    limit,
  });
  const items = (resp.items ?? []) as OdakShipmentRow[];
  const total = resp.total && resp.total > 0 ? resp.total : items.length;
  return { items, total };
}

async function fetchOdakShipmentsPageByCustomer(
  query: OdakShipmentListQuery,
  resolved: ResolvedShipmentCustomerFilter
): Promise<{ items: OdakShipmentRow[]; total: number }> {
  const limit = query.limit ?? 20;
  const page = query.page ?? 1;
  const sort = query.sort ?? '-shipmentDate';
  const { scopeTab, filters, packageIds, customerId } = resolved;

  type BranchSpec = {
    scopeTab: OdakShipmentScopeTab;
    extraClauses: string[];
    advancedFilters: AfListFilter[];
  };
  const branches: BranchSpec[] = [];

  if (packageIds.length) {
    const packageFilters =
      scopeTab === 'general' ? stripRecordScopeFilters(filters) : filters;
    for (const chunk of chunkStringIds(packageIds, SHIPMENT_PACKAGE_IN_CHUNK_SIZE)) {
      const clause = buildRelationInFilterClause('parentPackageId', chunk);
      if (clause) {
        branches.push({
          scopeTab: scopeTab === 'all' ? 'all' : 'package',
          extraClauses: [clause],
          advancedFilters: packageFilters,
        });
      }
    }
  }

  // Genel kayitta customerId dolu yeni sevkiyatlar (legacy disi)
  if (scopeTab === 'all' && customerId) {
    branches.push({
      scopeTab: 'general',
      extraClauses: [`customerId:eq:${customerId}`],
      advancedFilters: filters,
    });
  }

  if (!branches.length) return { items: [], total: 0 };

  const branchResults = await Promise.all(
    branches.map(async (branch) => {
      const filter = composeShipmentListFilterString({
        scopeTab: branch.scopeTab,
        statusTab: 'all',
        search: query.search,
        advancedFilters: branch.advancedFilters,
        extraClauses: branch.extraClauses,
      });
      const neededForPage = Math.min(page * limit, SHIPMENT_CUSTOMER_MERGE_FETCH_CAP);
      const perBranchLimit = Math.min(
        SHIPMENT_CUSTOMER_MERGE_FETCH_CAP,
        Math.max(neededForPage, limit)
      );
      const [countResp, dataResp] = await Promise.all([
        ocListDatasetPage(ODAK_SIPARIS_CONFIG.shipmentsDataset, {
          filter,
          sort,
          skip: 0,
          limit: 1,
        }),
        ocListDatasetPage(ODAK_SIPARIS_CONFIG.shipmentsDataset, {
          filter,
          sort,
          skip: 0,
          limit: perBranchLimit,
        }),
      ]);
      const branchTotal = countResp.total && countResp.total > 0 ? countResp.total : 0;
      const rows = (dataResp.items ?? []) as OdakShipmentRow[];
      return { branchTotal, rows };
    })
  );

  let total = 0;
  const branchRows: OdakShipmentRow[] = [];
  for (const result of branchResults) {
    total += result.branchTotal;
    branchRows.push(...result.rows);
  }

  const merged = mergeShipmentRowsSorted(branchRows);
  const skip = (page - 1) * limit;
  return { items: merged.slice(skip, skip + limit), total };
}

export async function fetchOdakShipmentsPage(query: OdakShipmentListQuery): Promise<{
  items: OdakShipmentRow[];
  total: number;
}> {
  const customerResolved = await resolveShipmentCustomerFilter(query);
  if (customerResolved?.forceEmpty) return { items: [], total: 0 };
  if (customerResolved) {
    return fetchOdakShipmentsPageByCustomer(query, customerResolved);
  }
  return fetchOdakShipmentsPageSimple(query, query.advancedFilters ?? []);
}

export function buildShipmentLinesByShipmentFilter(shipmentId: string): string {
  return `parentShipmentId:eq:${shipmentId}`;
}

export function buildShipmentLinesByShipmentsFilter(shipmentIds: string[]): string | undefined {
  const ids = shipmentIds.map((id) => id.trim()).filter(Boolean);
  if (!ids.length) return undefined;
  return `parentShipmentId:in:${JSON.stringify(ids)}`;
}

/** Single request (or chunked) line qty totals for list views — avoids N+1 parentShipmentId:eq calls. */
export async function fetchShipmentLineQtyMap(shipmentIds: string[]): Promise<Map<string, number>> {
  const map = new Map<string, number>();
  const ids = shipmentIds.map((id) => id.trim()).filter(Boolean);
  if (!ids.length) return map;

  const chunkSize = 50;
  for (let offset = 0; offset < ids.length; offset += chunkSize) {
    const chunk = ids.slice(offset, offset + chunkSize);
    const filter = buildShipmentLinesByShipmentsFilter(chunk);
    const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.shipmentLinesDataset, {
      filter,
      limit: 2000,
    });
    for (const line of (resp.items ?? []) as OdakShipmentLineRow[]) {
      const shipId = resolveRelationId(line.parentShipmentId);
      if (!shipId) continue;
      const qty = Number(line.shippedQuantity) || 0;
      map.set(shipId, (map.get(shipId) ?? 0) + qty);
    }
  }
  return map;
}

export function buildShipmentLinesByPackageFilter(parentPackageId: string): string {
  return `parentPackageId:eq:${parentPackageId}`;
}

export function shipmentBelongsToPackage(row: Record<string, unknown>, packageId: string): boolean {
  if (!packageId) return false;
  return resolveRelationId(row.parentPackageId) === packageId;
}

export function normalizeShipmentStatus(value: unknown): string {
  const key = String(value ?? '').trim();
  if (!key) return 'Planlandi';
  const exact = ODAK_SHIPMENT_STATUS_OPTIONS.find((o) => o.value === key);
  if (exact) return exact.value;
  if (/^Plan/i.test(key)) return 'Planlandi';
  if (/^Tamam/i.test(key)) return 'Tamamlandi';
  if (/^Iptal/i.test(key) || /^İptal/i.test(key)) return 'Iptal';
  return key;
}

export function shipmentStatusLabel(value: unknown): string {
  if (value == null || value === '') return '—';
  const normalized = normalizeShipmentStatus(value);
  return ODAK_SHIPMENT_STATUS_OPTIONS.find((o) => o.value === normalized)?.title ?? normalized;
}

export function normalizeQcfStatus(value: unknown): string {
  const key = String(value ?? '').trim();
  if (!key) return 'Yok';
  const exact = ODAK_QCF_STATUS_OPTIONS.find((o) => o.value === key);
  if (exact) return exact.value;
  if (/^Bekl/i.test(key)) return 'Bekliyor';
  if (/^Tamam/i.test(key)) return 'Tamamlandi';
  return key === 'Yok' ? 'Yok' : key;
}

export function qcfStatusLabel(value: unknown): string {
  if (value == null || value === '') return '—';
  const normalized = normalizeQcfStatus(value);
  return ODAK_QCF_STATUS_OPTIONS.find((o) => o.value === normalized)?.title ?? normalized;
}

export function lineLabelFromShipmentLine(raw: unknown): string {
  if (raw == null) return '—';
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    const no = o.lineNo != null ? String(o.lineNo) : '';
    const desc = o.lineDescription != null ? String(o.lineDescription) : o.description != null ? String(o.description) : '';
    if (no && desc) return `${no} — ${desc}`;
    if (no) return no;
    if (desc) return desc;
    return resolveRelationId(o) || '—';
  }
  return String(raw) || '—';
}

export interface OdakShipmentLineDraft {
  parentLineId: string;
  shippedQuantity: number;
  lineDescription?: string;
}

export interface OdakGeneralShipmentLineDraft {
  lineDescription: string;
  shippedQuantity: number;
}

export interface OdakShipmentFormModel {
  waybillNo: string;
  shipmentDate: string;
  status: string;
  controlType: string;
  shipmentAddress: string;
  notes: string;
  qcfStatus: string;
  qcfReferenceNo: string;
  qcfNotes: string;
  lines: OdakShipmentLineDraft[];
}

export interface OdakGeneralShipmentFormModel {
  customerId: string;
  headerDescription: string;
  waybillNo: string;
  shipmentDate: string;
  status: string;
  controlType: string;
  shipmentAddress: string;
  notes: string;
  qcfStatus: string;
  qcfReferenceNo: string;
  qcfNotes: string;
  lines: OdakGeneralShipmentLineDraft[];
}

export function emptyShipmentFormModel(partial?: Partial<OdakShipmentFormModel>): OdakShipmentFormModel {
  return {
    waybillNo: partial?.waybillNo ?? '',
    shipmentDate: partial?.shipmentDate ?? '',
    status: partial?.status ?? 'Planlandi',
    controlType: partial?.controlType ?? '',
    shipmentAddress: partial?.shipmentAddress ?? '',
    notes: partial?.notes ?? '',
    qcfStatus: partial?.qcfStatus ?? 'Yok',
    qcfReferenceNo: partial?.qcfReferenceNo ?? '',
    qcfNotes: partial?.qcfNotes ?? '',
    lines: partial?.lines ? partial.lines.map((l) => ({ ...l })) : [{ parentLineId: '', shippedQuantity: 1 }],
  };
}

export function shipmentHeaderToFormModel(row: OdakShipmentRow): Omit<OdakShipmentFormModel, 'lines'> {
  return {
    waybillNo: row.waybillNo ?? '',
    shipmentDate: toDateInputValue(row.shipmentDate),
    status: normalizeShipmentStatus(row.status),
    controlType: row.controlType ?? '',
    shipmentAddress: row.shipmentAddress ?? '',
    notes: row.notes ?? '',
    qcfStatus: normalizeQcfStatus(row.qcfStatus),
    qcfReferenceNo: row.qcfReferenceNo ?? '',
    qcfNotes: row.qcfNotes ?? '',
  };
}

function headerFormToPayload(form: Omit<OdakShipmentFormModel, 'lines'>, packageId: string): Record<string, unknown> {
  return {
    recordScope: 'Paketli',
    parentPackageId: packageId,
    legacyShipmentId: newNativeLegacyShipmentId(),
    waybillNo: form.waybillNo.trim() || null,
    shipmentDate: fromDateInputValue(form.shipmentDate),
    status: normalizeShipmentStatus(form.status),
    controlType: form.controlType.trim() || null,
    shipmentAddress: form.shipmentAddress.trim() || null,
    notes: form.notes.trim() || null,
    qcfStatus: normalizeQcfStatus(form.qcfStatus),
    qcfReferenceNo: form.qcfReferenceNo.trim() || null,
    qcfNotes: form.qcfNotes.trim() || null,
  };
}

export async function fetchOdakShipmentById(shipmentId: string): Promise<OdakShipmentRow | null> {
  if (!shipmentId) return null;
  const url = `/api/v1/data/${encodeURIComponent(ODAK_SIPARIS_CONFIG.shipmentsDataset)}/${encodeURIComponent(shipmentId)}?expand=true`;
  const raw = await fetchFromDataGateway(url, 'GET');
  const item = Array.isArray(raw) ? raw[0] : raw;
  if (!item || typeof item !== 'object') return null;
  return item as OdakShipmentRow;
}

export async function listShipmentsForPackage(packageId: string): Promise<OdakShipmentRow[]> {
  const filter = buildShipmentsByParentPackageFilter(packageId);
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.shipmentsDataset, {
    filter,
    sort: '-shipmentDate',
    limit: 500,
  });
  return ((resp.items ?? []) as OdakShipmentRow[]).filter((row) =>
    shipmentBelongsToPackage(row as Record<string, unknown>, packageId)
  );
}

export async function listShipmentLinesForShipment(shipmentId: string): Promise<OdakShipmentLineRow[]> {
  if (!shipmentId) return [];
  const filter = buildShipmentLinesByShipmentFilter(shipmentId);
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.shipmentLinesDataset, {
    filter,
    sort: 'lineNo:asc',
    limit: 500,
  });
  return (resp.items ?? []) as OdakShipmentLineRow[];
}

export async function listShipmentLinesForPackage(packageId: string): Promise<OdakShipmentLineRow[]> {
  const filter = buildShipmentLinesByPackageFilter(packageId);
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.shipmentLinesDataset, {
    filter,
    sort: 'lineNo:asc',
    limit: 2000,
  });
  return ((resp.items ?? []) as OdakShipmentLineRow[]).filter(
    (row) => resolveRelationId(row.parentPackageId) === packageId
  );
}

export function countCompletedShipments(items: OdakShipmentRow[]): number {
  return items.filter((s) => normalizeShipmentStatus(s.status) === 'Tamamlandi').length;
}

export function sumShipmentLineQuantities(lines: OdakShipmentLineRow[]): number {
  return lines.reduce((sum, row) => sum + (Number(row.shippedQuantity) || 0), 0);
}

export interface OdakShipmentQtySummary {
  /** Bu sevkiyattaki sevk miktarları toplamı */
  shippedQty: number;
  /** Bağlı sipariş kalemlerinin sipariş miktarı toplamı */
  orderQty: number;
  /** Bu sevkiyat satırında: orderQty − shippedQty (bu sevk kaydında karşılanmayan miktar) */
  remainingQty: number;
}

export function buildParentLineMap(lines: OdakLineRow[]): Map<string, OdakLineRow> {
  const map = new Map<string, OdakLineRow>();
  for (const line of lines) {
    const id = lineDataId(line);
    if (id) map.set(id, line);
  }
  return map;
}

export function buildShipmentQtySummaryMap(
  shipmentLines: OdakShipmentLineRow[],
  parentLineMap: Map<string, OdakLineRow>
): Map<string, OdakShipmentQtySummary> {
  const scratch = new Map<
    string,
    { shippedQty: number; orderQty: number; seenParentIds: Set<string> }
  >();

  for (const sl of shipmentLines) {
    const shipId = resolveRelationId(sl.parentShipmentId);
    if (!shipId) continue;

    let entry = scratch.get(shipId);
    if (!entry) {
      entry = { shippedQty: 0, orderQty: 0, seenParentIds: new Set() };
      scratch.set(shipId, entry);
    }

    entry.shippedQty += Number(sl.shippedQuantity) || 0;

    const parentId = resolveRelationId(sl.parentLineId);
    if (!parentId || entry.seenParentIds.has(parentId)) continue;
    entry.seenParentIds.add(parentId);

    const parent = parentLineMap.get(parentId);
    if (!parent) continue;
    entry.orderQty += Number(parent.quantity) || 0;
  }

  const result = new Map<string, OdakShipmentQtySummary>();
  for (const [shipId, entry] of scratch) {
    result.set(shipId, {
      shippedQty: entry.shippedQty,
      orderQty: entry.orderQty,
      remainingQty: Math.max(0, entry.orderQty - entry.shippedQty),
    });
  }
  return result;
}

export interface OdakShipmentLineQtyView {
  line: OdakShipmentLineRow;
  orderQty: number | null;
  shippedQty: number;
  remainingQty: number | null;
  unit: string;
}

export function buildShipmentLineQtyViews(
  shipmentLines: OdakShipmentLineRow[],
  parentLineMap: Map<string, OdakLineRow>
): OdakShipmentLineQtyView[] {
  return shipmentLines.map((line) => {
    const parentId = resolveRelationId(line.parentLineId);
    const parent = parentId ? parentLineMap.get(parentId) : undefined;
    const orderQty = parent != null ? Number(parent.quantity) || 0 : null;
    const shippedQty = Number(line.shippedQuantity) || 0;
    return {
      line,
      orderQty,
      shippedQty,
      remainingQty:
        orderQty != null ? Math.max(0, orderQty - shippedQty) : null,
      unit: String(parent?.unit ?? '').trim(),
    };
  });
}

async function loadLineMap(packageId: string): Promise<Map<string, OdakLineRow>> {
  const lines = await listLinesForPackage(packageId);
  return buildParentLineMap(lines);
}

export async function validateShipmentLines(
  packageId: string,
  drafts: OdakShipmentLineDraft[],
  shipmentId?: string
): Promise<string | null> {
  const cleaned = drafts.filter((d) => d.parentLineId && d.shippedQuantity > 0);
  if (!cleaned.length) return 'En az bir kalem ve miktar girilmelidir.';

  const lineMap = await loadLineMap(packageId);
  const allShipments = await listShipmentsForPackage(packageId);
  const completedShipmentIds = new Set(
    allShipments
      .filter((s) => {
        const id = shipmentDataId(s);
        if (!id) return false;
        if (shipmentId && id === shipmentId) return false;
        return normalizeShipmentStatus(s.status) === 'Tamamlandi';
      })
      .map((s) => shipmentDataId(s))
  );

  const allItems = await listShipmentLinesForPackage(packageId);
  const shippedByLine = new Map<string, number>();
  for (const item of allItems) {
    const shipId = resolveRelationId(item.parentShipmentId);
    if (!completedShipmentIds.has(shipId)) continue;
    const lineId = resolveRelationId(item.parentLineId);
    if (!lineId) continue;
    shippedByLine.set(lineId, (shippedByLine.get(lineId) ?? 0) + (Number(item.shippedQuantity) || 0));
  }

  const draftByLine = new Map<string, number>();
  for (const draft of cleaned) {
    draftByLine.set(draft.parentLineId, (draftByLine.get(draft.parentLineId) ?? 0) + draft.shippedQuantity);
  }

  for (const [lineId, draftQty] of draftByLine) {
    const line = lineMap.get(lineId);
    if (!line) return `Kalem bulunamadı.`;
    const maxQty = Number(line.quantity) || 0;
    const already = shippedByLine.get(lineId) ?? 0;
    if (already + draftQty > maxQty + 0.0001) {
      const no = line.lineNo ?? '—';
      return `Kalem ${no}: toplam sevk (${already + draftQty}) sipariş miktarını (${maxQty}) aşıyor.`;
    }
  }

  return null;
}

async function createShipmentLineRecords(
  packageId: string,
  shipmentId: string,
  drafts: OdakShipmentLineDraft[],
  lineMap: Map<string, OdakLineRow>
): Promise<void> {
  for (const draft of drafts) {
    if (!draft.parentLineId || draft.shippedQuantity <= 0) continue;
    const line = lineMap.get(draft.parentLineId);
    await ocCreate(ODAK_SIPARIS_CONFIG.shipmentLinesDataset, {
      parentShipmentId: shipmentId,
      lineMode: 'SiparisKalemi',
      parentPackageId: packageId,
      parentLineId: draft.parentLineId,
      shippedQuantity: draft.shippedQuantity,
      lineNo: line?.lineNo ?? null,
      lineDescription: line?.description?.trim() || null,
    });
  }
}

async function deleteShipmentLineRecords(shipmentId: string): Promise<void> {
  const items = await listShipmentLinesForShipment(shipmentId);
  for (const item of items) {
    const id = shipmentLineDataId(item);
    if (id) await ocDelete(ODAK_SIPARIS_CONFIG.shipmentLinesDataset, id);
  }
}

export async function recalculatePackageLineShippedQuantities(packageId: string): Promise<void> {
  if (!packageId) return;
  const lines = await listLinesForPackage(packageId);
  const shipments = await listShipmentsForPackage(packageId);
  const completedIds = new Set(
    shipments
      .filter((s) => normalizeShipmentStatus(s.status) === 'Tamamlandi')
      .map((s) => shipmentDataId(s))
      .filter(Boolean)
  );

  const allItems = await listShipmentLinesForPackage(packageId);
  const shippedByLine = new Map<string, number>();
  for (const item of allItems) {
    const shipId = resolveRelationId(item.parentShipmentId);
    if (!completedIds.has(shipId)) continue;
    const lineId = resolveRelationId(item.parentLineId);
    if (!lineId) continue;
    shippedByLine.set(lineId, (shippedByLine.get(lineId) ?? 0) + (Number(item.shippedQuantity) || 0));
  }

  for (const line of lines) {
    const lineId = lineDataId(line);
    if (!lineId) continue;
    const shipped = shippedByLine.get(lineId) ?? 0;
    const current = Number(line.shippedQuantity) || 0;
    if (Math.abs(current - shipped) < 0.0001) continue;
    await ocUpdate(ODAK_SIPARIS_CONFIG.linesDataset, lineId, { shippedQuantity: shipped });
    line.shippedQuantity = shipped;
  }

  let totalQuantity = 0;
  let totalShipped = 0;
  for (const line of lines) {
    totalQuantity += Number(line.quantity) || 0;
    const lineId = lineDataId(line);
    const shipped = lineId ? (shippedByLine.get(lineId) ?? 0) : 0;
    totalShipped += shipped;
  }
  const stockCount = Math.max(0, totalQuantity - totalShipped);

  const pkg = await fetchOdakPackageById(packageId);
  if (pkg) {
    const currentShipped = Number(pkg.shippedCount) || 0;
    const currentStock = Number(pkg.stockCount) || 0;
    if (Math.abs(currentShipped - totalShipped) >= 0.0001 || Math.abs(currentStock - stockCount) >= 0.0001) {
      await ocUpdate(ODAK_SIPARIS_CONFIG.packagesDataset, packageId, {
        shippedCount: totalShipped,
        stockCount,
      });
    }
  }
}

async function applyShipmentFieldPolicyToPayload(
  body: Record<string, unknown>,
  existingRow?: OdakShipmentRow | null
): Promise<Record<string, unknown>> {
  let blob = { policiesByField: {} };
  try {
    blob = await loadOdakShipmentFieldPoliciesOnly();
  } catch {
    /* open defaults */
  }
  const auth = useAuthStore();
  const record = existingRow
    ? shipmentRecordForPolicyEval(existingRow)
    : shipmentRecordForPolicyEval(body as OdakShipmentRow);
  return filterShipmentPayloadByFieldAccess(body, auth.userGroups, record, blob);
}

export async function createOdakShipment(
  packageId: string,
  form: OdakShipmentFormModel
): Promise<string | null> {
  const drafts = form.lines.filter((l) => l.parentLineId && l.shippedQuantity > 0);
  if (normalizeShipmentStatus(form.status) === 'Tamamlandi') {
    const err = await validateShipmentLines(packageId, drafts);
    if (err) throw new Error(err);
  }

  const lineMap = await loadLineMap(packageId);
  let header = headerFormToPayload(form, packageId);
  header = await applyShipmentFieldPolicyToPayload(header);
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.shipmentsDataset, header);
  const shipmentId = shipmentDataId(created as Record<string, unknown>);
  if (!shipmentId) return null;

  await createShipmentLineRecords(packageId, shipmentId, drafts, lineMap);
  if (normalizeShipmentStatus(form.status) === 'Tamamlandi') {
    await recalculatePackageLineShippedQuantities(packageId);
  }
  const pkg = await fetchOdakPackageById(packageId);
  void dispatchOdakPackageNotification('ShipmentCompleted', pkg, {
    shipmentPreviousStatus: null,
    shipmentNewStatus: normalizeShipmentStatus(form.status),
  });
  return shipmentId;
}

export async function updateOdakShipment(
  shipmentId: string,
  packageId: string,
  form: OdakShipmentFormModel
): Promise<void> {
  const existing = await fetchOdakShipmentById(shipmentId);
  const previousStatus = existing ? normalizeShipmentStatus(existing.status) : null;

  const drafts = form.lines.filter((l) => l.parentLineId && l.shippedQuantity > 0);
  if (normalizeShipmentStatus(form.status) === 'Tamamlandi') {
    const err = await validateShipmentLines(packageId, drafts, shipmentId);
    if (err) throw new Error(err);
  }

  const lineMap = await loadLineMap(packageId);
  let header = headerFormToPayload(form, packageId);
  header = await applyShipmentFieldPolicyToPayload(header, existing ?? null);
  await ocUpdate(ODAK_SIPARIS_CONFIG.shipmentsDataset, shipmentId, header);
  await deleteShipmentLineRecords(shipmentId);
  await createShipmentLineRecords(packageId, shipmentId, drafts, lineMap);
  await recalculatePackageLineShippedQuantities(packageId);

  const pkg = await fetchOdakPackageById(packageId);
  void dispatchOdakPackageNotification('ShipmentCompleted', pkg, {
    shipmentPreviousStatus: previousStatus,
    shipmentNewStatus: normalizeShipmentStatus(form.status),
  });
}

export async function deleteOdakShipment(shipmentId: string, packageId: string): Promise<void> {
  await deleteShipmentLineRecords(shipmentId);
  await ocDelete(ODAK_SIPARIS_CONFIG.shipmentsDataset, shipmentId);
  await recalculatePackageLineShippedQuantities(packageId);
}

export function formatShipmentDate(value: unknown): string {
  return formatOdakDate(value);
}

export async function loadShipmentFormModel(
  shipmentId: string,
  packageId: string
): Promise<OdakShipmentFormModel | null> {
  const header = await fetchOdakShipmentById(shipmentId);
  if (!header) return null;
  const items = await listShipmentLinesForShipment(shipmentId);
  const lines: OdakShipmentLineDraft[] = items.length
    ? items.map((item) => ({
        parentLineId: resolveRelationId(item.parentLineId),
        shippedQuantity: Number(item.shippedQuantity) || 0,
      }))
    : [{ parentLineId: '', shippedQuantity: 1 }];
  return {
    ...shipmentHeaderToFormModel(header),
    lines,
  };
}

export function remainingQuantityForLine(line: OdakLineRow, shippedQuantity: number): number {
  const qty = Number(line.quantity) || 0;
  const shipped = Number(shippedQuantity) || 0;
  return Math.max(0, qty - shipped);
}

export interface OdakLineQuantityAggregate {
  totalQuantity: number;
  totalShipped: number;
  totalRemaining: number;
}

export function aggregateLineQuantities(lines: OdakLineRow[]): OdakLineQuantityAggregate {
  let totalQuantity = 0;
  let totalShipped = 0;
  for (const line of lines) {
    totalQuantity += Number(line.quantity) || 0;
    totalShipped += Number(line.shippedQuantity) || 0;
  }
  return {
    totalQuantity,
    totalShipped,
    totalRemaining: Math.max(0, totalQuantity - totalShipped),
  };
}

export function emptyGeneralShipmentFormModel(
  partial?: Partial<OdakGeneralShipmentFormModel>
): OdakGeneralShipmentFormModel {
  return {
    customerId: partial?.customerId ?? '',
    headerDescription: partial?.headerDescription ?? '',
    waybillNo: partial?.waybillNo ?? '',
    shipmentDate: partial?.shipmentDate ?? '',
    status: partial?.status ?? 'Planlandi',
    controlType: partial?.controlType ?? '',
    shipmentAddress: partial?.shipmentAddress ?? '',
    notes: partial?.notes ?? '',
    qcfStatus: partial?.qcfStatus ?? 'Yok',
    qcfReferenceNo: partial?.qcfReferenceNo ?? '',
    qcfNotes: partial?.qcfNotes ?? '',
    lines: partial?.lines?.length
      ? partial.lines.map((l) => ({ ...l }))
      : [{ lineDescription: '', shippedQuantity: 1 }],
  };
}

function generalHeaderToPayload(form: OdakGeneralShipmentFormModel): Record<string, unknown> {
  return {
    recordScope: 'Genel',
    legacyShipmentId: newNativeLegacyShipmentId(),
    customerId: form.customerId.trim() || null,
    headerDescription: form.headerDescription.trim() || null,
    waybillNo: form.waybillNo.trim() || null,
    shipmentDate: fromDateInputValue(form.shipmentDate),
    status: normalizeShipmentStatus(form.status),
    controlType: form.controlType.trim() || null,
    shipmentAddress: form.shipmentAddress.trim() || null,
    notes: form.notes.trim() || null,
    qcfStatus: normalizeQcfStatus(form.qcfStatus),
    qcfReferenceNo: form.qcfReferenceNo.trim() || null,
    qcfNotes: form.qcfNotes.trim() || null,
  };
}

async function createGeneralShipmentLineRecords(
  shipmentId: string,
  drafts: OdakGeneralShipmentLineDraft[]
): Promise<void> {
  for (const draft of drafts) {
    if (!draft.lineDescription.trim() || draft.shippedQuantity <= 0) continue;
    await ocCreate(ODAK_SIPARIS_CONFIG.shipmentLinesDataset, {
      parentShipmentId: shipmentId,
      lineMode: 'Serbest',
      lineDescription: draft.lineDescription.trim(),
      shippedQuantity: draft.shippedQuantity,
    });
  }
}

export async function createGeneralOdakShipment(form: OdakGeneralShipmentFormModel): Promise<string | null> {
  const drafts = form.lines.filter((l) => l.lineDescription.trim() && l.shippedQuantity > 0);
  if (!drafts.length) throw new Error('En az bir serbest kalem girilmelidir.');
  if (!form.headerDescription.trim() && !form.waybillNo.trim()) {
    throw new Error('İrsaliye no veya sevkiyat içeriği zorunludur.');
  }

  let header = generalHeaderToPayload(form);
  header = await applyShipmentFieldPolicyToPayload(header);
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.shipmentsDataset, header);
  const shipmentId = shipmentDataId(created as Record<string, unknown>);
  if (!shipmentId) return null;
  await createGeneralShipmentLineRecords(shipmentId, drafts);
  const createdRow = await fetchOdakShipmentById(shipmentId);
  if (createdRow) {
    void import('@/utils/odakSiparisNotificationDispatch').then(({ dispatchOdakGlobalShipmentNotification }) =>
      dispatchOdakGlobalShipmentNotification(createdRow, {
        actorPersonId: useAuthStore().userInfo?.mng_person_id ?? null,
        lineCount: drafts.length,
      })
    );
  }
  return shipmentId;
}

export async function updateGeneralOdakShipment(
  shipmentId: string,
  form: OdakGeneralShipmentFormModel
): Promise<void> {
  const drafts = form.lines.filter((l) => l.lineDescription.trim() && l.shippedQuantity > 0);
  if (!drafts.length) throw new Error('En az bir serbest kalem girilmelidir.');

  const existing = await fetchOdakShipmentById(shipmentId);
  let header = generalHeaderToPayload(form);
  header = await applyShipmentFieldPolicyToPayload(header, existing ?? null);
  await ocUpdate(ODAK_SIPARIS_CONFIG.shipmentsDataset, shipmentId, header);
  await deleteShipmentLineRecords(shipmentId);
  await createGeneralShipmentLineRecords(shipmentId, drafts);
}

export async function deleteGeneralOdakShipment(shipmentId: string): Promise<void> {
  await deleteShipmentLineRecords(shipmentId);
  await ocDelete(ODAK_SIPARIS_CONFIG.shipmentsDataset, shipmentId);
}

export async function loadGeneralShipmentFormModel(shipmentId: string): Promise<OdakGeneralShipmentFormModel | null> {
  const header = await fetchOdakShipmentById(shipmentId);
  if (!header) return null;
  const items = await listShipmentLinesForShipment(shipmentId);
  const lines: OdakGeneralShipmentLineDraft[] = items.length
    ? items.map((item) => ({
        lineDescription: item.lineDescription ?? '',
        shippedQuantity: Number(item.shippedQuantity) || 0,
      }))
    : [{ lineDescription: header.headerDescription ?? '', shippedQuantity: 1 }];
  return emptyGeneralShipmentFormModel({
    customerId: resolveRelationId(header.customerId),
    headerDescription: header.headerDescription ?? '',
    ...shipmentHeaderToFormModel(header),
    lines,
  });
}
