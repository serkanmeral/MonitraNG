import { fetchFromDataGateway } from '@/services/apiService';
import { ocCreate, ocListDatasetPage, ocUpdate } from '@/services/operationCoreService';
import {
  ODAK_FAI_STATUS_OPTIONS,
  ODAK_NCR_STATUS_OPTIONS,
  ODAK_RECORD_SCOPE_OPTIONS,
  ODAK_SIPARIS_CONFIG,
  type OdakNcrRow,
  type OdakRecordScope,
} from '@/utils/odakSiparisConfig';
import { formatOdakDate, buildRelationInFilter, packageDataId } from '@/utils/odakSiparisService';
import { fromDateInputValue, toDateInputValue } from '@/utils/odakSiparisDateUtils';

export type OdakNcrDialogMode = 'view' | 'create' | 'edit';

function resolveRelationId(raw: unknown): string {
  if (raw == null) return '';
  if (typeof raw === 'string') return raw.trim();
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    return String(o.__dataId ?? o.dataId ?? o.id ?? '').trim();
  }
  return String(raw).trim();
}

export function ncrDataId(row: OdakNcrRow | Record<string, unknown>): string {
  return packageDataId(row);
}

export function buildNcrByParentPackageFilter(parentPackageId: string): string {
  return `parentPackageId:eq:${parentPackageId}`;
}

export function buildNcrByRecordScopeFilter(scope: OdakRecordScope): string {
  return `recordScope:eq:${scope}`;
}

export type OdakNcrScopeTab = 'all' | 'package' | 'general';
export type OdakNcrStatusTab = 'open' | 'all';

export interface OdakNcrListQuery {
  scopeTab?: OdakNcrScopeTab;
  statusTab?: OdakNcrStatusTab;
  search?: string;
  page?: number;
  limit?: number;
  sort?: string;
}

export function normalizeNcrRecordScope(value: unknown): OdakRecordScope {
  const key = String(value ?? '').trim();
  if (key === 'Genel') return 'Genel';
  return 'Paketli';
}

export function ncrRecordScopeLabel(value: unknown): string {
  const normalized = normalizeNcrRecordScope(value);
  return ODAK_RECORD_SCOPE_OPTIONS.find((o) => o.value === normalized)?.title ?? normalized;
}

export function buildNcrListFilter(query: OdakNcrListQuery): string | undefined {
  const parts: string[] = [];
  const scopeTab = query.scopeTab ?? 'all';
  if (scopeTab === 'package') parts.push('recordScope:eq:Paketli');
  if (scopeTab === 'general') parts.push('recordScope:eq:Genel');
  const search = (query.search ?? '').trim();
  if (search) {
    parts.push(`descriptor:contains:${search}`);
  }
  return parts.length ? parts.join(',') : undefined;
}

export async function fetchOdakNcrsPage(query: OdakNcrListQuery): Promise<{
  items: OdakNcrRow[];
  total: number;
}> {
  const limit = query.limit ?? 20;
  const page = query.page ?? 1;
  const filter = buildNcrListFilter(query);
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.ncrDataset, {
    filter,
    sort: query.sort ?? '-ncDate',
    skip: (page - 1) * limit,
    limit,
  });
  let items = (resp.items ?? []) as OdakNcrRow[];
  if (query.statusTab === 'open') {
    items = items.filter((row) => normalizeNcrStatus(row.ncStatus) !== 'Kapalı');
  }
  return {
    items,
    total: Number(resp.total ?? items.length),
  };
}

export function buildNcrByParentPackagesFilter(parentPackageIds: string[]): string | undefined {
  return buildRelationInFilter('parentPackageId', parentPackageIds);
}

export function ncrBelongsToPackage(row: Record<string, unknown>, packageId: string): boolean {
  if (!packageId) return false;
  return resolveRelationId(row.parentPackageId) === packageId;
}

