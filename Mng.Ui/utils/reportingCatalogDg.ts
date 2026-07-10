/**
 * Reporting catalog — DG merkezi kayıt (@reporting_reports / @reporting_categories).
 * Bellek önbelleği + bir kerelik localStorage migrate.
 */
import { fetchFromDataGateway } from '@/services/apiService';
import type { ReportingCategory, ReportingReportDefinition } from '@/types/apps/reporting';
import {
  loadReportingCatalog,
  parseReportingReportDefinition,
  saveReportingCatalog,
} from '@/utils/reportingCatalogStorage';
import {
  loadReportingCategories,
  normalizeReportingCategory,
  saveReportingCategories,
} from '@/utils/reportingCategoryStorage';

export const REPORTING_CATEGORIES_DATASET = '@reporting_categories';
export const REPORTING_REPORTS_DATASET = '@reporting_reports';

const MIGRATE_FLAG_PREFIX = 'mng_reporting_dg_migrated';

export interface ReportingCatalogCacheState {
  reports: ReportingReportDefinition[];
  categories: ReportingCategory[];
  reportDataIds: Map<string, string>;
  categoryDataIds: Map<string, string>;
  hydrated: boolean;
}

const cacheByDomain = new Map<string, ReportingCatalogCacheState>();
const hydrateInflight = new Map<string, Promise<void>>();

function migrateFlagKey(domainKey: string): string {
  return `${MIGRATE_FLAG_PREFIX}_${domainKey || 'default'}`;
}

function emptyCache(): ReportingCatalogCacheState {
  return {
    reports: [],
    categories: [],
    reportDataIds: new Map(),
    categoryDataIds: new Map(),
    hydrated: false,
  };
}

export function getReportingCatalogCache(domainKey: string): ReportingCatalogCacheState {
  const key = domainKey?.trim() || 'default';
  let cache = cacheByDomain.get(key);
  if (!cache) {
    cache = emptyCache();
    cacheByDomain.set(key, cache);
  }
  return cache;
}

function rowDataId(row: Record<string, unknown>): string {
  return String(row.__dataId ?? row.dataId ?? row.DataId ?? '').trim();
}

function extractItems(res: unknown): Record<string, unknown>[] {
  if (Array.isArray(res)) return res as Record<string, unknown>[];
  if (res && typeof res === 'object') {
    const o = res as Record<string, unknown>;
    const items = o.items ?? o.Items;
    if (Array.isArray(items)) return items as Record<string, unknown>[];
    const data = o.data ?? o.Data;
    if (Array.isArray(data)) return data as Record<string, unknown>[];
    if (data && typeof data === 'object') {
      const nested = data as Record<string, unknown>;
      const nestedItems = nested.items ?? nested.Items;
      if (Array.isArray(nestedItems)) return nestedItems as Record<string, unknown>[];
    }
  }
  return [];
}

function unwrapCreatedRow(res: unknown): Record<string, unknown> {
  if (!res || typeof res !== 'object') return {};
  const o = res as Record<string, unknown>;
  const data = o.data ?? o.Data;
  if (data && typeof data === 'object' && !Array.isArray(data)) {
    return data as Record<string, unknown>;
  }
  return o;
}

async function listDatasetRows(dataset: string): Promise<Record<string, unknown>[]> {
  const res = await fetchFromDataGateway(
    `/api/v1/data/${encodeURIComponent(dataset)}?limit=5000`
  );
  return extractItems(res);
}

function reportToDgBody(report: ReportingReportDefinition): Record<string, unknown> {
  return {
    id: report.id,
    title: report.title,
    description: report.description ?? '',
    categoryId: report.categoryId ?? '',
    datasetName: report.datasetName ?? '',
    listConfig: report.listConfig ?? { columns: [] },
    expand: report.expand ?? {},
    fieldPolicies: report.fieldPolicies ?? {},
    defaultFilters: report.defaultFilters ?? [],
    visibilityPolicies: report.visibilityPolicies ?? [],
    parameters: report.parameters ?? [],
    summary: report.summary ?? {},
    documentBindings: report.documentBindings ?? [],
    createdAt: report.createdAt,
    updatedAt: report.updatedAt,
  };
}

