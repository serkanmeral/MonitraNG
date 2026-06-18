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
import {
  filterLinePayloadByFieldAccess,
  lineRecordForPolicyEval,
} from '@/utils/odakSiparisFieldPolicies';
import { loadOdakLineFieldPoliciesOnly } from '@/utils/odakSiparisHubSettingsService';
import { fromDateInputValue, toDateInputValue } from '@/utils/odakSiparisDateUtils';
import {
  computeIsFaiFromQualityReqs,
  qualityRequirementIdsFromRow,
} from '@/utils/odakSiparisCustomerQualityReqService';

export type OdakLineDialogMode = 'view' | 'create' | 'edit';

export function lineDataId(row: OdakLineRow | Record<string, unknown>): string {
  return packageDataId(row);
}

/** Combobox / select — ayni aciklama tekrar eden kalemleri ayirt etmek icin. */
export function formatLineSelectLabel(line: OdakLineRow | Record<string, unknown>): string {
  const row = line as OdakLineRow;
  const no = row.lineNo ?? '—';
  const desc = row.description?.trim() || '';
  const parts: string[] = [`#${no}`];
  if (row.customerPoItemNo != null && String(row.customerPoItemNo).trim() !== '') {
    parts.push(`PO kalem ${row.customerPoItemNo}`);
  }
  if (row.quantity != null) {
    const unit = (row.unit ?? 'adet').trim() || 'adet';
    parts.push(`${row.quantity} ${unit}`);
  }
  const prefix = parts.join(' · ');
  return desc ? `${prefix} — ${desc}` : prefix;
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

export interface OdakLineFormModel {
  lineNo: number | null;
  customerProjectNo: string;
  customerPoNo: string;
  customerPoItemNo: string;
  sasItemNo: string;
  customerJobNo: string;
  poItemRevNo: string;
  description: string;
  productId: string | null;
  quantity: number | null;
  unit: string;
  shippedQuantity: number | null;
  qualityReqs: string;
  qualityRequirementIds: string[];
  isFai: boolean;
  isFaiComplete: boolean;
  deliveryDate: string;
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
    sasItemNo: partial?.sasItemNo ?? '',
    customerJobNo: partial?.customerJobNo ?? '',
    poItemRevNo: partial?.poItemRevNo ?? '',
    description: partial?.description ?? '',
    productId: partial?.productId ?? null,
    quantity: partial?.quantity ?? 1,
    unit: partial?.unit ?? 'adet',
    shippedQuantity: partial?.shippedQuantity ?? 0,
    qualityReqs: partial?.qualityReqs ?? '',
    qualityRequirementIds: partial?.qualityRequirementIds ?? [],
    isFai: partial?.isFai ?? false,
    isFaiComplete: partial?.isFaiComplete ?? false,
    deliveryDate: partial?.deliveryDate ?? '',
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
    sasItemNo: row.sasItemNo ?? '',
    customerJobNo: row.customerJobNo ?? '',
    poItemRevNo: row.poItemRevNo ?? '',
    description: row.description ?? '',
    productId: productIdFromRow(row.productId) || null,
    quantity: row.quantity ?? null,
    unit: row.unit ?? 'adet',
    shippedQuantity: row.shippedQuantity ?? 0,
    qualityReqs: row.qualityReqs ?? '',
    qualityRequirementIds: qualityRequirementIdsFromRow(row.qualityRequirementIds),
    isFai: Boolean(row.isFai),
    isFaiComplete: Boolean(row.isFaiComplete),
    deliveryDate: toDateInputValue(row.deliveryDate),
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
    sasItemNo: form.sasItemNo.trim() || null,
    customerJobNo: form.customerJobNo.trim() || null,
    poItemRevNo: form.poItemRevNo.trim() || null,
    description: form.description.trim(),
    productId: form.productId || null,
    quantity: form.quantity,
    unit: form.unit || 'adet',
    qualityReqs: form.qualityReqs.trim() || null,
    qualityRequirementIds: form.qualityRequirementIds.length ? form.qualityRequirementIds : null,
    isFai: form.isFai,
    isFaiComplete: form.isFaiComplete,
    deliveryDate: fromDateInputValue(form.deliveryDate),
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

async function applyLineFieldPolicyToPayload(
  body: Record<string, unknown>,
  existingRow?: OdakLineRow | null
): Promise<Record<string, unknown>> {
  let blob = { policiesByField: {} };
  try {
    blob = await loadOdakLineFieldPoliciesOnly();
  } catch {
    /* open defaults */
  }
  const auth = useAuthStore();
  const record = existingRow
    ? lineRecordForPolicyEval(existingRow)
    : lineRecordForPolicyEval(body as OdakLineRow);
  return filterLinePayloadByFieldAccess(body, auth.userGroups, record, blob);
}

export async function createOdakLine(
  packageId: string,
  form: OdakLineFormModel
): Promise<string | null> {
  syncLineTotalCost(form);
  let body = formModelToPayload(form, packageId);
  body = await applyLineFieldPolicyToPayload(body);
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.linesDataset, body);
  const o = created as Record<string, unknown>;
  return packageDataId(o) || null;
}

export async function updateOdakLine(
  lineId: string,
  packageId: string,
  form: OdakLineFormModel,
  existingRow?: OdakLineRow | null
): Promise<void> {
  syncLineTotalCost(form);
  let body = formModelToPayload(form, packageId);
  body = await applyLineFieldPolicyToPayload(body, existingRow ?? null);
  await ocUpdate(ODAK_SIPARIS_CONFIG.linesDataset, lineId, body);
}

export async function listLinesForPackage(packageId: string): Promise<OdakLineRow[]> {
  const filter = buildLinesByParentPackageFilter(packageId);
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.linesDataset, {
    filter,
    sort: 'lineNo:asc',
    limit: 500,
    expand: true,
  });
  return ((resp.items ?? []) as OdakLineRow[]).filter((row) =>
    lineBelongsToPackage(row as Record<string, unknown>, packageId)
  );
}
