import type { OdakLineRow } from '@/utils/odakSiparisConfig';
import {
  buildFieldToListKeyMap,
  buildHubListHeaders,
  buildListKeyToFieldMap,
  defaultHubListConfigFromCatalog,
  fieldNameFromListSortKey,
  listSortKeyFromField,
  mergeHubListConfig,
  parseHubListConfig,
  filterHubListConfigToCatalog,
  type OdakHubListConfig,
  type OdakHubListFieldDef,
  type OdakHubListHeader,
} from '@/utils/odakSiparisHubListConfig';
import {
  ODAK_LINE_LIST_DEFAULT_FORMATS,
  withDefaultListColumnFormats,
} from '@/utils/odakSiparisHubListDefaultFormats';
import { formatOdakDate, formatOdakNumber } from '@/utils/odakSiparisService';
import { remainingQuantityForLine } from '@/utils/odakSiparisShipmentService';

export type OdakLineListColumnConfig = import('@/utils/odakSiparisHubListConfig').OdakHubListColumnConfig;
export type OdakLineListConfig = OdakHubListConfig;

export const ODAK_LINE_LIST_FIELD_CATALOG: OdakHubListFieldDef[] = [
  { fieldName: 'lineNo', listKey: 'lineNo', defaultVisible: true, defaultSortable: true, defaultFilterable: false, defaultOrder: 1, width: 88 },
  { fieldName: 'customerProjectNo', listKey: 'customerProjectNo', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 2, width: 120 },
  { fieldName: 'customerPoNo', listKey: 'customerPoNo', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 3, width: 120 },
  { fieldName: 'customerPoItemNo', listKey: 'customerPoItemNo', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 4, width: 88 },
  { fieldName: 'sasItemNo', listKey: 'sasItemNo', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 5, width: 100 },
  { fieldName: 'customerJobNo', listKey: 'customerJobNo', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 6, width: 120 },
  { fieldName: 'poItemRevNo', listKey: 'poItemRevNo', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 7, width: 100 },
  { fieldName: 'description', listKey: 'description', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 8 },
  { fieldName: 'quantity', listKey: 'quantity', defaultVisible: true, defaultSortable: true, defaultFilterable: false, defaultOrder: 9, width: 88 },
  { fieldName: 'shippedQuantity', listKey: 'shippedQuantity', defaultVisible: true, defaultSortable: true, defaultFilterable: false, defaultOrder: 10, width: 96 },
  { fieldName: 'remainingQuantity', listKey: 'remainingQuantity', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 11, width: 96 },
  { fieldName: 'unit', listKey: 'unit', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 12, width: 72 },
  { fieldName: 'deliveryDate', listKey: 'deliveryDate', defaultVisible: true, defaultSortable: true, defaultFilterable: false, defaultOrder: 13, width: 110 },
  { fieldName: 'shipmentDate', listKey: 'shipmentDate', defaultVisible: false, defaultSortable: true, defaultFilterable: false, defaultOrder: 14, width: 110 },
  { fieldName: 'shipmentAddress', listKey: 'shipmentAddress', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 15 },
  { fieldName: 'qualityReqs', listKey: 'qualityReqs', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 16 },
  { fieldName: 'isFai', listKey: 'isFai', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 17, width: 88 },
  { fieldName: 'isFaiComplete', listKey: 'isFaiComplete', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 18, width: 100 },
  { fieldName: 'unitCost', listKey: 'unitCost', defaultVisible: false, defaultSortable: true, defaultFilterable: false, defaultOrder: 19, width: 100 },
  { fieldName: 'totalCost', listKey: 'totalCost', defaultVisible: false, defaultSortable: true, defaultFilterable: false, defaultOrder: 20, width: 110 },
  { fieldName: 'currency', listKey: 'currency', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 21, width: 80 },
];

export const ODAK_LINE_LIST_FIELD_TO_KEY = buildFieldToListKeyMap(ODAK_LINE_LIST_FIELD_CATALOG);
export const ODAK_LINE_LIST_KEY_TO_FIELD = buildListKeyToFieldMap(ODAK_LINE_LIST_FIELD_CATALOG);