function categoryToDgBody(cat: ReportingCategory): Record<string, unknown> {
  return {
    id: cat.id,
    parentId: cat.parentId ?? '',
    ancestorIds: cat.ancestorIds ?? [],
    name: cat.name,
    description: cat.description ?? '',
    sortOrder: cat.sortOrder ?? 0,
    status: cat.status || 'active',
    createdBy: cat.createdBy ?? '',
    createdAt: cat.createdAt,
    updatedAt: cat.updatedAt,
  };
}

async function loadFromDg(cache: ReportingCatalogCacheState): Promise<void> {
  const [catRows, reportRows] = await Promise.all([
    listDatasetRows(REPORTING_CATEGORIES_DATASET),
    listDatasetRows(REPORTING_REPORTS_DATASET),
  ]);

  cache.categoryDataIds.clear();
  cache.categories = [];
  for (const row of catRows) {
    const parsed = normalizeReportingCategory(row);
    if (!parsed) continue;
    const dataId = rowDataId(row);
    if (dataId && dataId !== parsed.id) cache.categoryDataIds.set(parsed.id, dataId);
    else if (dataId) cache.categoryDataIds.set(parsed.id, dataId);
    cache.categories.push(parsed);
  }

  cache.reportDataIds.clear();
  cache.reports = [];
  for (const row of reportRows) {
    const parsed = parseReportingReportDefinition(row);
    if (!parsed) continue;
    const dataId = rowDataId(row);
    // Prefer logical id field; __dataId is separate
    const logicalId = String(row.id ?? row.Id ?? parsed.id).trim() || parsed.id;
    parsed.id = logicalId;
    if (dataId) cache.reportDataIds.set(logicalId, dataId);
    cache.reports.push(parsed);
  }
}

async function migrateLocalStorageIfNeeded(
  domainKey: string,
  cache: ReportingCatalogCacheState
): Promise<void> {
  if (typeof localStorage === 'undefined') return;
  if (localStorage.getItem(migrateFlagKey(domainKey)) === '1') return;
  if (cache.reports.length > 0 || cache.categories.length > 0) {
    localStorage.setItem(migrateFlagKey(domainKey), '1');
    return;
  }

  const lsCategories = loadReportingCategories(domainKey);
  const lsReports = loadReportingCatalog(domainKey).catalog.reports;
  if (!lsCategories.length && !lsReports.length) {
    localStorage.setItem(migrateFlagKey(domainKey), '1');
    return;
  }

  for (const cat of lsCategories) {
    await upsertCategoryToDg(domainKey, cat);
  }
  // reload category ids after posts
  await loadFromDg(cache);

  for (const report of lsReports) {
    await upsertReportToDg(domainKey, report);
  }
  await loadFromDg(cache);

  localStorage.setItem(migrateFlagKey(domainKey), '1');
  // Keep LS as backup snapshot (optional); clear active writes go to DG only
  try {
    saveReportingCategories(domainKey, cache.categories);
    saveReportingCatalog(domainKey, { reports: cache.reports });
  } catch {
    // ignore
  }
}

export async function hydrateReportingCatalog(domainKey: string): Promise<void> {
  const key = domainKey?.trim() || 'default';
  const existing = hydrateInflight.get(key);
  if (existing) return existing;

  const run = (async () => {
    const cache = getReportingCatalogCache(key);
    try {
      await loadFromDg(cache);
      await migrateLocalStorageIfNeeded(key, cache);
      cache.hydrated = true;
    } catch (e) {
      // DG unavailable: fall back to LS for this session
      console.warn('[reporting] DG catalog hydrate failed, using localStorage', e);
      cache.categories = loadReportingCategories(key);
      cache.reports = loadReportingCatalog(key).catalog.reports;
      cache.hydrated = true;
      cache.reportDataIds.clear();
      cache.categoryDataIds.clear();
    }
  })();

  hydrateInflight.set(key, run);
  try {
    await run;
  } finally {
    hydrateInflight.delete(key);
  }
}

