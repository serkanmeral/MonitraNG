import { computed, type ComputedRef, type Ref } from 'vue';
import {
  applyListColumnFormatting,
  getListColumnCellStyle,
  type AfListColumnFormat,
} from '@/utils/afListColumnFormat';
import type { OdakHubListColumnConfig, OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';

export function useKeeperConfigurableListColumns(
  listConfig: ComputedRef<OdakHubListConfig> | Ref<OdakHubListConfig>
) {
  const columnByListKey = computed(() => {
    const cfg = 'value' in listConfig ? listConfig.value : listConfig;
    const map = new Map<string, OdakHubListColumnConfig>();
    for (const col of cfg.columns) {
      const listKey =
        cfg.columns.find((c) => c.fieldName === col.fieldName)?.fieldName === col.fieldName
          ? col.fieldName
          : col.fieldName;
      map.set(listKey, col);
    }
    return map;
  });

  function findColumnByListKey(listKey: string, fieldToKey: Record<string, string>): OdakHubListColumnConfig | undefined {
    const cfg = 'value' in listConfig ? listConfig.value : listConfig;
    const fieldName = Object.entries(fieldToKey).find(([, key]) => key === listKey)?.[0] ?? listKey;
    return cfg.columns.find((c) => c.fieldName === fieldName);
  }

  function cellDisplayValue(raw: string, listKey: string, fieldToKey: Record<string, string>): string {
    const col = findColumnByListKey(listKey, fieldToKey);
    return applyListColumnFormatting(raw, col?.format);
  }

  function cellStyle(
    raw: string,
    listKey: string,
    fieldToKey: Record<string, string>,
    item?: Record<string, unknown>
  ): Record<string, string> {
    const col = findColumnByListKey(listKey, fieldToKey);
    const fieldName =
      Object.entries(fieldToKey).find(([, key]) => key === listKey)?.[0] ?? listKey;
    return getListColumnCellStyle(raw, fieldName, col?.format, item);
  }

  const genericListColumns = computed(() => {
    const cfg = 'value' in listConfig ? listConfig.value : listConfig;
    return [...cfg.columns]
      .filter((c) => c.visible)
      .sort((a, b) => a.order - b.order)
      .map((c) => ({ fieldName: c.fieldName, key: c.fieldName }));
  });

  return {
    columnByListKey,
    cellDisplayValue,
    cellStyle,
    genericListColumns,
    findColumnByListKey,
  };
}

export function applyHubDefaultSort(
  listConfig: OdakHubListConfig,
  fieldToListKey: Record<string, string>,
  tableOptions: Ref<{ sortBy: Array<{ key: string; order: 'asc' | 'desc' }> }>
) {
  const sortField = listConfig.defaultSortBy;
  if (!sortField) return;
  const listKey = fieldToListKey[sortField] ?? sortField;
  tableOptions.value.sortBy = [{ key: listKey, order: listConfig.defaultSortOrder ?? 'asc' }];
}
