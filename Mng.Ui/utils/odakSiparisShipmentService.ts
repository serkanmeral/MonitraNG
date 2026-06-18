import { fetchFromDataGateway } from '@/services/apiService';
import { ocCreate, ocDelete, ocListDatasetPage, ocUpdate } from '@/services/operationCoreService';
import {
  ODAK_QCF_STATUS_OPTIONS,
  ODAK_SHIPMENT_STATUS_OPTIONS,
  ODAK_SIPARIS_CONFIG,
  type OdakLineRow,
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
import { formatOdakDate, fetchOdakPackageById, packageDataId } from '@/utils/odakSiparisService';

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

export function shipmentLineDataId(row: OdakShipmentLineRow | Record<string, unknown>): string {
  return packageDataId(row);
}

export function buildShipmentsByParentPackageFilter(parentPackageId: string): string {
  return `parentPackageId:eq:${parentPackageId}`;
}

export function buildShipmentLinesByShipmentFilter(shipmentId: string): string {
  return `parentShipmentId:eq:${shipmentId}`;
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
    parentPackageId: packageId,
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

async function loadLineMap(packageId: string): Promise<Map<string, OdakLineRow>> {
  const lines = await listLinesForPackage(packageId);
  const map = new Map<string, OdakLineRow>();
  for (const line of lines) {
    const id = lineDataId(line);
    if (id) map.set(id, line);
  }
  return map;
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
