import { fetchFromDataGateway } from '@/services/apiService';
import { ocCreate, ocDelete, ocListDatasetPage, ocUpdate } from '@/services/operationCoreService';
import {
  ODAK_SIPARIS_CONFIG,
  type OdakCustomerQualityReqRow,
  type OdakQualityReqTemplateRow,
} from '@/utils/odakSiparisConfig';
import { packageDataId } from '@/utils/odakSiparisService';

export type OdakCustomerQualityReqDialogMode = 'create' | 'edit';

function resolveRelationId(raw: unknown): string {
  if (raw == null) return '';
  if (typeof raw === 'string') return raw.trim();
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    return String(o.__dataId ?? o.dataId ?? o.id ?? '').trim();
  }
  return String(raw).trim();
}

export function qualityReqDataId(row: OdakCustomerQualityReqRow | Record<string, unknown>): string {
  return packageDataId(row);
}

export function buildQualityReqsByParentCustomerFilter(parentCustomerId: string): string {
  return `parentCustomerId:eq:${parentCustomerId}`;
}

export function qualityReqBelongsToCustomer(row: Record<string, unknown>, customerId: string): boolean {
  if (!customerId) return false;
  return resolveRelationId(row.parentCustomerId) === customerId;
}

export function qualityRequirementIdsFromRow(raw: unknown): string[] {
  if (raw == null) return [];
  if (Array.isArray(raw)) {
    return raw.map((item) => resolveRelationId(item)).filter(Boolean);
  }
  const single = resolveRelationId(raw);
  return single ? [single] : [];
}

export function qualityReqRowFromRelationValue(raw: unknown): OdakCustomerQualityReqRow | null {
  if (raw == null || typeof raw !== 'object' || Array.isArray(raw)) return null;
  const o = raw as Record<string, unknown>;
  if (o.kod != null || o.ad != null) return o as OdakCustomerQualityReqRow;
  return null;
}

export function formatQualityReqSelectLabel(row: OdakCustomerQualityReqRow | Record<string, unknown>): string {
  const r = row as OdakCustomerQualityReqRow;
  const kod = r.kod?.trim() || '—';
  const ad = r.ad?.trim() || '';
  return ad ? `${kod} — ${ad}` : kod;
}

export interface OdakCustomerQualityReqFormModel {
  kod: string;
  ad: string;
  aciklama: string;
  faiUygulanacak: boolean;
  aktif: boolean;
}

export function emptyCustomerQualityReqFormModel(
  partial?: Partial<OdakCustomerQualityReqFormModel>
): OdakCustomerQualityReqFormModel {
  return {
    kod: partial?.kod ?? '',
    ad: partial?.ad ?? '',
    aciklama: partial?.aciklama ?? '',
    faiUygulanacak: partial?.faiUygulanacak ?? false,
    aktif: partial?.aktif ?? true,
  };
}

export function qualityReqRowToFormModel(row: OdakCustomerQualityReqRow): OdakCustomerQualityReqFormModel {
  return emptyCustomerQualityReqFormModel({
    kod: row.kod ?? '',
    ad: row.ad ?? '',
    aciklama: row.aciklama ?? '',
    faiUygulanacak: Boolean(row.faiUygulanacak),
    aktif: row.aktif !== false,
  });
}

export function formModelToQualityReqPayload(
  form: OdakCustomerQualityReqFormModel,
  customerId: string
): Record<string, unknown> {
  return {
    parentCustomerId: customerId,
    kod: form.kod.trim(),
    ad: form.ad.trim(),
    aciklama: form.aciklama.trim() || null,
    faiUygulanacak: form.faiUygulanacak,
    aktif: form.aktif,
  };
}

export function validateCustomerQualityReqForm(
  form: OdakCustomerQualityReqFormModel,
  existingRows: OdakCustomerQualityReqRow[],
  editingId?: string
): string | null {
  if (!form.kod.trim()) return 'kodRequired';
  if (!form.ad.trim()) return 'adRequired';
  const kodNorm = form.kod.trim().toLowerCase();
  const duplicate = existingRows.some((row) => {
    const id = qualityReqDataId(row);
    if (editingId && id === editingId) return false;
    return (row.kod ?? '').trim().toLowerCase() === kodNorm;
  });
  if (duplicate) return 'kodDuplicate';
  return null;
}

export function computeIsFaiFromQualityReqs(rows: OdakCustomerQualityReqRow[]): boolean {
  return rows.some((r) => Boolean(r.faiUygulanacak));
}

export function formatQualityRequirementsSummary(line: {
  qualityRequirementIds?: unknown;
}): string {
  const ids = qualityRequirementIdsFromRow(line.qualityRequirementIds);
  if (!ids.length) return '';

  const raw = line.qualityRequirementIds;
  if (Array.isArray(raw)) {
    const labels: string[] = [];
    for (const item of raw) {
      const expanded = qualityReqRowFromRelationValue(item);
      if (expanded) {
        labels.push(formatQualityReqSelectLabel(expanded));
      } else {
        const id = resolveRelationId(item);
        if (id) labels.push(id);
      }
    }
    if (labels.length) return labels.join(', ');
  }

  return ids.join(', ');
}

export async function fetchOdakCustomerQualityReqById(
  reqId: string
): Promise<OdakCustomerQualityReqRow | null> {
  if (!reqId) return null;
  const url = `/api/v1/data/${encodeURIComponent(ODAK_SIPARIS_CONFIG.customerQualityReqsDataset)}/${encodeURIComponent(reqId)}?expand=true`;
  const raw = await fetchFromDataGateway(url, 'GET');
  const item = Array.isArray(raw) ? raw[0] : raw;
  if (!item || typeof item !== 'object') return null;
  return item as OdakCustomerQualityReqRow;
}

