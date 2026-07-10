import type { ReportingCatalog, ReportingCatalogSnapshot, ReportingReportDefinition, ReportingExpandChildListTab, ReportingExpandConfig } from '@/types/apps/reporting';
import {
  loadReportingCategories,
  migrateFlatCategoriesToTree,
  saveReportingCategories,
} from '@/utils/reportingCategoryStorage';
import { emptyOdakFieldPoliciesBlob, parseOdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import { defaultReportingExpandConfigFromFields } from '@/utils/reportingExpandLayout';
import { defaultReportingListConfigFromFields } from '@/utils/reportingListConfig';
import { normalizeReportingSummaryConfig, emptyReportingSummaryConfig } from '@/utils/reportingSummary';

const STORAGE_PREFIX = 'mng_reporting_catalog';

function storageKey(domainKey: string): string {
  return `${STORAGE_PREFIX}_${domainKey || 'default'}`;
}

function defaultCatalog(): ReportingCatalog {
  return { reports: [] };
}

interface LegacyFlatCategory {
  id: string;
  name: string;
  description?: string;
  order?: number;
}

function parseLegacyCategories(raw: unknown): LegacyFlatCategory[] {
  if (!Array.isArray(raw)) return [];
  const out: LegacyFlatCategory[] = [];
  for (const item of raw) {
    if (!item || typeof item !== 'object') continue;
    const o = item as Record<string, unknown>;
    const id = String(o.id ?? o.Id ?? '').trim();
    const name = String(o.name ?? o.Name ?? '').trim();
    if (!id || !name) continue;
    out.push({
      id,
      name,
      description: String(o.description ?? o.Description ?? '').trim() || undefined,
      order: Number(o.order ?? o.Order ?? 0) || 0,
    });
  }
  return out;
}

function migrateEmbeddedCategoriesIfNeeded(domainKey: string, legacyCategories: LegacyFlatCategory[]) {
  if (!legacyCategories.length) return;
  const existing = loadReportingCategories(domainKey);
  if (existing.length) return;
  saveReportingCategories(domainKey, migrateFlatCategoriesToTree(legacyCategories));
}

function normalizeCatalog(raw: unknown, domainKey: string): ReportingCatalog {
  if (!raw || typeof raw !== 'object') return defaultCatalog();
  const o = raw as Record<string, unknown>;
  const reportsRaw = o.reports ?? o.Reports;
  const legacyCategories = parseLegacyCategories(o.categories ?? o.Categories);
  migrateEmbeddedCategoriesIfNeeded(domainKey, legacyCategories);

  const reports: ReportingReportDefinition[] = [];
  if (Array.isArray(reportsRaw)) {
    for (const item of reportsRaw) {
      const parsed = parseReport(item);
      if (parsed) reports.push(parsed);
    }
  }

  return { reports };
}

function parseExpandConfig(raw: unknown): ReportingExpandConfig {
  const defaults = defaultReportingExpandConfigFromFields([]);
  if (!raw || typeof raw !== 'object') return defaults;
  const o = raw as Record<string, unknown>;
  const sections = (o.sections ?? o.Sections) as ReportingExpandConfig['sections'];
  const fieldCols = (o.fieldCols ?? o.FieldCols) as ReportingExpandConfig['fieldCols'];
  const actions = (o.actions ?? o.Actions) as ReportingExpandConfig['actions'];
  const tabsRaw = o.tabs ?? o.Tabs;
  const tabs = Array.isArray(tabsRaw)
    ? tabsRaw
        .map((tab) => {
          if (!tab || typeof tab !== 'object') return null;
          const t = tab as Record<string, unknown>;
          const id = String(t.id ?? t.Id ?? '').trim();
          const title = String(t.title ?? t.Title ?? '').trim();
          const childList = (t.childList ?? t.ChildList) as ReportingExpandChildListTab['childList'];
          if (!id || !title || !childList?.datasetName) return null;
          if (childList.summary != null) {
            childList.summary = normalizeReportingSummaryConfig(childList.summary);
          }
          const fieldPoliciesRaw = t.fieldPolicies ?? t.FieldPolicies;
          const visibilityPoliciesRaw = t.visibilityPolicies ?? t.VisibilityPolicies;
          return {
            id,
            title,
            childList,
            fieldPolicies: parseOdakFieldPoliciesBlob(fieldPoliciesRaw ?? {}),
            visibilityPolicies: Array.isArray(visibilityPoliciesRaw)
              ? (visibilityPoliciesRaw as ReportingExpandChildListTab['visibilityPolicies'])
              : [],
          };
        })
        .filter((tab): tab is NonNullable<typeof tab> => tab != null)
    : [];

  return {
    enabled: Boolean(o.enabled ?? o.Enabled ?? defaults.enabled),
    hideEmptyFields: o.hideEmptyFields != null ? Boolean(o.hideEmptyFields) : defaults.hideEmptyFields,
    heading: String(o.heading ?? o.Heading ?? defaults.heading),
    intro: String(o.intro ?? o.Intro ?? defaults.intro),
    sections: Array.isArray(sections) ? sections : defaults.sections,
    fieldCols: fieldCols && typeof fieldCols === 'object' ? fieldCols : defaults.fieldCols,
    actions: Array.isArray(actions) ? actions : defaults.actions,
    tabs,
    defaultTabId: String(o.defaultTabId ?? o.DefaultTabId ?? 'fields'),
  };
}

function parseReport(raw: unknown): ReportingReportDefinition | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const id = String(o.id ?? o.Id ?? '').trim();
  const title = String(o.title ?? o.Title ?? '').trim();
  const datasetName = String(o.datasetName ?? o.DatasetName ?? '').trim();
  if (!id || !title) return null;

  const listConfig = (o.listConfig ?? o.ListConfig) as ReportingReportDefinition['listConfig'];
  const expand = parseExpandConfig(o.expand ?? o.Expand);
  const fieldPolicies = (o.fieldPolicies ?? o.FieldPolicies) as ReportingReportDefinition['fieldPolicies'];
  const defaultFilters = (o.defaultFilters ?? o.DefaultFilters) as ReportingReportDefinition['defaultFilters'];
  const visibilityPolicies = (o.visibilityPolicies ??
    o.VisibilityPolicies) as ReportingReportDefinition['visibilityPolicies'];
  const parameters = (o.parameters ?? o.Parameters) as ReportingReportDefinition['parameters'];

  return {
    id,
    title,
    description: String(o.description ?? o.Description ?? '').trim() || undefined,
    categoryId: o.categoryId != null ? String(o.categoryId) : o.CategoryId != null ? String(o.CategoryId) : null,
    datasetName,
    listConfig: listConfig?.columns ? listConfig : { columns: [] },
    expand,
    fieldPolicies: fieldPolicies ?? emptyOdakFieldPoliciesBlob(),
    defaultFilters: Array.isArray(defaultFilters) ? defaultFilters : [],
    visibilityPolicies: Array.isArray(visibilityPolicies) ? visibilityPolicies : [],
    parameters: Array.isArray(parameters) ? parameters : [],
    summary: normalizeReportingSummaryConfig(o.summary ?? o.Summary),
    createdAt: String(o.createdAt ?? o.CreatedAt ?? new Date().toISOString()),
    updatedAt: String(o.updatedAt ?? o.UpdatedAt ?? new Date().toISOString()),
  };
}

