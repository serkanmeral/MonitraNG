import { fetchFromDataGateway } from '@/services/apiService';
import { ocCreate, ocListDatasetPage, ocUpdate } from '@/services/operationCoreService';
import {
  ODAK_FAI_STATUS_OPTIONS,
  ODAK_NCR_STATUS_OPTIONS,
  ODAK_SIPARIS_CONFIG,
  type OdakNcrRow,
} from '@/utils/odakSiparisConfig';
import { formatOdakDate, packageDataId } from '@/utils/odakSiparisService';

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

export function toDateInputValue(v: unknown): string {
  if (!v) return '';
  try {
    const d = new Date(String(v));
    if (Number.isNaN(d.getTime())) return '';
    return d.toISOString().slice(0, 10);
  } catch {
    return '';
  }
}

export function fromDateInputValue(v: string): string | null {
  const trimmed = v.trim();
  if (!trimmed) return null;
  return `${trimmed}T21:00:00.0000000Z`;
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
    closureDate: toDateInputValue(row.closureDate),
    notes: row.notes ?? '',
  });
}

export function formModelToNcrPayload(
  form: OdakNcrFormModel,
  packageId: string
): Record<string, unknown> {
  return {
    parentPackageId: packageId,
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
    closureDate: fromDateInputValue(form.closureDate),
    notes: form.notes.trim() || null,
  };
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
  const filter = buildNcrByParentPackageFilter(packageId);
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.ncrDataset, {
    filter,
    sort: '-__createdAt',
    limit: 500,
  });
  return ((resp.items ?? []) as OdakNcrRow[]).filter((row) =>
    ncrBelongsToPackage(row as Record<string, unknown>, packageId)
  );
}

export async function createOdakNcr(
  packageId: string,
  form: OdakNcrFormModel
): Promise<string | null> {
  const body = formModelToNcrPayload(form, packageId);
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.ncrDataset, body);
  return ncrDataId(created as Record<string, unknown>) || null;
}

export async function updateOdakNcr(
  ncrId: string,
  packageId: string,
  form: OdakNcrFormModel
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
