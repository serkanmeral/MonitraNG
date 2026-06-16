import { fetchFromDataGateway } from '@/services/apiService';
import { ocCreate, ocListDatasetPage, ocUpdate } from '@/services/operationCoreService';
import {
  ODAK_SIPARIS_CONFIG,
  type OdakLineRow,
} from '@/utils/odakSiparisConfig';
import {
  buildLinesByParentPackageFilter,
  lineBelongsToPackage,
  packageDataId,
} from '@/utils/odakSiparisService';

export type OdakLineDialogMode = 'view' | 'create' | 'edit';

export function lineDataId(row: OdakLineRow | Record<string, unknown>): string {
  return packageDataId(row);
}

export function productLabelFromRow(raw: unknown): string {
  if (raw == null) return '—';
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    const part = o.partNumber != null ? String(o.partNumber) : '';
    const ad = o.ad != null ? String(o.ad) : '';
    if (part && ad) return `${part} — ${ad}`;
    if (ad) return ad;
    if (part) return part;
    const id = packageDataId(o);
    return id || '—';
  }
  return String(raw) || '—';
}

export function productIdFromRow(raw: unknown): string {
  if (raw == null) return '';
  if (typeof raw === 'string') return raw;
  if (typeof raw === 'object') return packageDataId(raw as Record<string, unknown>);
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

export interface OdakLineFormModel {
  lineNo: number | null;
  customerProjectNo: string;
  customerPoNo: string;
  customerPoItemNo: string;
  customerJobNo: string;
  poItemRevNo: string;
  description: string;
  productId: string | null;
  quantity: number | null;
  unit: string;
  shippedQuantity: number | null;
  qualityReqs: string;
  isFai: boolean;
  isFaiComplete: boolean;
  shipmentDate: string;
  shipmentAddress: string;
  unitCost: number | null;
  totalCost: number | null;
  currency: string;
}

export function emptyLineFormModel(partial?: Partial<OdakLineFormModel>): OdakLineFormModel {
  return {
    lineNo: partial?.lineNo ?? null,
    customerProjectNo: partial?.customerProjectNo ?? '',
    customerPoNo: partial?.customerPoNo ?? '',
    customerPoItemNo: partial?.customerPoItemNo ?? '',
    customerJobNo: partial?.customerJobNo ?? '',
    poItemRevNo: partial?.poItemRevNo ?? '',
    description: partial?.description ?? '',
    productId: partial?.productId ?? null,
    quantity: partial?.quantity ?? 1,
    unit: partial?.unit ?? 'adet',
    shippedQuantity: partial?.shippedQuantity ?? 0,
    qualityReqs: partial?.qualityReqs ?? '',
    isFai: partial?.isFai ?? false,
    isFaiComplete: partial?.isFaiComplete ?? false,
    shipmentDate: partial?.shipmentDate ?? '',
    shipmentAddress: partial?.shipmentAddress ?? '',
    unitCost: partial?.unitCost ?? null,
    totalCost: partial?.totalCost ?? null,
    currency: partial?.currency ?? 'TRY',
  };
}

export function lineRowToFormModel(row: OdakLineRow): OdakLineFormModel {
  return emptyLineFormModel({
    lineNo: row.lineNo ?? null,
    customerProjectNo: row.customerProjectNo ?? '',
    customerPoNo: row.customerPoNo ?? '',
    customerPoItemNo: row.customerPoItemNo != null ? String(row.customerPoItemNo) : '',
    customerJobNo: row.customerJobNo ?? '',
    poItemRevNo: row.poItemRevNo ?? '',
    description: row.description ?? '',
    productId: productIdFromRow(row.productId) || null,
    quantity: row.quantity ?? null,
    unit: row.unit ?? 'adet',
    shippedQuantity: row.shippedQuantity ?? 0,
    qualityReqs: row.qualityReqs ?? '',
    isFai: Boolean(row.isFai),
    isFaiComplete: Boolean(row.isFaiComplete),
    shipmentDate: toDateInputValue(row.shipmentDate),
    shipmentAddress: row.shipmentAddress ?? '',
    unitCost: row.unitCost ?? null,
    totalCost: row.totalCost ?? null,
    currency: row.currency ?? 'TRY',
  });
}

export function formModelToPayload(
  form: OdakLineFormModel,
  packageId: string
): Record<string, unknown> {
  const poItem = form.customerPoItemNo.trim();
  const payload: Record<string, unknown> = {
    parentPackageId: packageId,
    parentWorkItemId: packageId,
    lineNo: form.lineNo,
    customerProjectNo: form.customerProjectNo.trim() || null,
    customerPoNo: form.customerPoNo.trim() || null,
    customerPoItemNo: poItem ? Number(poItem) : null,
    customerJobNo: form.customerJobNo.trim() || null,
    poItemRevNo: form.poItemRevNo.trim() || null,
    description: form.description.trim(),
    productId: form.productId || null,
    quantity: form.quantity,
    unit: form.unit || 'adet',
    qualityReqs: form.qualityReqs.trim() || null,
    isFai: form.isFai,
    isFaiComplete: form.isFaiComplete,
    shipmentDate: fromDateInputValue(form.shipmentDate),
    shipmentAddress: form.shipmentAddress.trim() || null,
    unitCost: form.unitCost,
    totalCost: form.totalCost,
    currency: form.currency || null,
  };
  return payload;
}

export function syncLineTotalCost(form: OdakLineFormModel): void {
  if (form.quantity != null && form.unitCost != null) {
    form.totalCost = Math.round(form.quantity * form.unitCost * 100) / 100;
  }
}

export async function fetchOdakLineById(lineId: string): Promise<OdakLineRow | null> {
  if (!lineId) return null;
  const url = `/api/v1/data/${encodeURIComponent(ODAK_SIPARIS_CONFIG.linesDataset)}/${encodeURIComponent(lineId)}?expand=true`;
  const raw = await fetchFromDataGateway(url, 'GET');
  const item = Array.isArray(raw) ? raw[0] : raw;
  if (!item || typeof item !== 'object') return null;
  return item as OdakLineRow;
}

export async function fetchNextLineNo(packageId: string): Promise<number> {
  if (!packageId) return 1;
  const filter = buildLinesByParentPackageFilter(packageId);
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.linesDataset, {
    filter,
    sort: '-lineNo',
    limit: 1,
  });
  const row = (resp.items?.[0] ?? null) as OdakLineRow | null;
  const max = row?.lineNo ?? 0;
  return max > 0 ? max + 1 : 1;
}

