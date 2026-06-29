import {
  buildFieldToListKeyMap,
  buildHubListHeaders,
  buildListKeyToFieldMap,
  defaultHubListConfigFromCatalog,
  hubFieldNameFromListSortKey,
  hubListSortKeyFromField,
  mergeHubListConfig,
  parseHubListConfig,
  type OdakHubListColumnConfig,
  type OdakHubListConfig,
  type OdakHubListFieldDef,
  type OdakHubListHeader,
} from '@/utils/odakSiparisHubListConfig';
import {
  ODAK_PACKAGE_LIST_DEFAULT_FORMATS,
  withDefaultListColumnFormats,
} from '@/utils/odakSiparisHubListDefaultFormats';

export type OdakPackageListColumnConfig = OdakHubListColumnConfig;
export type OdakPackageListConfig = OdakHubListConfig;

/** Hub listConfig — tanımlı tüm iş paketi alanları (ayarlar + liste). */
export type OdakPackageListFieldDef = OdakHubListFieldDef;

export const ODAK_PACKAGE_LIST_FIELD_CATALOG: OdakPackageListFieldDef[] = [
  { fieldName: 'packageNo', listKey: 'displayNo', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 1 },
  { fieldName: 'name', listKey: 'name', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 2 },
  { fieldName: 'customerId', listKey: 'customer', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 3 },
  { fieldName: 'customerContactId', listKey: 'customerContact', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 4 },
  { fieldName: 'designContactId', listKey: 'designContact', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 5 },
  { fieldName: 'manufactureContactId', listKey: 'manufactureContact', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 6 },
  { fieldName: 'customerPo', listKey: 'customerPo', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 7 },
  { fieldName: 'projectNo', listKey: 'projectNo', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 8 },
  { fieldName: 'poVersion', listKey: 'poVersion', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 9 },
  { fieldName: 'status', listKey: 'statusLabel', defaultVisible: true, defaultSortable: true, defaultFilterable: false, defaultOrder: 10 },
  { fieldName: 'beginDate', listKey: 'beginDate', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 11 },
  { fieldName: 'deliveryDate', listKey: 'deliveryDate', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 12 },
  { fieldName: 'closedAt', listKey: 'closedAt', defaultVisible: false, defaultSortable: true, defaultFilterable: false, defaultOrder: 13 },
  { fieldName: 'partCount', listKey: 'partCount', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 14, width: 88 },
  { fieldName: 'stockCount', listKey: 'stockCount', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 15, width: 88 },
  { fieldName: 'shippedCount', listKey: 'shippedCount', defaultVisible: false, defaultSortable: true, defaultFilterable: false, defaultOrder: 16, width: 88 },
  { fieldName: 'lineCount', listKey: 'lineCount', defaultVisible: true, defaultSortable: true, defaultFilterable: false, defaultOrder: 17, width: 72 },
  { fieldName: 'deliveryAddress', listKey: 'deliveryAddress', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 18 },
  { fieldName: 'notes', listKey: 'notes', defaultVisible: false, defaultSortable: false, defaultFilterable: false, defaultOrder: 19 },
];

export type OdakPackageListColumnKey = (typeof ODAK_PACKAGE_LIST_FIELD_CATALOG)[number]['listKey'];

export const ODAK_PACKAGE_LIST_FIELD_TO_KEY = buildFieldToListKeyMap(ODAK_PACKAGE_LIST_FIELD_CATALOG);
export const ODAK_PACKAGE_LIST_KEY_TO_FIELD = buildListKeyToFieldMap(ODAK_PACKAGE_LIST_FIELD_CATALOG);

function baseOdakPackageListConfig(): OdakPackageListConfig {
  return defaultHubListConfigFromCatalog(ODAK_PACKAGE_LIST_FIELD_CATALOG, {
    enableSearch: true,
    defaultSortBy: 'packageNo',
    defaultSortOrder: 'desc',
  });
}

export function defaultOdakPackageListConfig(): OdakPackageListConfig {
  return withDefaultListColumnFormats(baseOdakPackageListConfig(), ODAK_PACKAGE_LIST_DEFAULT_FORMATS);
}

export function parseOdakPackageListConfig(raw: unknown): OdakPackageListConfig {
  return parseHubListConfig(raw, defaultOdakPackageListConfig());
}

export function mergeOdakPackageListConfig(saved: unknown): OdakPackageListConfig {
  return withDefaultListColumnFormats(
    mergeHubListConfig(saved, baseOdakPackageListConfig()),
    ODAK_PACKAGE_LIST_DEFAULT_FORMATS
  );
}

export type OdakPackageListHeader = OdakHubListHeader;

export function buildPackageListHeaders(
  listConfig: OdakPackageListConfig,
  columnTitle: (fieldName: string, listKey: string) => string,
  canViewColumn: (listKey: string) => boolean
): OdakPackageListHeader[] {
  return buildHubListHeaders(listConfig, ODAK_PACKAGE_LIST_FIELD_TO_KEY, columnTitle, canViewColumn);
}

export function buildPackageFilterColumns(
  listConfig: OdakPackageListConfig,
  labelForField: (fieldName: string) => string
): { key: string; label: string; kind: 'text' | 'date' | 'number' | 'relation' }[] {
  const out: { key: string; label: string; kind: 'text' | 'date' | 'number' | 'relation' }[] = [];
  for (const col of listConfig.columns) {
    if (!col.filterable) continue;
    const kind =
      col.fieldName === 'customerId'
        ? 'relation'
        : col.fieldName === 'beginDate' || col.fieldName === 'deliveryDate' || col.fieldName === 'closedAt'
          ? 'date'
          : col.fieldName === 'partCount' || col.fieldName === 'stockCount' || col.fieldName === 'shippedCount'
            ? 'number'
            : 'text';
    out.push({ key: col.fieldName, label: labelForField(col.fieldName), kind });
  }
  return out;
}

export function listSortKeyFromField(fieldName: string): string {
  return hubListSortKeyFromField(fieldName, ODAK_PACKAGE_LIST_FIELD_TO_KEY);
}

export function fieldNameFromListSortKey(sortKey: string): string {
  return hubFieldNameFromListSortKey(sortKey, ODAK_PACKAGE_LIST_KEY_TO_FIELD);
}
