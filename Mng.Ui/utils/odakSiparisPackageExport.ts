import type { AfListFilter } from '@/utils/afListFilters';
import { exportToCSV } from '@/utils/exportUtils';
import type { OdakPackageRow } from '@/utils/odakSiparisConfig';
import {
  customerLabelFromRow,
  fetchCustomerLabelMap,
  fetchOdakPackagesPage,
  fetchPackageLineStatsMap,
  packageDataId,
  packageDisplayNo,
  packageStatusLabel,
  filterPackagesByLineAdv,
  type OdakPackageLineAdvFilter,
  type OdakPackageLineStats,
  type OdakPackageListQuery,
  type OdakPackageListSort,
} from '@/utils/odakSiparisService';

export const ODAK_PACKAGE_EXPORT_MAX = 5000;
const EXPORT_PAGE_SIZE = 500;

export interface OdakPackageExportQuery {
  statusTab: OdakPackageListQuery['statusTab'];
  search?: string;
  advancedFilters?: AfListFilter[];
  lineAdv?: OdakPackageLineAdvFilter;
  sortBy?: OdakPackageListSort[];
}

export interface OdakPackageExportColumn {
  key: string;
  label: string;
}

/** Liste sutunlari ile hizali CSV basliklari. */
export function odakPackageExportColumns(labels: Record<string, string>): OdakPackageExportColumn[] {
  return [
    { key: 'packageNo', label: labels.packageNo ?? 'İş Paketi No' },
    { key: 'name', label: labels.name ?? 'İş Paketi İsmi' },
    { key: 'customer', label: labels.customer ?? 'Müşteri' },
    { key: 'customerPo', label: labels.customerPo ?? 'Müşteri PO' },
    { key: 'projectNo', label: labels.projectNo ?? 'Proje No' },
    { key: 'partCount', label: labels.partCount ?? 'Parça Sayısı' },
    { key: 'stockCount', label: labels.stockCount ?? 'Stok Sayısı' },
    { key: 'lineCount', label: labels.lineCount ?? 'Kalem' },
    { key: 'status', label: labels.status ?? 'Durum' },
    { key: 'beginDate', label: labels.beginDate ?? 'Başlangıç' },
    { key: 'deliveryDate', label: labels.deliveryDate ?? 'Termin' },
    { key: 'poVersion', label: labels.poVersion ?? 'PO versiyonu' },
  ];
}

function lineCountLabel(item: OdakPackageRow, lineStats: Map<string, OdakPackageLineStats>): string {
  if (item.lineCount != null && item.lineCount >= 0) return String(item.lineCount);
  const fromStats = lineStats.get(packageDataId(item))?.lineCount;
  return fromStats != null && fromStats > 0 ? String(fromStats) : '';
}

function exportDate(v: unknown): string {
  if (!v) return '';
  try {
    return new Date(String(v)).toLocaleDateString('tr-TR');
  } catch {
    return String(v);
  }
}

export function buildOdakPackageExportRows(
  items: OdakPackageRow[],
  customerLabels: Record<string, string>,
  lineStats: Map<string, OdakPackageLineStats>
): Record<string, string>[] {
  return items.map((item) => {
    const id = packageDataId(item);
    const stats = lineStats.get(id);
    return {
      packageNo: packageDisplayNo(item),
      name: String(item.name ?? ''),
      customer: customerLabelFromRow(item, customerLabels),
      customerPo: stats?.customerPoNos ?? '',
      projectNo: stats?.customerProjectNos ?? '',
      partCount: item.partCount != null ? String(item.partCount) : '',
      stockCount: item.stockCount != null ? String(item.stockCount) : '',
      lineCount: lineCountLabel(item, lineStats),
      status: packageStatusLabel(item.status),
      beginDate: exportDate(item.beginDate),
      deliveryDate: exportDate(item.deliveryDate),
      poVersion: String(item.poVersion ?? ''),
    };
  });
}

async function fetchAllPackagesForExport(query: OdakPackageExportQuery): Promise<OdakPackageRow[]> {
  const all: OdakPackageRow[] = [];
  let skip = 0;
  let total = Number.POSITIVE_INFINITY;

  while (skip < total && all.length < ODAK_PACKAGE_EXPORT_MAX) {
    const limit = Math.min(EXPORT_PAGE_SIZE, ODAK_PACKAGE_EXPORT_MAX - all.length);
    const resp = await fetchOdakPackagesPage({
      statusTab: query.statusTab,
      skip,
      limit,
      search: query.search,
      advancedFilters: query.advancedFilters,
      sortBy: query.sortBy,
    });
    const batch = resp.items ?? [];
    total = resp.total ?? batch.length;
    if (!batch.length) break;
    all.push(...batch);
    skip += batch.length;
    if (batch.length < limit) break;
  }

  return all.slice(0, ODAK_PACKAGE_EXPORT_MAX);
}

function needsLineStats(lineAdv?: OdakPackageLineAdvFilter): boolean {
  if (!lineAdv) return false;
  return Boolean(
    lineAdv.customerPo?.trim() ||
      lineAdv.customerProjectNo?.trim() ||
      lineAdv.customerPoItem?.trim() ||
      lineAdv.productDesc?.trim()
  );
}

/** Mevcut liste filtreleriyle is paketlerini CSV (Excel uyumlu) olarak indirir. */
export async function exportOdakPackagesToCsv(
  query: OdakPackageExportQuery,
  columnLabels: Record<string, string>
): Promise<{ rowCount: number; truncated: boolean }> {
  const customerLabels = await fetchCustomerLabelMap();
  let items = await fetchAllPackagesForExport(query);
  const truncated = items.length >= ODAK_PACKAGE_EXPORT_MAX;

  let lineStats = new Map<string, OdakPackageLineStats>();
  if (items.length) {
    lineStats = await fetchPackageLineStatsMap(items.map((x) => packageDataId(x)).filter(Boolean));
  }

  if (needsLineStats(query.lineAdv)) {
    items = filterPackagesByLineAdv(items, query.lineAdv ?? {}, lineStats);
  }

  const columns = odakPackageExportColumns(columnLabels);
  const rows = buildOdakPackageExportRows(items, customerLabels, lineStats);
  const headers = columns.map((c) => c.label);
  const data = rows.map((row) => {
    const out: Record<string, string> = {};
    for (const col of columns) {
      out[col.label] = row[col.key] ?? '';
    }
    return out;
  });

  exportToCSV(data, 'odak_is_paketleri', headers);
  return { rowCount: items.length, truncated };
}
