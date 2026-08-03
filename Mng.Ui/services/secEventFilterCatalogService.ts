import type {
  SecEventFilterCatalogState,
  SecEventFilterCategory,
  SecEventFilterTreeNode,
  SecEventSavedFilter,
} from '@/types/apps/secEventFilterCatalog';
import { SEC_EVENT_FILTER_CATALOG_ROOT_ID } from '@/types/apps/secEventFilterCatalog';
import {
  createSecEventFilterCatalogSeed,
  SEC_FILTER_CAT_USER,
} from '@/utils/secEventFilterCatalogSeed';

const STORAGE_KEY = 'mng.siem.secEventFilterCatalog.v1';

function cloneState(state: SecEventFilterCatalogState): SecEventFilterCatalogState {
  return {
    categories: state.categories.map((c) => ({ ...c })),
    filters: state.filters.map((f) => ({
      ...f,
      scope: {
        type: f.scope?.type ?? null,
        product: f.scope?.product ?? null,
        hosts: [...(f.scope?.hosts ?? [])],
      },
      fields: f.fields.map((x) => ({ ...x })),
    })),
  };
}

/** Merge system seed with user localStorage overrides (user cats/filters only). */
export function loadSecEventFilterCatalog(): SecEventFilterCatalogState {
  const seed = createSecEventFilterCatalogSeed();
  if (typeof window === 'undefined') return seed;

  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return seed;
    const parsed = JSON.parse(raw) as Partial<SecEventFilterCatalogState>;
    const userCategories = (parsed.categories ?? []).filter((c) => c && !c.isSystem);
    const userFilters = (parsed.filters ?? []).filter((f) => f && !f.isSystem);

    const categories = [
      ...seed.categories,
      ...userCategories.filter((c) => !seed.categories.some((s) => s.id === c.id)),
    ];
    const filters = [
      ...seed.filters,
      ...userFilters.filter((f) => !seed.filters.some((s) => s.id === f.id)),
    ];
    return { categories, filters };
  } catch {
    return seed;
  }
}

function persistUserSlice(state: SecEventFilterCatalogState) {
  if (typeof window === 'undefined') return;
  const payload: SecEventFilterCatalogState = {
    categories: state.categories.filter((c) => !c.isSystem),
    filters: state.filters.filter((f) => !f.isSystem),
  };
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
  } catch {
    /* ignore quota */
  }
}

export function saveSecEventFilterCatalog(state: SecEventFilterCatalogState): SecEventFilterCatalogState {
  const next = cloneState(state);
  persistUserSlice(next);
  return next;
}

export function buildSecEventFilterTree(state: SecEventFilterCatalogState): SecEventFilterTreeNode[] {
  const byParent = new Map<string | null, SecEventFilterCategory[]>();
  for (const cat of [...state.categories].sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))) {
    const key = cat.parentId;
    const list = byParent.get(key) ?? [];
    list.push(cat);
    byParent.set(key, list);
  }

  const filtersByCat = new Map<string, SecEventSavedFilter[]>();
  for (const f of state.filters) {
    const list = filtersByCat.get(f.categoryId) ?? [];
    list.push(f);
    filtersByCat.set(f.categoryId, list);
  }

  function buildCategoryNode(cat: SecEventFilterCategory): SecEventFilterTreeNode {
    const childCats = byParent.get(cat.id) ?? [];
    const childFilters = (filtersByCat.get(cat.id) ?? []).sort((a, b) => a.name.localeCompare(b.name));
    const children: SecEventFilterTreeNode[] = [
      ...childCats.map(buildCategoryNode),
      ...childFilters.map((f) => ({
        id: `filter:${f.id}`,
        kind: 'filter' as const,
        name: f.name,
        isSystem: f.isSystem,
        filterId: f.id,
      })),
    ];
    return {
      id: `category:${cat.id}`,
      kind: 'category',
      name: cat.name,
      isSystem: cat.isSystem,
      children,
    };
  }

  return (byParent.get(null) ?? []).map(buildCategoryNode);
}

export function findFilterById(
  state: SecEventFilterCatalogState,
  filterId: string,
): SecEventSavedFilter | null {
  return state.filters.find((f) => f.id === filterId) ?? null;
}

export function findCategoryById(
  state: SecEventFilterCatalogState,
  categoryId: string,
): SecEventFilterCategory | null {
  return state.categories.find((c) => c.id === categoryId) ?? null;
}

export function defaultUserCategoryId(state: SecEventFilterCatalogState): string {
  return state.categories.find((c) => c.id === SEC_FILTER_CAT_USER)?.id
    ?? state.categories.find((c) => !c.isSystem)?.id
    ?? SEC_FILTER_CAT_USER;
}

export function createUserCategory(
  state: SecEventFilterCatalogState,
  name: string,
  parentId: string | null = SEC_FILTER_CAT_USER,
): SecEventFilterCatalogState {
  const next = cloneState(state);
  const id = `cat-user-${Date.now().toString(36)}`;
  next.categories.push({
    id,
    parentId,
    name: name.trim() || 'Yeni kategori',
    sortOrder: next.categories.length,
    isSystem: false,
  });
  return saveSecEventFilterCatalog(next);
}

export function upsertUserFilter(
  state: SecEventFilterCatalogState,
  filter: SecEventSavedFilter,
): SecEventFilterCatalogState {
  if (filter.isSystem) {
    throw new Error('System filters are read-only; use Save as copy.');
  }
  const next = cloneState(state);
  const idx = next.filters.findIndex((f) => f.id === filter.id);
  if (idx >= 0) next.filters[idx] = cloneState({ categories: [], filters: [filter] }).filters[0];
  else next.filters.push(filter);
  return saveSecEventFilterCatalog(next);
}

export function deleteUserFilter(
  state: SecEventFilterCatalogState,
  filterId: string,
): SecEventFilterCatalogState {
  const existing = findFilterById(state, filterId);
  if (!existing || existing.isSystem) return state;
  const next = cloneState(state);
  next.filters = next.filters.filter((f) => f.id !== filterId);
  return saveSecEventFilterCatalog(next);
}

export function deleteUserCategory(
  state: SecEventFilterCatalogState,
  categoryId: string,
): SecEventFilterCatalogState {
  const existing = findCategoryById(state, categoryId);
  if (!existing || existing.isSystem) return state;
  if (state.filters.some((f) => f.categoryId === categoryId)) return state;
  if (state.categories.some((c) => c.parentId === categoryId)) return state;
  const next = cloneState(state);
  next.categories = next.categories.filter((c) => c.id !== categoryId);
  return saveSecEventFilterCatalog(next);
}

export function renameUserCategory(
  state: SecEventFilterCatalogState,
  categoryId: string,
  name: string,
): SecEventFilterCatalogState {
  const existing = findCategoryById(state, categoryId);
  if (!existing || existing.isSystem) return state;
  const next = cloneState(state);
  const cat = next.categories.find((c) => c.id === categoryId);
  if (cat) cat.name = name.trim() || cat.name;
  return saveSecEventFilterCatalog(next);
}

export { SEC_EVENT_FILTER_CATALOG_ROOT_ID };
