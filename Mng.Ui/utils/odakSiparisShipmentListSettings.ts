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
import {
  formatShipmentDate,
  qcfStatusLabel,
  shipmentDataId,
  shipmentStatusLabel,
} from '@/utils/odakSiparisShipmentService';

export type OdakShipmentListColumnConfig = import('@/utils/odakSiparisHubListConfig').OdakHubListColumnConfig;
export type OdakShipmentListConfig = OdakHubListConfig;

export const ODAK_SHIPMENT_LIST_FIELD_CATALOG: OdakHubListFieldDef[] = [
  { fieldName: 'waybillNo', listKey: 'waybillNo', defaultVisible: true, defaultSortable: true, defaultFilterable: false, defaultOrder: 1, width: 140 },
  { fieldName: 'shipmentDate', listKey: 'shipmentDate', defaultVisible: true, defaultSortable: true, defaultFilterable: false, defaultOrder: 2, width: 110 },
  { fieldName: 'status', listKey: 'status', defaultVisible: true, defaultSortable: true, defaultFilterable: false, defaultOrder: 3, width: 120 },
  { fieldName: 'lineQty', listKey: 'lineQty', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 4, width: 100 },
  { fieldName: 'qcfStatus', listKey: 'qcfStatus', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 5, width: 120 },
  { fieldName: 'controlType', listKey: 'controlType', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 6, width: 120 },
  { fieldName: 'shipmentAddress', listKey: 'shipmentAddress', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 7 },
  { fieldName: 'notes', listKey: 'notes', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 8 },
  { fieldName: 'qcfReferenceNo', listKey: 'qcfReferenceNo', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 9, width: 120 },
  { fieldName: 'qcfNotes', listKey: 'qcfNotes', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 10 },
];

export const ODAK_SHIPMENT_LIST_FIELD_TO_KEY = buildFieldToListKeyMap(ODAK_SHIPMENT_LIST_FIELD_CATALOG);
export const ODAK_SHIPMENT_LIST_KEY_TO_FIELD = buildListKeyToFieldMap(ODAK_SHIPMENT_LIST_FIELD_CATALOG);

function baseOdakShipmentListConfig(): OdakShipmentListConfig {
  return defaultHubListConfigFromCatalog(ODAK_SHIPMENT_LIST_FIELD_CATALOG, {
    defaultSortBy: 'shipmentDate',
    defaultSortOrder: 'desc',
  });
}

export function defaultOdakShipmentListConfig(): OdakShipmentListConfig {
  return withDefaultListColumnFormats(baseOdakShipmentListConfig(), ODAK_SHIPMENT_LIST_DEFAULT_FORMATS);
}

export function parseOdakShipmentListConfig(raw: unknown): OdakShipmentListConfig {
  return parseHubListConfig(raw, defaultOdakShipmentListConfig());
}

export function mergeOdakShipmentListConfig(saved: unknown): OdakShipmentListConfig {
  const merged = mergeHubListConfig(saved, baseOdakShipmentListConfig());
  const filtered = filterHubListConfigToCatalog(merged, ODAK_SHIPMENT_LIST_FIELD_CATALOG);
  return withDefaultListColumnFormats(filtered, ODAK_SHIPMENT_LIST_DEFAULT_FORMATS);
}

export function buildShipmentListHeaders(
  listConfig: OdakShipmentListConfig,
  columnTitle: (fieldName: string, listKey: string) => string,
  canViewColumn?: (listKey: string) => boolean
): OdakHubListHeader[] {
  return buildHubListHeaders(listConfig, ODAK_SHIPMENT_LIST_FIELD_TO_KEY, columnTitle, canViewColumn);
}

export function listSortKeyFromShipmentField(fieldName: string): string {
  return hubListSortKeyFromField(fieldName, ODAK_SHIPMENT_LIST_FIELD_TO_KEY);
}

export function fieldNameFromShipmentListKey(sortKey: string): string {
  return hubFieldNameFromListSortKey(sortKey, ODAK_SHIPMENT_LIST_KEY_TO_FIELD);
}

export interface ShipmentListCellContext {
  lineQtyByShipmentId?: Map<string, number>;
}

export function shipmentListCellRaw(
  row: OdakShipmentRow,
  listKey: string,
  ctx?: ShipmentListCellContext
): string {
  switch (listKey) {
    case 'waybillNo':
      return String(row.waybillNo ?? '').trim() || '—';
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
    default:
      return '—';
  }
}
