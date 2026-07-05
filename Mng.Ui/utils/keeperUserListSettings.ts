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
  KEEPER_USER_LIST_DEFAULT_FORMATS,
  withKeeperDefaultListColumnFormats,
} from '@/utils/keeperListDefaultFormats';
import type { AfFilterColumn } from '@/utils/afListFilters';
import { buildKeeperFilterColumns } from '@/utils/keeperListFilters';
import { keeperProvisioningSourceSelectItems } from '@/utils/keeperListFilterOptions';

export const KEEPER_USER_LIST_FIELD_CATALOG: OdakHubListFieldDef[] = [
  { fieldName: 'username', listKey: 'username', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 1 },
  { fieldName: 'provisioningSource', listKey: 'provisioningSource', defaultVisible: true, defaultSortable: false, defaultFilterable: true, defaultOrder: 2 },
  { fieldName: 'email', listKey: 'email', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 3 },
  { fieldName: 'fullName', listKey: 'fullName', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 4 },
  { fieldName: 'department', listKey: 'department', defaultVisible: false, defaultSortable: true, defaultFilterable: true, defaultOrder: 5 },
  { fieldName: 'title', listKey: 'title', defaultVisible: false, defaultSortable: false, defaultFilterable: true, defaultOrder: 6 },
  { fieldName: 'isActive', listKey: 'isActive', defaultVisible: true, defaultSortable: true, defaultFilterable: true, defaultOrder: 7 },
  { fieldName: 'includeInApplication', listKey: 'includeInApplication', defaultVisible: true, defaultSortable: false, defaultFilterable: true, defaultOrder: 8 },
  { fieldName: 'groups', listKey: 'groups', defaultVisible: true, defaultSortable: false, defaultFilterable: true, defaultOrder: 9 },
  { fieldName: 'createdAt', listKey: 'createdAt', defaultVisible: true, defaultSortable: true, defaultFilterable: false, defaultOrder: 10 },
  { fieldName: 'updatedAt', listKey: 'updatedAt', defaultVisible: false, defaultSortable: true, defaultFilterable: false, defaultOrder: 11 },
  { fieldName: 'lastLoginAt', listKey: 'lastLoginAt', defaultVisible: false, defaultSortable: true, defaultFilterable: false, defaultOrder: 12 },
];

export const KEEPER_USER_LIST_FIELD_TO_KEY = buildFieldToListKeyMap(KEEPER_USER_LIST_FIELD_CATALOG);
export const KEEPER_USER_LIST_KEY_TO_FIELD = buildListKeyToFieldMap(KEEPER_USER_LIST_FIELD_CATALOG);

export const KEEPER_USER_SORT_FIELD_MAP: Record<string, string> = {
  username: 'username',
  email: 'email',
  fullName: 'firstName',
  department: 'department',
  title: 'title',
  isActive: 'isActive',
  createdAt: 'createdAt',
  updatedAt: 'updatedAt',
  lastLoginAt: 'lastLoginAt',
};

function baseKeeperUserListConfig(): OdakHubListConfig {
  return defaultHubListConfigFromCatalog(KEEPER_USER_LIST_FIELD_CATALOG, {
    enableSearch: true,
    defaultSortBy: 'username',
    defaultSortOrder: 'asc',
  });
}

export function defaultKeeperUserListConfig(): OdakHubListConfig {
  return withKeeperDefaultListColumnFormats(baseKeeperUserListConfig(), KEEPER_USER_LIST_DEFAULT_FORMATS);
}

export function parseKeeperUserListConfig(raw: unknown): OdakHubListConfig {
  return parseHubListConfig(raw, defaultKeeperUserListConfig());
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

export function mergeKeeperUserListConfig(saved: unknown): OdakHubListConfig {
  const merged = mergeHubListConfig(saved, baseKeeperUserListConfig());
  return withKeeperDefaultListColumnFormats(
    syncCatalogFilterableUpgrades(merged, KEEPER_USER_LIST_FIELD_CATALOG),
    KEEPER_USER_LIST_DEFAULT_FORMATS
  );
}

export function buildKeeperUserListHeaders(
  listConfig: OdakHubListConfig,
  columnTitle: (fieldName: string, listKey: string) => string
): OdakHubListHeader[] {
  return buildHubListHeaders(listConfig, KEEPER_USER_LIST_FIELD_TO_KEY, columnTitle);
}

export function buildKeeperUserFilterColumns(
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
      if (fieldName === 'groups') return 'group';
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

export function keeperUserListSortKeyFromField(fieldName: string): string {
  return hubListSortKeyFromField(fieldName, KEEPER_USER_LIST_FIELD_TO_KEY);
}

export function keeperUserFieldNameFromListSortKey(sortKey: string): string {
  return hubFieldNameFromListSortKey(sortKey, KEEPER_USER_LIST_KEY_TO_FIELD);
}

export function keeperUserBackendSortField(listSortKey: string): string {
  return KEEPER_USER_SORT_FIELD_MAP[listSortKey] ?? listSortKey;
}

export function buildKeeperVisibleListColumns(
  listConfig: OdakHubListConfig,
  fieldToKey: Record<string, string>,
  excludeListKeys: string[] = []
): { key: string; fieldName: string }[] {
  return [...listConfig.columns]
    .filter((c) => c.visible)
    .sort((a, b) => a.order - b.order)
    .map((c) => ({ key: fieldToKey[c.fieldName] ?? c.fieldName, fieldName: c.fieldName }))
    .filter((c) => !excludeListKeys.includes(c.key));
}