export function normalizeNcrStatus(value: unknown): string {
  const key = String(value ?? '').trim();
  if (!key) return '';
  const exact = ODAK_NCR_STATUS_OPTIONS.find((o) => o.value === key);
  if (exact) return exact.value;
  // Legacy TSV migration mojibake (Kapalı -> Kapal╒ / Kapal�)
  if (/^Kapal/i.test(key)) return 'Kapalı';
  if (/^MRB/i.test(key)) return 'MRB Bekleniyor';
  if (/^Rework/i.test(key)) return 'Rework/Kontrol Bekleniyor';
  if (/erlendirme Bekleniyor/i.test(key)) return 'Değerlendirme Bekleniyor';
  if (/^DF No/i.test(key)) return 'DF No Bekleniyor';
  if (/Dönüşü Bekleniyor/i.test(key) || /^M[uü]şteri/i.test(key)) return 'Müşteri Dönüşü Bekleniyor';
  if (/^DF D/i.test(key)) return 'DF Düzenlenecek';
  return key;
}

export function ncrStatusLabel(value: unknown): string {
  if (value == null || value === '') return '—';
  const normalized = normalizeNcrStatus(value);
  return ODAK_NCR_STATUS_OPTIONS.find((o) => o.value === normalized)?.title ?? normalized;
}

export function faiStatusLabel(value: unknown): string {
  if (value == null || value === '') return '—';
  const key = String(value);
  return ODAK_FAI_STATUS_OPTIONS.find((o) => o.value === key)?.title ?? key;
}

export function lineLabelFromRow(raw: unknown): string {
  if (raw == null) return '—';
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    const no = o.lineNo != null ? String(o.lineNo) : '';
    const desc = o.description != null ? String(o.description) : '';
    if (no && desc) return `${no} — ${desc}`;
    if (no) return no;
    if (desc) return desc;
    return resolveRelationId(o) || '—';
  }
  return String(raw) || '—';
}

export function lineIdFromRow(raw: unknown): string {
  if (raw == null) return '';
  if (typeof raw === 'string') return raw;
  if (typeof raw === 'object') return resolveRelationId(raw);
  return String(raw);
}

export interface OdakNcrFormModel {
  ncStatus: string;
  ncDate: string;
  controlType: string;
  descriptor: string;
  explanation: string;
  parentLineId: string | null;
  productCode: string;
  jobNo: string;
  partCount: number | null;
  reworkCount: number | null;
  scrapCount: number | null;
  faiStatus: string;
  errorCode: string;
  ncAction: string;
  responsible: string;
  supplierRef: string;
  closureDate: string;
  notes: string;
}

export function emptyNcrFormModel(partial?: Partial<OdakNcrFormModel>): OdakNcrFormModel {
  return {
    ncStatus: partial?.ncStatus ?? 'Değerlendirme Bekleniyor',
    ncDate: partial?.ncDate ?? '',
    controlType: partial?.controlType ?? '',
    descriptor: partial?.descriptor ?? '',
    explanation: partial?.explanation ?? '',
    parentLineId: partial?.parentLineId ?? null,
    productCode: partial?.productCode ?? '',
    jobNo: partial?.jobNo ?? '',
    partCount: partial?.partCount ?? null,
    reworkCount: partial?.reworkCount ?? null,
    scrapCount: partial?.scrapCount ?? null,
    faiStatus: partial?.faiStatus ?? '',
    errorCode: partial?.errorCode ?? '',
    ncAction: partial?.ncAction ?? '',
    responsible: partial?.responsible ?? '',
    supplierRef: partial?.supplierRef ?? '',
    closureDate: partial?.closureDate ?? '',
    notes: partial?.notes ?? '',
  };
}

export function ncrRowToFormModel(row: OdakNcrRow): OdakNcrFormModel {
  return emptyNcrFormModel({
    ncStatus: normalizeNcrStatus(row.ncStatus) || 'Değerlendirme Bekleniyor',
    ncDate: toDateInputValue(row.ncDate),
    controlType: row.controlType ?? '',
    descriptor: row.descriptor ?? '',
    explanation: row.explanation ?? '',
    parentLineId: lineIdFromRow(row.parentLineId) || null,
    productCode: row.productCode ?? '',
    jobNo: row.jobNo ?? '',
    partCount: row.partCount ?? null,
    reworkCount: row.reworkCount ?? null,
    scrapCount: row.scrapCount ?? null,
    faiStatus: row.faiStatus ?? '',
    errorCode: row.errorCode ?? '',
    ncAction: row.ncAction ?? '',
    responsible: row.responsible ?? '',
    supplierRef: row.supplierRef ?? '',
    closureDate: toDateInputValue(row.closureDate),
    notes: row.notes ?? '',
  });
}

