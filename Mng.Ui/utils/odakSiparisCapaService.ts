import { fetchFromDataGateway } from '@/services/apiService';
import { ocCreate, ocListDatasetPage, ocUpdate } from '@/services/operationCoreService';
import {
  ODAK_CAPA_STATUS_OPTIONS,
  ODAK_SIPARIS_CONFIG,
  type OdakCapaRow,
} from '@/utils/odakSiparisConfig';
import { formatOdakDate, packageDataId } from '@/utils/odakSiparisService';
import {
  fromDateInputValue,
  ncrDisplayNo,
  toDateInputValue,
} from '@/utils/odakSiparisNcrService';

export type OdakCapaDialogMode = 'view' | 'create' | 'edit';

function resolveRelationId(raw: unknown): string {
  if (raw == null) return '';
  if (typeof raw === 'string') return raw.trim();
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    return String(o.__dataId ?? o.dataId ?? o.id ?? '').trim();
  }
  return String(raw).trim();
}

export function capaDataId(row: OdakCapaRow | Record<string, unknown>): string {
  return packageDataId(row);
}

export function buildCapaByParentPackageFilter(parentPackageId: string): string {
  return `parentPackageId:eq:${parentPackageId}`;
}

export function capaBelongsToPackage(row: Record<string, unknown>, packageId: string): boolean {
  if (!packageId) return false;
  return resolveRelationId(row.parentPackageId) === packageId;
}

export function capaStatusLabel(value: unknown): string {
  if (value == null || value === '') return '—';
  const key = String(value);
  return ODAK_CAPA_STATUS_OPTIONS.find((o) => o.value === key)?.title ?? key;
}

export function ncrIdFromRow(raw: unknown): string {
  if (raw == null) return '';
  if (typeof raw === 'string') return raw;
  if (typeof raw === 'object') return resolveRelationId(raw);
  return String(raw);
}

export function ncrLabelFromRow(raw: unknown): string {
  if (raw == null) return '—';
  if (typeof raw === 'object') return ncrDisplayNo(raw as Record<string, unknown>);
  return String(raw) || '—';
}

export interface OdakCapaFormModel {
  capaStatus: string;
  cpaDate: string;
  source: string;
  requestDivision: string;
  description: string;
  parentNcrId: string | null;
  nonconformity: string;
  tecnique: string;
  errorCode: string;
  rootCause: string;
  correctiveAction: string;
  preventiveAction: string;
  closedDate: string;
  notes: string;
}

export function emptyCapaFormModel(partial?: Partial<OdakCapaFormModel>): OdakCapaFormModel {
  return {
    capaStatus: partial?.capaStatus ?? 'Acik',
    cpaDate: partial?.cpaDate ?? '',
    source: partial?.source ?? '',
    requestDivision: partial?.requestDivision ?? '',
    description: partial?.description ?? '',
    parentNcrId: partial?.parentNcrId ?? null,
    nonconformity: partial?.nonconformity ?? '',
    tecnique: partial?.tecnique ?? '',
    errorCode: partial?.errorCode ?? '',
    rootCause: partial?.rootCause ?? '',
    correctiveAction: partial?.correctiveAction ?? '',
    preventiveAction: partial?.preventiveAction ?? '',
    closedDate: partial?.closedDate ?? '',
    notes: partial?.notes ?? '',
  };
}

export function capaRowToFormModel(row: OdakCapaRow): OdakCapaFormModel {
  return emptyCapaFormModel({
    capaStatus: row.capaStatus ?? 'Acik',
    cpaDate: toDateInputValue(row.cpaDate),
    source: row.source ?? '',
    requestDivision: row.requestDivision ?? '',
    description: row.description ?? '',
    parentNcrId: ncrIdFromRow(row.parentNcrId) || null,
    nonconformity: row.nonconformity ?? '',
    tecnique: row.tecnique ?? '',
    errorCode: row.errorCode ?? '',
    rootCause: row.rootCause ?? '',
    correctiveAction: row.correctiveAction ?? '',
    preventiveAction: row.preventiveAction ?? '',
    closedDate: toDateInputValue(row.closedDate),
    notes: row.notes ?? '',
  });
}

export function formModelToCapaPayload(
  form: OdakCapaFormModel,
  packageId: string
): Record<string, unknown> {
  return {
    parentPackageId: packageId,
    capaStatus: form.capaStatus || 'Acik',
    cpaDate: fromDateInputValue(form.cpaDate),
    source: form.source.trim() || null,
    requestDivision: form.requestDivision.trim() || null,
    description: form.description.trim(),
    parentNcrId: form.parentNcrId || null,
    nonconformity: form.nonconformity.trim() || null,
    tecnique: form.tecnique.trim() || null,
    errorCode: form.errorCode.trim() || null,
    rootCause: form.rootCause.trim() || null,
    correctiveAction: form.correctiveAction.trim() || null,
    preventiveAction: form.preventiveAction.trim() || null,
    closedDate: fromDateInputValue(form.closedDate),
    notes: form.notes.trim() || null,
  };
}

export async function fetchOdakCapaById(capaId: string): Promise<OdakCapaRow | null> {
  if (!capaId) return null;
  const url = `/api/v1/data/${encodeURIComponent(ODAK_SIPARIS_CONFIG.capaDataset)}/${encodeURIComponent(capaId)}?expand=true`;
  const raw = await fetchFromDataGateway(url, 'GET');
  const item = Array.isArray(raw) ? raw[0] : raw;
  if (!item || typeof item !== 'object') return null;
  return item as OdakCapaRow;
}

export async function listCapasForPackage(packageId: string): Promise<OdakCapaRow[]> {
  if (!packageId) return [];
  const filter = buildCapaByParentPackageFilter(packageId);
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.capaDataset, {
    filter,
    sort: '-__createdAt',
    limit: 500,
  });
  return ((resp.items ?? []) as OdakCapaRow[]).filter((row) =>
    capaBelongsToPackage(row as Record<string, unknown>, packageId)
  );
}

export async function createOdakCapa(
  packageId: string,
  form: OdakCapaFormModel
): Promise<string | null> {
  const body = formModelToCapaPayload(form, packageId);
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.capaDataset, body);
  return capaDataId(created as Record<string, unknown>) || null;
}

export async function updateOdakCapa(
  capaId: string,
  packageId: string,
  form: OdakCapaFormModel
): Promise<void> {
  const body = formModelToCapaPayload(form, packageId);
  await ocUpdate(ODAK_SIPARIS_CONFIG.capaDataset, capaId, body);
}

export function countOpenCapas(items: OdakCapaRow[]): number {
  return items.filter((x) => x.capaStatus !== 'Kapali').length;
}

export function formatCapaDate(v: unknown): string {
  return formatOdakDate(v);
}

export function capaDisplayNo(row: OdakCapaRow | Record<string, unknown>): string {
  const r = row as OdakCapaRow;
  if (r.legacyCapaNo?.trim()) return r.legacyCapaNo.trim();
  if (r.capaNo?.trim()) return r.capaNo.trim();
  return capaDataId(row) || '—';
}