export async function listQualityReqsForCustomer(
  customerId: string,
  options?: { activeOnly?: boolean }
): Promise<OdakCustomerQualityReqRow[]> {
  if (!customerId) return [];
  const filter = buildQualityReqsByParentCustomerFilter(customerId);
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.customerQualityReqsDataset, {
    filter,
    sort: 'kod:asc',
    limit: 500,
    expand: true,
  });
  let rows = ((resp.items ?? []) as OdakCustomerQualityReqRow[]).filter((row) =>
    qualityReqBelongsToCustomer(row as Record<string, unknown>, customerId)
  );
  if (options?.activeOnly) {
    rows = rows.filter((r) => r.aktif !== false);
  }
  return rows;
}

export async function listQualityReqTemplates(options?: {
  sektor?: string | null;
  activeOnly?: boolean;
}): Promise<OdakQualityReqTemplateRow[]> {
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.qualityReqTemplatesDataset, {
    sort: 'sira:asc,kod:asc',
    limit: 500,
  });
  let rows = (resp.items ?? []) as OdakQualityReqTemplateRow[];
  if (options?.activeOnly !== false) {
    rows = rows.filter((r) => r.aktif !== false);
  }
  const sektor = options?.sektor?.trim();
  if (sektor && sektor !== 'diger') {
    rows = rows.filter((r) => !r.sektor || r.sektor === 'genel' || r.sektor === sektor);
  }
  return rows;
}

export async function resolveLineQualityRequirements(
  line: Pick<OdakCustomerQualityReqRow, never> & {
    qualityRequirementIds?: unknown;
  },
  customerId?: string
): Promise<OdakCustomerQualityReqRow[]> {
  const ids = qualityRequirementIdsFromRow(line.qualityRequirementIds);
  if (!ids.length) return [];

  const fromExpand: OdakCustomerQualityReqRow[] = [];
  const raw = line.qualityRequirementIds;
  if (Array.isArray(raw)) {
    for (const item of raw) {
      const expanded = qualityReqRowFromRelationValue(item);
      if (expanded) fromExpand.push(expanded);
    }
  }

  if (fromExpand.length === ids.length) {
    const byId = new Map(fromExpand.map((r) => [qualityReqDataId(r), r]));
    return ids.map((id) => byId.get(id)).filter((r): r is OdakCustomerQualityReqRow => !!r);
  }

  if (!customerId) {
    const fetched = await Promise.all(ids.map((id) => fetchOdakCustomerQualityReqById(id)));
    return fetched.filter((r): r is OdakCustomerQualityReqRow => !!r);
  }

  const allForCustomer = await listQualityReqsForCustomer(customerId, { activeOnly: false });
  const byId = new Map(allForCustomer.map((r) => [qualityReqDataId(r), r]));
  return ids.map((id) => byId.get(id)).filter((r): r is OdakCustomerQualityReqRow => !!r);
}

export async function createOdakCustomerQualityReq(
  customerId: string,
  form: OdakCustomerQualityReqFormModel
): Promise<string | null> {
  const body = formModelToQualityReqPayload(form, customerId);
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.customerQualityReqsDataset, body);
  return qualityReqDataId(created as Record<string, unknown>) || null;
}

export async function updateOdakCustomerQualityReq(
  reqId: string,
  customerId: string,
  form: OdakCustomerQualityReqFormModel
): Promise<void> {
  const body = formModelToQualityReqPayload(form, customerId);
  await ocUpdate(ODAK_SIPARIS_CONFIG.customerQualityReqsDataset, reqId, body);
}

export async function deleteOdakCustomerQualityReq(reqId: string): Promise<void> {
  if (!reqId) return;
  await ocDelete(ODAK_SIPARIS_CONFIG.customerQualityReqsDataset, reqId);
}

export async function copyQualityReqTemplatesToCustomer(
  customerId: string,
  templates: OdakQualityReqTemplateRow[],
  existingRows?: OdakCustomerQualityReqRow[]
): Promise<number> {
  if (!customerId || !templates.length) return 0;
  const existing = existingRows ?? (await listQualityReqsForCustomer(customerId));
  const existingKods = new Set(existing.map((r) => (r.kod ?? '').trim().toLowerCase()));
  let created = 0;
  for (const tpl of templates) {
    const kod = (tpl.kod ?? '').trim();
    if (!kod || existingKods.has(kod.toLowerCase())) continue;
    await createOdakCustomerQualityReq(
      customerId,
      emptyCustomerQualityReqFormModel({
        kod: tpl.kod ?? '',
        ad: tpl.ad ?? '',
        aciklama: tpl.aciklama ?? '',
        faiUygulanacak: Boolean(tpl.faiUygulanacak),
        aktif: true,
      })
    );
    existingKods.add(kod.toLowerCase());
    created += 1;
  }
  return created;
}

export async function copyQualityReqsFromCustomer(
  targetCustomerId: string,
  sourceCustomerId: string
): Promise<number> {
  if (!targetCustomerId || !sourceCustomerId || targetCustomerId === sourceCustomerId) return 0;
  const [sourceRows, targetRows] = await Promise.all([
    listQualityReqsForCustomer(sourceCustomerId, { activeOnly: true }),
    listQualityReqsForCustomer(targetCustomerId),
  ]);
  const existingKods = new Set(targetRows.map((r) => (r.kod ?? '').trim().toLowerCase()));
  let created = 0;
  for (const src of sourceRows) {
    const kod = (src.kod ?? '').trim();
    if (!kod || existingKods.has(kod.toLowerCase())) continue;
    await createOdakCustomerQualityReq(
      targetCustomerId,
      qualityReqRowToFormModel(src)
    );
    existingKods.add(kod.toLowerCase());
    created += 1;
  }
  return created;
}
