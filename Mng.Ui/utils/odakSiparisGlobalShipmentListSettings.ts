import type { OdakShipmentRow } from '@/utils/odakSiparisConfig';
import {
  buildFieldToListKeyMap,
  buildHubListHeaders,
  buildListKeyToFieldMap,
  defaultHubListConfigFromCatalog,
  filterHubListConfigToCatalog,
  hubFieldNameFromListSortKey,
  hubListSortKeyFromField,
  mergeHubListConfig,
  parseHubListConfig,
  type OdakHubListConfig,
  type OdakHubListFieldDef,
  type OdakHubListHeader,
} from '@/utils/odakSiparisHubListConfig';
import {
  ODAK_SHIPMENT_LIST_DEFAULT_FORMATS,
  withDefaultListColumnFormats,
} from '@/utils/odakSiparisHubListDefaultFormats';
import { shipmentListCustomerLabel } from '@/utils/odakSiparisService';
import {
  formatShipmentDate,
  qcfStatusLabel,
  recordScopeLabel,
  shipmentDataId,
  shipmentStatusLabel,
} from '@/utils/odakSiparisShipmentService';

export type OdakGlobalShipmentListConfig = OdakHubListConfig;

export const ODAK_GLOBAL_SHIPMENT_LIST_FIELD_CATALOG: OdakHubListFieldDef[] = [
  { fieldName: 'waybillNo', listKey: 'waybillNo', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 1, width: 118 },
  { fieldName: 'recordScope', listKey: 'scopeLabel', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 2, width: 156 },
  { fieldName: 'headerDescription', listKey: 'headerDescription', defaultVisible: true, defaultSortable: false, defaultFilterable: true, defaultOrder: 3, width: 220 },
  { fieldName: 'customerId', listKey: 'customerLabel', defaultVisible: true, defaultSortable: false, defaultFilterable: true, defaultOrder: 4, width: 140 },
  { fieldName: 'shipmentDate', listKey: 'shipmentDate', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 5, width: 112 },
  { fieldName: 'status', listKey: 'status', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 6, width: 118 },
  { fieldName: 'lineQty', listKey: 'lineQty', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 7, width: 88 },
  { fieldName: 'qcfStatus', listKey: 'qcfStatus', defaultVisible: false, defaultSortable: false, defaultFilterable: true, defaultOrder: 8, width: 120 },
  { fieldName: 'controlType', listKey: 'controlType', defaultVisible: false, defaultSortable: false, defaultFilterable: true, defaultOrder: 9, width: 120 },
  { fieldName: 'shipmentAddress', listKey: 'shipmentAddress', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 10 },
  { fieldName: 'notes', listKey: 'notes', defaultVisible: false, defaultSortable: false, defaultFilterable: true, defaultOrder: 11 },
  { fieldName: 'qcfReferenceNo', listKey: 'qcfReferenceNo', defaultVisible: false, defaultSortable: false, defaultFilterable: true, defaultOrder: 12, width: 120 },
  { fieldName: 'qcfNotes', listKey: 'qcfNotes', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 13 },
  { fieldName: 'parentPackageId', listKey: 'parentPackageId', defaultVisible: false, defaultSortable: false, defaultFilterable: true, defaultOrder: 14, width: 140 },
];

export const ODAK_GLOBAL_SHIPMENT_LIST_FIELD_TO_KEY = buildFieldToListKeyMap(
  ODAK_GLOBAL_SHIPMENT_LIST_FIELD_CATALOG
);
export const ODAK_GLOBAL_SHIPMENT_LIST_KEY_TO_FIELD = buildListKeyToFieldMap(
  ODAK_GLOBAL_SHIPMENT_LIST_FIELD_CATALOG
);

/** DG sort param — yalnizca sunucu alanlari. */
export const GLOBAL_SHIPMENT_LIST_SORT_FIELD_MAP: Record<string, string> = {
  waybillNo: 'waybillNo',
  recordScope: 'recordScope',
  headerDescription: 'headerDescription',
  shipmentDate: 'shipmentDate',
  status: 'status',
  controlType: 'controlType',
  qcfStatus: 'qcfStatus',
  shipmentAddress: 'shipmentAddress',
  notes: 'notes',
  qcfReferenceNo: 'qcfReferenceNo',
  qcfNotes: 'qcfNotes',
};

function baseOdakGlobalShipmentListConfig(): OdakGlobalShipmentListConfig {
  return defaultHubListConfigFromCatalog(ODAK_GLOBAL_SHIPMENT_LIST_FIELD_CATALOG, {
    enableSearch: true,
    defaultSortBy: 'shipmentDate',
    defaultSortOrder: 'desc',
  });
}

export function defaultOdakGlobalShipmentListConfig(): OdakGlobalShipmentListConfig {
  return withDefaultListColumnFormats(baseOdakGlobalShipmentListConfig(), {
    ...ODAK_SHIPMENT_LIST_DEFAULT_FORMATS,
  });
}