export async function upsertReportToDg(
  domainKey: string,
  report: ReportingReportDefinition
): Promise<ReportingReportDefinition> {
  const cache = getReportingCatalogCache(domainKey);
  const body = reportToDgBody(report);
  const dataId = cache.reportDataIds.get(report.id);

  if (dataId) {
    await fetchFromDataGateway(
      `/api/v1/data/${encodeURIComponent(REPORTING_REPORTS_DATASET)}/${encodeURIComponent(dataId)}`,
      'PUT',
      body
    );
  } else {
    const created = await fetchFromDataGateway(
      `/api/v1/data/${encodeURIComponent(REPORTING_REPORTS_DATASET)}`,
      'POST',
      body
    );
    const newId = rowDataId(unwrapCreatedRow(created));
    if (newId) cache.reportDataIds.set(report.id, newId);
  }

  const idx = cache.reports.findIndex((r) => r.id === report.id);
  if (idx >= 0) cache.reports[idx] = report;
  else cache.reports.push(report);
  return report;
}

export async function deleteReportFromDg(domainKey: string, reportId: string): Promise<void> {
  const cache = getReportingCatalogCache(domainKey);
  const dataId = cache.reportDataIds.get(reportId);
  if (dataId) {
    await fetchFromDataGateway(
      `/api/v1/data/${encodeURIComponent(REPORTING_REPORTS_DATASET)}/${encodeURIComponent(dataId)}`,
      'DELETE'
    );
  }
  cache.reportDataIds.delete(reportId);
  cache.reports = cache.reports.filter((r) => r.id !== reportId);
}

export async function upsertCategoryToDg(
  domainKey: string,
  cat: ReportingCategory
): Promise<ReportingCategory> {
  const cache = getReportingCatalogCache(domainKey);
  const body = categoryToDgBody(cat);
  const dataId = cache.categoryDataIds.get(cat.id);

  if (dataId) {
    await fetchFromDataGateway(
      `/api/v1/data/${encodeURIComponent(REPORTING_CATEGORIES_DATASET)}/${encodeURIComponent(dataId)}`,
      'PUT',
      body
    );
  } else {
    const created = await fetchFromDataGateway(
      `/api/v1/data/${encodeURIComponent(REPORTING_CATEGORIES_DATASET)}`,
      'POST',
      body
    );
    const newId = rowDataId(unwrapCreatedRow(created));
    if (newId) cache.categoryDataIds.set(cat.id, newId);
  }

  const idx = cache.categories.findIndex((c) => c.id === cat.id);
  if (idx >= 0) cache.categories[idx] = cat;
  else cache.categories.push(cat);
  return cat;
}

export async function deleteCategoryFromDg(domainKey: string, categoryId: string): Promise<void> {
  const cache = getReportingCatalogCache(domainKey);
  const dataId = cache.categoryDataIds.get(categoryId);
  if (dataId) {
    await fetchFromDataGateway(
      `/api/v1/data/${encodeURIComponent(REPORTING_CATEGORIES_DATASET)}/${encodeURIComponent(dataId)}`,
      'DELETE'
    );
  }
  cache.categoryDataIds.delete(categoryId);
  cache.categories = cache.categories.filter((c) => c.id !== categoryId);
}

/** Invalidate cache (e.g. after domain switch). */
export function resetReportingCatalogCache(domainKey?: string): void {
  if (domainKey) {
    cacheByDomain.delete(domainKey.trim() || 'default');
    return;
  }
  cacheByDomain.clear();
}