export function loadReportingCatalog(domainKey: string): ReportingCatalogSnapshot {
  if (typeof localStorage === 'undefined') {
    return { catalog: defaultCatalog(), updatedAt: new Date().toISOString() };
  }
  try {
    const raw = localStorage.getItem(storageKey(domainKey));
    if (!raw) return { catalog: defaultCatalog(), updatedAt: new Date().toISOString() };
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    const catalog = normalizeCatalog(parsed.catalog ?? parsed, domainKey);
    return {
      catalog,
      updatedAt: String(parsed.updatedAt ?? new Date().toISOString()),
    };
  } catch {
    return { catalog: defaultCatalog(), updatedAt: new Date().toISOString() };
  }
}

export function saveReportingCatalog(domainKey: string, catalog: ReportingCatalog): void {
  if (typeof localStorage === 'undefined') {
    throw new Error('localStorage is not available');
  }
  try {
    const payload: ReportingCatalogSnapshot = {
      catalog: { reports: catalog.reports },
      updatedAt: new Date().toISOString(),
    };
    localStorage.setItem(storageKey(domainKey), JSON.stringify(payload));
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    throw new Error(msg || 'Failed to save report catalog');
  }
}

export function newReportingReportId(): string {
  return `rpt_${Date.now()}_${Math.random().toString(36).slice(2, 7)}`;
}