export function formModelToNcrPayload(
  form: OdakNcrFormModel,
  packageId?: string
): Record<string, unknown> {
  const isGeneral = !packageId?.trim();
  const body: Record<string, unknown> = {
    recordScope: isGeneral ? 'Genel' : 'Paketli',
    ncStatus: form.ncStatus || 'Değerlendirme Bekleniyor',
    ncDate: fromDateInputValue(form.ncDate),
    controlType: form.controlType.trim() || null,
    descriptor: form.descriptor.trim(),
    explanation: form.explanation.trim() || null,
    parentLineId: form.parentLineId || null,
    productCode: form.productCode.trim() || null,
    jobNo: form.jobNo.trim() || null,
    partCount: form.partCount,
    reworkCount: form.reworkCount,
    scrapCount: form.scrapCount,
    faiStatus: form.faiStatus.trim() || null,
    errorCode: form.errorCode.trim() || null,
    ncAction: form.ncAction.trim() || null,
    responsible: form.responsible.trim() || null,
    supplierRef: form.supplierRef.trim() || null,
    closureDate: fromDateInputValue(form.closureDate),
    notes: form.notes.trim() || null,
  };
  if (!isGeneral) {
    body.parentPackageId = packageId;
  }
  return body;
}

export async function fetchOdakNcrById(ncrId: string): Promise<OdakNcrRow | null> {
  if (!ncrId) return null;
  const url = `/api/v1/data/${encodeURIComponent(ODAK_SIPARIS_CONFIG.ncrDataset)}/${encodeURIComponent(ncrId)}?expand=true`;
  const raw = await fetchFromDataGateway(url, 'GET');
  const item = Array.isArray(raw) ? raw[0] : raw;
  if (!item || typeof item !== 'object') return null;
  return item as OdakNcrRow;
}

export async function listNcrsForPackage(packageId: string): Promise<OdakNcrRow[]> {
  if (!packageId) return [];
  return listNcrsForPackages([packageId]);
}

export async function listNcrsForPackages(parentPackageIds: string[]): Promise<OdakNcrRow[]> {
  const filter = buildNcrByParentPackagesFilter(parentPackageIds);
  if (!filter) return [];
  const packageIdSet = new Set(parentPackageIds.map((id) => id.trim()).filter(Boolean));
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.ncrDataset, {
    filter,
    sort: '-__createdAt',
    limit: 500,
  });
  return ((resp.items ?? []) as OdakNcrRow[]).filter((row) =>
    packageIdSet.has(resolveRelationId(row.parentPackageId))
  );
}

export async function createOdakNcr(
  form: OdakNcrFormModel,
  packageId?: string
): Promise<string | null> {
  const body = formModelToNcrPayload(form, packageId);
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.ncrDataset, body);
  return ncrDataId(created as Record<string, unknown>) || null;
}

export async function updateOdakNcr(
  ncrId: string,
  form: OdakNcrFormModel,
  packageId?: string
): Promise<void> {
  const body = formModelToNcrPayload(form, packageId);
  await ocUpdate(ODAK_SIPARIS_CONFIG.ncrDataset, ncrId, body);
}

export function countOpenNcrs(items: OdakNcrRow[]): number {
  return items.filter((x) => normalizeNcrStatus(x.ncStatus) !== 'Kapalı').length;
}

export function formatNcrDate(v: unknown): string {
  return formatOdakDate(v);
}

export function ncrDisplayNo(row: OdakNcrRow | Record<string, unknown>): string {
  const r = row as OdakNcrRow;
  if (r.legacyNcNo?.trim()) return r.legacyNcNo.trim();
  if (r.ncrNo?.trim()) return r.ncrNo.trim();
  return ncrDataId(row) || '—';
}
