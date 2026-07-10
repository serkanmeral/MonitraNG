import type { ReportingCategory } from '@/types/apps/reporting';

const STORAGE_PREFIX = 'mng_reporting_categories';

function storageKey(domainKey: string): string {
  return `${STORAGE_PREFIX}_${domainKey || 'default'}`;
}

function normalizeCategory(raw: unknown): ReportingCategory | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const id = String(o.id ?? o.__dataId ?? o.Id ?? '').trim();
  const name = String(o.name ?? o.Name ?? '').trim();
  if (!id || !name) return null;

  const parentRaw = o.parentId ?? o.ParentId;
  const ancestorRaw = o.ancestorIds ?? o.AncestorIds;

  return {
    id,
    parentId: parentRaw != null && String(parentRaw).trim() ? String(parentRaw) : null,
    ancestorIds: Array.isArray(ancestorRaw)
      ? ancestorRaw.map((x) => String(x).trim()).filter(Boolean)
      : [],
    name,
    description: o.description != null ? String(o.description) : o.Description != null ? String(o.Description) : null,
    sortOrder: Number(o.sortOrder ?? o.SortOrder ?? 0) || 0,
    status: String(o.status ?? o.Status ?? 'active'),
    createdBy: o.createdBy != null ? String(o.createdBy) : null,
    createdAt: o.createdAt != null ? String(o.createdAt) : null,
    updatedAt: o.updatedAt != null ? String(o.updatedAt) : null,
  };
}

export function normalizeReportingCategory(raw: unknown): ReportingCategory | null {
  return normalizeCategory(raw);
}

export function loadReportingCategories(domainKey: string): ReportingCategory[] {
  if (typeof localStorage === 'undefined') return [];
  try {
    const raw = localStorage.getItem(storageKey(domainKey));
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed.map(normalizeCategory).filter((c): c is ReportingCategory => c != null);
  } catch {
    return [];
  }
}

export function saveReportingCategories(domainKey: string, categories: ReportingCategory[]): void {
  if (typeof localStorage === 'undefined') return;
  try {
    localStorage.setItem(storageKey(domainKey), JSON.stringify(categories));
  } catch {
    // ignore
  }
}

export function newReportingCategoryId(): string {
  return `rcat_${Date.now()}_${Math.random().toString(36).slice(2, 7)}`;
}

function resolveAncestors(categories: ReportingCategory[], parentId: string | null): string[] {
  if (!parentId) return [];
  const parent = categories.find((c) => c.id === parentId);
  if (!parent) return [];
  return [...parent.ancestorIds, parentId];
}

export function createReportingCategoryRecord(
  categories: ReportingCategory[],
  input: { name: string; description?: string; parentId?: string | null; createdBy?: string | null }
): ReportingCategory {
  const name = input.name.trim();
  if (!name) throw new Error('Category name is required');

  const parentId = input.parentId?.trim() || null;
  if (parentId && !categories.some((c) => c.id === parentId)) {
    throw new Error('Parent category not found');
  }

  const now = new Date().toISOString();
  return {
    id: newReportingCategoryId(),
    parentId,
    ancestorIds: resolveAncestors(categories, parentId),
    name,
    description: input.description?.trim() || null,
    sortOrder: 0,
    status: 'active',
    createdBy: input.createdBy ?? null,
    createdAt: now,
    updatedAt: now,
  };
}

export function renameReportingCategoryRecord(
  categories: ReportingCategory[],
  id: string,
  name: string
): ReportingCategory {
  const trimmed = name.trim();
  if (!trimmed) throw new Error('Category name is required');
  const idx = categories.findIndex((c) => c.id === id);
  if (idx < 0) throw new Error('Category not found');
  const now = new Date().toISOString();
  categories[idx] = {
    ...categories[idx],
    name: trimmed,
    updatedAt: now,
  };
  return categories[idx];
}

/** Eski düz katalog kategorilerini DI uyumlu kayda dönüştürür (tek seferlik). */
export function migrateFlatCategoriesToTree(
  flat: { id: string; name: string; description?: string; order?: number }[]
): ReportingCategory[] {
  const now = new Date().toISOString();
  return flat.map((c, idx) => ({
    id: c.id,
    parentId: null,
    ancestorIds: [],
    name: c.name,
    description: c.description?.trim() || null,
    sortOrder: c.order ?? idx,
    status: 'active',
    createdBy: null,
    createdAt: now,
    updatedAt: now,
  }));
}