export function parseOdakLineListConfig(raw: unknown): OdakLineListConfig {
  return parseHubListConfig(raw, defaultOdakLineListConfig());
}

export function defaultOdakLineListConfig(): OdakLineListConfig {
  return withDefaultListColumnFormats(
    defaultHubListConfigFromCatalog(ODAK_LINE_LIST_FIELD_CATALOG, {
      defaultSortBy: 'lineNo',
      defaultSortOrder: 'asc',
    }),
    ODAK_LINE_LIST_DEFAULT_FORMATS
  );
}

export function mergeOdakLineListConfig(saved: unknown): OdakLineListConfig {
  const defaults = defaultHubListConfigFromCatalog(ODAK_LINE_LIST_FIELD_CATALOG, {
    defaultSortBy: 'lineNo',
    defaultSortOrder: 'asc',
  });
  const merged = mergeHubListConfig(saved, defaults);
  const filtered = filterHubListConfigToCatalog(merged, ODAK_LINE_LIST_FIELD_CATALOG);
  return withDefaultListColumnFormats(filtered, ODAK_LINE_LIST_DEFAULT_FORMATS);
}

export function buildLineListHeaders(
  listConfig: OdakLineListConfig,
  columnTitle: (fieldName: string, listKey: string) => string,
  canViewColumn?: (listKey: string) => boolean
): OdakHubListHeader[] {
  const headers = buildHubListHeaders(listConfig, ODAK_LINE_LIST_FIELD_TO_KEY, columnTitle, canViewColumn);
  for (const h of headers) {
    if (h.key === 'description') h.minWidth = 180;
  }
  return headers;
}

export function listSortKeyFromLineField(fieldName: string): string {
  return listSortKeyFromField(fieldName, ODAK_LINE_LIST_FIELD_TO_KEY);
}

export function fieldNameFromLineListKey(sortKey: string): string {
  return fieldNameFromListSortKey(sortKey, ODAK_LINE_LIST_KEY_TO_FIELD);
}

export function lineListCellRaw(row: OdakLineRow, listKey: string): string {
  switch (listKey) {
    case 'lineNo':
      return row.lineNo != null ? String(row.lineNo) : '—';
    case 'customerProjectNo':
      return String(row.customerProjectNo ?? '').trim() || '—';
    case 'customerPoNo':
      return String(row.customerPoNo ?? '').trim() || '—';
    case 'customerPoItemNo':
      return row.customerPoItemNo != null ? String(row.customerPoItemNo) : '—';
    case 'sasItemNo':
      return String(row.sasItemNo ?? '').trim() || '—';
    case 'customerJobNo':
      return String(row.customerJobNo ?? '').trim() || '—';
    case 'poItemRevNo':
      return String(row.poItemRevNo ?? '').trim() || '—';
    case 'description':
      return String(row.description ?? '').trim() || '—';
    case 'quantity':
      return formatOdakNumber(row.quantity);
    case 'shippedQuantity':
      return formatOdakNumber(row.shippedQuantity ?? 0);
    case 'remainingQuantity':
      return formatOdakNumber(remainingQuantityForLine(row, Number(row.shippedQuantity) || 0));
    case 'unit':
      return String(row.unit ?? '').trim() || '—';
    case 'deliveryDate':
      return formatOdakDate(row.deliveryDate);
    case 'shipmentDate':
      return formatOdakDate(row.shipmentDate);
    case 'shipmentAddress':
      return String(row.shipmentAddress ?? '').trim() || '—';
    case 'qualityReqs':
      return String(row.qualityReqs ?? '').trim() || '—';
    case 'isFai':
      return row.isFai ? 'Evet' : 'Hayır';
    case 'isFaiComplete':
      return row.isFaiComplete ? 'Evet' : 'Hayır';
    case 'unitCost':
      return formatOdakNumber(row.unitCost);
    case 'totalCost':
      return formatOdakNumber(row.totalCost);
    case 'currency':
      return String(row.currency ?? '').trim() || '—';
    default:
      return '—';
  }
}
