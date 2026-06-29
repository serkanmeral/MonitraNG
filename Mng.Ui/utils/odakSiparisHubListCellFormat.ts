import {
  applyListColumnFormatting,
  getListColumnCellStyle,
} from '@/utils/afListColumnFormat';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import { hubFieldNameFromListSortKey } from '@/utils/odakSiparisHubListConfig';

export function columnConfigForListKey(
  listConfig: OdakHubListConfig,
  listKey: string,
  listKeyToField: Record<string, string>
) {
  const fieldName = hubFieldNameFromListSortKey(listKey, listKeyToField);
  return listConfig.columns.find((c) => c.fieldName === fieldName);
}

export function hubListCellDisplayValue(
  raw: string,
  listKey: string,
  listConfig: OdakHubListConfig,
  listKeyToField: Record<string, string>,
  item: Record<string, unknown>
): string {
  const col = columnConfigForListKey(listConfig, listKey, listKeyToField);
  return applyListColumnFormatting(raw, col?.format);
}

export function hubListCellStyle(
  listKey: string,
  raw: string,
  listConfig: OdakHubListConfig,
  listKeyToField: Record<string, string>,
  item: Record<string, unknown>
): Record<string, string> {
  const col = columnConfigForListKey(listConfig, listKey, listKeyToField);
  const fieldName = hubFieldNameFromListSortKey(listKey, listKeyToField);
  return getListColumnCellStyle(raw, fieldName, col?.format, item);
}