export function parseOdakGlobalShipmentListConfig(raw: unknown): OdakGlobalShipmentListConfig {
  return parseHubListConfig(raw, defaultOdakGlobalShipmentListConfig());
}

export function mergeOdakGlobalShipmentListConfig(saved: unknown): OdakGlobalShipmentListConfig {
  const merged = mergeHubListConfig(saved, baseOdakGlobalShipmentListConfig());
  const filtered = filterHubListConfigToCatalog(merged, ODAK_GLOBAL_SHIPMENT_LIST_FIELD_CATALOG);
  return withDefaultListColumnFormats(filtered, ODAK_SHIPMENT_LIST_DEFAULT_FORMATS);
}

export function buildGlobalShipmentListHeaders(
  listConfig: OdakGlobalShipmentListConfig,
  columnTitle: (fieldName: string, listKey: string) => string,
  canViewColumn?: (listKey: string) => boolean
): OdakHubListHeader[] {
  const headers = buildHubListHeaders(
    listConfig,
    ODAK_GLOBAL_SHIPMENT_LIST_FIELD_TO_KEY,
    columnTitle,
    canViewColumn
  );
  for (const h of headers) {
    if (h.key === 'headerDescription') h.minWidth = 220;
  }
  return headers;
}

export function listSortKeyFromGlobalShipmentField(fieldName: string): string {
  return hubListSortKeyFromField(fieldName, ODAK_GLOBAL_SHIPMENT_LIST_FIELD_TO_KEY);
}

export function fieldNameFromGlobalShipmentListKey(sortKey: string): string {
  return hubFieldNameFromListSortKey(sortKey, ODAK_GLOBAL_SHIPMENT_LIST_KEY_TO_FIELD);
}

export type GlobalShipmentListSort = { key: string; order: 'asc' | 'desc' };

export function buildGlobalShipmentListSort(sortBy: GlobalShipmentListSort[]): string {
  const first = sortBy[0];
  if (!first) return '-shipmentDate';
  const fieldName = fieldNameFromGlobalShipmentListKey(first.key);
  const dgField = GLOBAL_SHIPMENT_LIST_SORT_FIELD_MAP[fieldName];
  if (!dgField) return '-shipmentDate';
  return first.order === 'desc' ? `-${dgField}` : dgField;
}

export interface GlobalShipmentListCellContext {
  lineQtyByShipmentId?: Map<string, number>;
  customerLabels?: Record<string, string>;
  packageCustomerLabels?: Record<string, string>;
  contentSummaryMax?: number;
}

function truncateContent(text: string | null | undefined, maxLength: number): string {
  const value = String(text ?? '').trim();
  if (!value) return '—';
  if (value.length <= maxLength) return value;
  return `${value.slice(0, maxLength)}…`;
}

export function globalShipmentListCellRaw(
  row: OdakShipmentRow,
  listKey: string,
  ctx?: GlobalShipmentListCellContext
): string {
  switch (listKey) {
    case 'waybillNo':
      return String(row.waybillNo ?? '').trim() || '—';
    case 'scopeLabel':
      return recordScopeLabel(row.recordScope);
    case 'headerDescription':
      return truncateContent(row.headerDescription || row.notes, ctx?.contentSummaryMax ?? 80);
    case 'customerLabel':
      return shipmentListCustomerLabel(
        row,
        ctx?.customerLabels ?? {},
        ctx?.packageCustomerLabels ?? {}
      );
    case 'shipmentDate':
      return formatShipmentDate(row.shipmentDate);
    case 'status':
      return shipmentStatusLabel(row.status);
    case 'lineQty': {
      const id = shipmentDataId(row);
      const qty = id ? ctx?.lineQtyByShipmentId?.get(id) : undefined;
      return qty != null ? String(qty) : '—';
    }
    case 'qcfStatus':
      return qcfStatusLabel(row.qcfStatus);
    case 'controlType':
      return String(row.controlType ?? '').trim() || '—';
    case 'shipmentAddress':
      return String(row.shipmentAddress ?? '').trim() || '—';
    case 'notes':
      return String(row.notes ?? '').trim() || '—';
    case 'qcfReferenceNo':
      return String(row.qcfReferenceNo ?? '').trim() || '—';
    case 'qcfNotes':
      return String(row.qcfNotes ?? '').trim() || '—';
    case 'parentPackageId': {
      const raw = row.parentPackageId;
      if (raw == null) return '—';
      if (typeof raw === 'object') {
        const o = raw as Record<string, unknown>;
        const no = o.packageNo ?? o.PackageNo;
        if (no != null && String(no).trim()) return String(no);
      }
      return '—';
    }
    default:
      return '—';
  }
}

export function globalShipmentContentFull(row: OdakShipmentRow): string {
  return (row.headerDescription || row.notes || '').trim();
}