export function createEmptyReportDefinition(title: string, categoryId: string | null): ReportingReportDefinition {
  const now = new Date().toISOString();
  return {
    id: newReportingReportId(),
    title,
    categoryId,
    datasetName: '',
    listConfig: { columns: [] },
    expand: defaultReportingExpandConfigFromFields([]),
    fieldPolicies: emptyOdakFieldPoliciesBlob(),
    defaultFilters: [],
    visibilityPolicies: [],
    parameters: [],
    summary: emptyReportingSummaryConfig(),
    createdAt: now,
    updatedAt: now,
  };
}

export function draftFromReportDefinition(report: ReportingReportDefinition) {
  return {
    title: report.title,
    datasetName: report.datasetName,
    categoryId: report.categoryId,
    description: report.description,
    listConfig: JSON.parse(JSON.stringify(report.listConfig)),
    expand: JSON.parse(JSON.stringify(report.expand)),
    fieldPolicies: JSON.parse(JSON.stringify(report.fieldPolicies)),
    defaultFilters: JSON.parse(JSON.stringify(report.defaultFilters)),
    visibilityPolicies: JSON.parse(JSON.stringify(report.visibilityPolicies)),
    parameters: JSON.parse(JSON.stringify(report.parameters ?? [])),
    summary: normalizeReportingSummaryConfig(report.summary),
  };
}

export function reportFromDraft(
  existing: ReportingReportDefinition | null,
  draft: {
    title: string;
    description?: string;
    categoryId: string | null;
    datasetName: string;
    listConfig: ReportingReportDefinition['listConfig'];
    expand: ReportingReportDefinition['expand'];
    fieldPolicies: ReportingReportDefinition['fieldPolicies'];
    defaultFilters: ReportingReportDefinition['defaultFilters'];
    visibilityPolicies: ReportingReportDefinition['visibilityPolicies'];
    parameters?: ReportingReportDefinition['parameters'];
    summary?: ReportingReportDefinition['summary'];
  }
): ReportingReportDefinition {
  const now = new Date().toISOString();
  return {
    id: existing?.id ?? newReportingReportId(),
    title: draft.title.trim(),
    description: draft.description?.trim() || undefined,
    categoryId: draft.categoryId,
    datasetName: draft.datasetName.trim(),
    listConfig: JSON.parse(JSON.stringify(draft.listConfig)),
    expand: JSON.parse(JSON.stringify(draft.expand)),
    fieldPolicies: JSON.parse(JSON.stringify(draft.fieldPolicies)),
    defaultFilters: JSON.parse(JSON.stringify(draft.defaultFilters)),
    visibilityPolicies: JSON.parse(JSON.stringify(draft.visibilityPolicies)),
    parameters: JSON.parse(JSON.stringify(draft.parameters ?? existing?.parameters ?? [])),
    summary: normalizeReportingSummaryConfig(draft.summary ?? existing?.summary),
    createdAt: existing?.createdAt ?? now,
    updatedAt: now,
  };
}

export function freshReportConfigFromSchema(fields: Parameters<typeof defaultReportingListConfigFromFields>[0]) {
  return {
    listConfig: defaultReportingListConfigFromFields(fields),
    expand: defaultReportingExpandConfigFromFields(fields),
    fieldPolicies: emptyOdakFieldPoliciesBlob(),
    defaultFilters: [] as ReportingReportDefinition['defaultFilters'],
  };
}
