import * as XLSX from 'xlsx';
import { downloadFile, exportToCSV } from '@/utils/exportUtils';
import { reportingColumnListKey } from '@/utils/reportingListConfig';
import { reportingCellExportValue } from '@/utils/reportingCellDisplay';
import type { OdakHubListColumnConfig, OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';

export type ReportingExportFormat = 'csv' | 'xlsx';

export interface ReportingExportColumnChoice {
  key: string;
  title: string;
  column: OdakHubListColumnConfig;
}

/** Görünür sütunlardan export seçenekleri (order’a göre). */
export function listReportingExportColumnChoices(
  listConfig: OdakHubListConfig,
  columnTitles: Record<string, string>,
  canViewColumn?: (fieldName: string) => boolean
): ReportingExportColumnChoice[] {
  return [...listConfig.columns]
    .filter((c) => c.visible)
    .filter((c) => !canViewColumn || canViewColumn(c.fieldName))
    .sort((a, b) => a.order - b.order)
    .map((c) => {
      const key = reportingColumnListKey(c);
      return {
        key,
        title: columnTitles[key] ?? c.title ?? c.fieldName,
        column: c,
      };
    });
}

export function buildReportingExportMatrix(
  rows: Record<string, unknown>[],
  columns: ReportingExportColumnChoice[]
): { headers: string[]; matrix: string[][] } {
  const headers = columns.map((c) => c.title);
  const matrix = rows.map((row) => columns.map((c) => reportingCellExportValue(row, c.column)));
  return { headers, matrix };
}

export function exportReportingRowsToCsv(
  rows: Record<string, unknown>[],
  listConfig: OdakHubListConfig,
  columnTitles: Record<string, string>,
  filename: string,
  options?: {
    columnKeys?: string[];
    canViewColumn?: (fieldName: string) => boolean;
  }
): void {
  let choices = listReportingExportColumnChoices(listConfig, columnTitles, options?.canViewColumn);
  if (options?.columnKeys?.length) {
    const allow = new Set(options.columnKeys);
    choices = choices.filter((c) => allow.has(c.key));
  }
  if (!choices.length) return;

  const { headers, matrix } = buildReportingExportMatrix(rows, choices);
  const titled = matrix.map((cells) => {
    const out: Record<string, string> = {};
    choices.forEach((c, i) => {
      out[c.title] = cells[i] ?? '';
    });
    return out;
  });
  const base = filename.replace(/\.csv$/i, '');
  exportToCSV(titled, base, headers);
}

export function exportReportingRowsToXlsx(
  rows: Record<string, unknown>[],
  listConfig: OdakHubListConfig,
  columnTitles: Record<string, string>,
  filename: string,
  options?: {
    columnKeys?: string[];
    canViewColumn?: (fieldName: string) => boolean;
    sheetName?: string;
  }
): void {
  let choices = listReportingExportColumnChoices(listConfig, columnTitles, options?.canViewColumn);
  if (options?.columnKeys?.length) {
    const allow = new Set(options.columnKeys);
    choices = choices.filter((c) => allow.has(c.key));
  }
  if (!choices.length) return;

  const { headers, matrix } = buildReportingExportMatrix(rows, choices);
  const aoa: string[][] = [headers, ...matrix];
  const ws = XLSX.utils.aoa_to_sheet(aoa);
  const wb = XLSX.utils.book_new();
  const sheet = (options?.sheetName ?? 'Report').slice(0, 31) || 'Report';
  XLSX.utils.book_append_sheet(wb, ws, sheet);

  const base = filename.replace(/\.xlsx$/i, '');
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);
  const fullFilename = `${base}_${timestamp}.xlsx`;
  const wbout = XLSX.write(wb, { bookType: 'xlsx', type: 'array' });
  downloadFile(
    new Blob([wbout], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    }),
    fullFilename,
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
  );
}