export async function searchOdakProducts(query: string): Promise<{ value: string; title: string }[]> {
  const q = query.trim();
  if (q.length < 2) return [];
  const resp = await ocListDatasetPage('odak_urunler', {
    search: q,
    limit: 25,
    sort: 'partNumber:asc',
  });
  return (resp.items ?? [])
    .map((row) => {
      const o = row as Record<string, unknown>;
      const id = packageDataId(o);
      if (!id) return null;
      return { value: id, title: productLabelFromRow(o) };
    })
    .filter((x): x is { value: string; title: string } => !!x);
}

export async function createOdakLine(
  packageId: string,
  form: OdakLineFormModel
): Promise<string | null> {
  syncLineTotalCost(form);
  const body = formModelToPayload(form, packageId);
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.linesDataset, body);
  const o = created as Record<string, unknown>;
  return packageDataId(o) || null;
}

export async function updateOdakLine(
  lineId: string,
  packageId: string,
  form: OdakLineFormModel
): Promise<void> {
  syncLineTotalCost(form);
  const body = formModelToPayload(form, packageId);
  await ocUpdate(ODAK_SIPARIS_CONFIG.linesDataset, lineId, body);
}

export async function listLinesForPackage(packageId: string): Promise<OdakLineRow[]> {
  const filter = buildLinesByParentPackageFilter(packageId);
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.linesDataset, {
    filter,
    sort: 'lineNo:asc',
    limit: 500,
  });
  return ((resp.items ?? []) as OdakLineRow[]).filter((row) =>
    lineBelongsToPackage(row as Record<string, unknown>, packageId)
  );
}
