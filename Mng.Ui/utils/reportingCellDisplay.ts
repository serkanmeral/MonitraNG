import { applyListColumnFormatting } from '@/utils/afListColumnFormat';
import type { OdakHubListColumnConfig } from '@/utils/odakSiparisHubListConfig';
import { reportingCellRawForColumn } from '@/utils/reportingListConfig';

export function reportingCellDisplayValue(
  raw: string,
  col: OdakHubListColumnConfig | undefined
): string {
  return applyListColumnFormatting(raw, col?.format);
}

export function reportingCellExportValue(
  row: Record<string, unknown>,
  col: OdakHubListColumnConfig
): string {
  const raw = reportingCellRawForColumn(row, col);
  return reportingCellDisplayValue(raw, col);
}
