import {
  buildFieldToListKeyMap,
  buildHubListHeaders,
  buildListKeyToFieldMap,
  defaultHubListConfigFromCatalog,
  hubFieldNameFromListSortKey,
  hubListSortKeyFromField,
  mergeHubListConfig,
  parseHubListConfig,
  type OdakHubListConfig,
  type OdakHubListFieldDef,
  type OdakHubListHeader,
} from '@/utils/odakSiparisHubListConfig';
import {
  KEEPER_GROUP_LIST_DEFAULT_FORMATS,
  withKeeperDefaultListColumnFormats,
} from '@/utils/keeperListDefaultFormats';
import type { AfFilterColumn } from '@/utils/afListFilters';
import { buildKeeperFilterColumns } from '@/utils/keeperListFilters';
import { keeperProvisioningSourceSelectItems } from '@/utils/keeperListFilterOptions';

export const KEEPER_GROUP_LIST_FIELD_CATALOG: OdakHubListFieldDef[] = [
  { fieldName: 'name', listKey: 'name', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 1 },
  { fieldName: 'provisioningSource', listKey: 'provisioningSource', defaultVisible: true, defaultSortable: false, defaultFilterable: true, defaultOrder: 2 },
  { fieldName: 'description', listKey: 'description', defaultVisible: false, defaultSortable: false, defaultFilterable: true, defaultOrder: 3 },
  { fieldName: 'isActive', listKey: 'isActive', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 4 },
  { fieldName: 'includeInApplication', listKey: 'includeInApplication', defaultVisible: true, defaultSortable: false, defaultFilterable: true, defaultOrder: 5 },
  { fieldName: 'memberCount', listKey: 'memberCount', defaultVisible: true, defaultSortable: false, defaultFilterable: false, defaultOrder: 6, width: 96 },
  { fieldName: 'createdAt', listKey: 'createdAt', defaultVisible: true, defaultSortable: true, defaultFilterable: false, defaultOrder: 7 },
  { fieldName: 'updatedAt', listKey: 'updatedAt', defaultVisible: false, defaultSortable: true, defaultFilterable: false, defaultOrder: 8 },
];

export const KEEPER_GROUP_LIST_FIELD_TO_KEY = buildFieldToListKeyMap(KEEPER_GROUP_LIST_FIELD_CATALOG);
export const KEEPER_GROUP_LIST_KEY_TO_FIELD = buildListKeyToFieldMap(KEEPER_GROUP_LIST_FIELD_CATALOG);

export const KEEPER_GROUP_SORT_FIELD_MAP: Record<string, string> = {
  name: 'name',
  isActive: 'isActive',
  includeInApplication: 'includeInApplication',
  createdAt: 'createdAt',
  updatedAt: 'updatedAt',
};

function baseKeeperGroupListConfig(): OdakHubListConfig {
  return defaultHubListConfigFromCatalog(KEEPER_GROUP_LIST_FIELD_CATALOG, {
    enableSearch: true,
    defaultSortBy: 'name',
    defaultSortOrder: 'asc',
  });
}

export function defaultKeeperGroupListConfig(): OdakHubListConfig {
  return withKeeperDefaultListColumnFormats(baseKeeperGroupListConfig(), KEEPER_GROUP_LIST_DEFAULT_FORMATS);
}

export function parseKeeperGroupListConfig(raw: unknown): OdakHubListConfig {
  return parseHubListConfig(raw, defaultKeeperGroupListConfig());
}

function syncCatalogFilterableUpgrades(
  config: OdakHubListConfig,
  catalog: OdakHubListFieldDef[]
): OdakHubListConfig {
  const catalogByField = new Map(catalog.map((d) => [d.fieldName, d]));
  return {
    ...config,
    columns: config.columns.map((col) => {
      const cat = catalogByField.get(col.fieldName);
      if (!cat?.defaultFilterable || col.filterable) return col;
      return { ...col, filterable: true };
    }),
  };
}

export function mergeKeeperGroupListConfig(saved: unknown): OdakHubListConfig {
  const merged = mergeHubListConfig(saved, baseKeeperGroupListConfig());
  return withKeeperDefaultListColumnFormats(
    syncCatalogFilterableUpgrades(merged, KEEPER_GROUP_LIST_FIELD_CATALOG),
    KEEPER_GROUP_LIST_DEFAULT_FORMATS
  );
}

export function buildKeeperGroupListHeaders(
  listConfig: OdakHubListConfig,
  columnTitle: (fieldName: string, listKey: string) => string
): OdakHubListHeader[] {
  return buildHubListHeaders(listConfig, KEEPER_GROUP_LIST_FIELD_TO_KEY, columnTitle);
}

export function buildKeeperGroupFilterColumns(
  listConfig: OdakHubListConfig,
  labelForField: (fieldName: string) => string,
  t?: (key: string) => string
): AfFilterColumn[] {
  return buildKeeperFilterColumns(
    listConfig,
    labelForField,
    (fieldName) => {
      if (fieldName === 'isActive' || fieldName === 'includeInApplication') return 'bool';
      if (fieldName === 'provisioningSource') return 'select';
      return 'text';
    },
    (fieldName) => {
      if (fieldName === 'provisioningSource' && t) {
        return { selectItems: keeperProvisioningSourceSelectItems(t) };
      }
      return {};
    }
  );
}

export function keeperGroupListSortKeyFromField(fieldName: string): string {
  return hubListSortKeyFromField(fieldName, KEEPER_GROUP_LIST_FIELD_TO_KEY);
}

export function keeperGroupFieldNameFromListSortKey(sortKey: string): string {
  return hubFieldNameFromListSortKey(sortKey, KEEPER_GROUP_LIST_KEY_TO_FIELD);
}

export function keeperGroupBackendSortField(listSortKey: string): string {
  return KEEPER_GROUP_SORT_FIELD_MAP[listSortKey] ?? listSortKey;
}
