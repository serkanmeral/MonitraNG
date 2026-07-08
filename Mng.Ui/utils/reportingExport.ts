import { exportToCSV } from '@/utils/exportUtils';
import { reportingColumnListKey } from '@/utils/reportingListConfig';
import { reportingCellExportValue } from '@/utils/reportingCellDisplay';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';

export function exportReportingRowsToCsv(
  rows: Record<string, unknown>[],
  listConfig: OdakHubListConfig,
  columnTitles: Record<string, string>,
  filename: string
): void {
  const columns = [...listConfig.columns]
    .filter((c) => c.visible)
    .sort((a, b) => a.order - b.order);

  const headers = columns.map((c) => columnTitles[reportingColumnListKey(c)] ?? c.title ?? c.fieldName);
  const data = rows.map((row) => {
    const out: Record<string, string> = {};
    for (const col of columns) {
      const key = reportingColumnListKey(col);
      out[key] = reportingCellExportValue(row, col);
    }
    return out;
  });

  exportToCSV(data, filename, headers);
}
