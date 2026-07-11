import { ref } from 'vue';
import { ocListDatasetPage } from '@/services/operationCoreService';
import {
  buildLookupColumnFilterClause,
  formatLookupSelectionLabel,
  type OcLookupColumn,
  type OcLookupDefaultSort,
} from '@/utils/ocLookupFieldOptions';

export type OcDatasetPickerConfig = {
  dataset: string;
  valueField: string;
  labelField: string;
  pageSize: number;
  baseFilter?: string | null;
  dependsOnFilter?: string | null;
  searchFields?: string[];
  defaultSort?: OcLookupDefaultSort | null;
  /** Used to build column filter clauses (enum → eq, text → contains). */
  columns?: OcLookupColumn[];
  /** Chip / summary fields (joined); falls back to labelField. */
  displayFields?: string[];
  displaySeparator?: string;
};

export type OcDatasetPickerRow = {
  title: string;
  value: string;
  raw: Record<string, unknown>;
};

export type OcDatasetPickerTableOptions = {
  page: number;
  itemsPerPage: number;
  sortBy?: Array<{ key: string; order: 'asc' | 'desc' }>;
};

/**
 * Dataset relation modal picker — arama, kolon filtresi, sunucu sayfalama/sıralama, expand etiketleri.
 */
export function useOcDatasetPicker(getConfig: () => OcDatasetPickerConfig) {
  const items = ref<OcDatasetPickerRow[]>([]);
  const loading = ref(false);
  const totalItems = ref(0);
  const page = ref(1);
  const itemsPerPage = ref(20);
  const searchTerm = ref('');
  const sortBy = ref<Array<{ key: string; order: 'asc' | 'desc' }>>([]);
  /** field → filter text */
  const columnFilters = ref<Record<string, string>>({});
  const labelCache = ref<Record<string, string>>({});

  let searchDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  let columnFilterDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  let fetchGeneration = 0;

  function buildFilter(): string | undefined {
    const cfg = getConfig();
    const parts: string[] = [];
    if (cfg.baseFilter) parts.push(cfg.baseFilter);
    if (cfg.dependsOnFilter) parts.push(cfg.dependsOnFilter);

    const cols = cfg.columns ?? [];
    const colByField = new Map(cols.map((c) => [c.field, c]));
    for (const [field, rawVal] of Object.entries(columnFilters.value)) {
      const col = colByField.get(field) ?? { field };
      const clause = buildLookupColumnFilterClause(col, rawVal);
      if (clause) parts.push(clause);
    }

    return parts.length ? parts.join(',') : undefined;
  }

  function buildSortParam(): string | undefined {
    if (sortBy.value.length) {
      return sortBy.value
        .map((s) => `${s.key}:${s.order === 'desc' ? 'desc' : 'asc'}`)
        .join(',');
    }
    const def = getConfig().defaultSort;
    if (def?.field) {
      return `${def.field}:${def.dir === 'desc' ? 'desc' : 'asc'}`;
    }
    return undefined;
  }

  function rowsToItems(rows: unknown[]): OcDatasetPickerRow[] {
    const cfg = getConfig();
    const out: OcDatasetPickerRow[] = [];
    for (const row of rows) {
      if (!row || typeof row !== 'object') continue;
      const o = row as Record<string, unknown>;
      const id = String(o[cfg.valueField] ?? o.__dataId ?? o.dataId ?? '').trim();
      if (!id) continue;
      const title = formatLookupSelectionLabel(o, {
        labelField: cfg.labelField,
        columns: cfg.columns,
        displayFields: cfg.displayFields,
        displaySeparator: cfg.displaySeparator,
        fallbackId: id,
      });
      out.push({ title: title || id, value: id, raw: o });
    }
    return out;
  }

  function cacheLabels(rows: OcDatasetPickerRow[]) {
    if (!rows.length) return;
    const next = { ...labelCache.value };
    for (const row of rows) {
      next[row.value] = row.title;
    }
    labelCache.value = next;
  }

  async function fetchPage(options?: {
    page?: number;
    itemsPerPage?: number;
    search?: string;
    sortBy?: Array<{ key: string; order: 'asc' | 'desc' }>;
  }) {
    if (import.meta.server) return;

    const cfg = getConfig();
    const dataset = cfg.dataset?.trim();
    if (!dataset) {
      items.value = [];
      totalItems.value = 0;
      return;
    }

    const generation = ++fetchGeneration;
    const nextPage = options?.page ?? page.value;
    const nextItemsPerPage = options?.itemsPerPage ?? itemsPerPage.value;
    const nextSearch = options?.search ?? searchTerm.value;
    if (options?.sortBy) sortBy.value = options.sortBy;

    loading.value = true;
    try {
      const skip = (nextPage - 1) * nextItemsPerPage;
      const result = await ocListDatasetPage(dataset, {
        skip,
        limit: nextItemsPerPage,
        filter: buildFilter(),
        search: nextSearch.trim() || undefined,
        sort: buildSortParam(),
        expand: true,
      });

      if (generation !== fetchGeneration) return;

      const mapped = rowsToItems(result.items);
      items.value = mapped;
      totalItems.value = result.total;
      page.value = nextPage;
      itemsPerPage.value = nextItemsPerPage;
      searchTerm.value = nextSearch;
      cacheLabels(mapped);
    } catch {
      if (generation !== fetchGeneration) return;
      items.value = [];
      totalItems.value = 0;
    } finally {
      if (generation === fetchGeneration) {
        loading.value = false;
      }
    }
  }

  async function resetAndFetch(search = '') {
    page.value = 1;
    const def = getConfig().defaultSort;
    if (!sortBy.value.length && def?.field) {
      sortBy.value = [{ key: def.field, order: def.dir === 'desc' ? 'desc' : 'asc' }];
    }
    await fetchPage({ page: 1, search });
  }

  function onSearchUpdate(query: string) {
    if (searchDebounceTimer) clearTimeout(searchDebounceTimer);
    searchDebounceTimer = setTimeout(() => {
      const q = query.trim();
      if (q === searchTerm.value.trim()) return;
      void resetAndFetch(query);
    }, 300);
  }

  function setColumnFilter(field: string, value: string) {
    const next = { ...columnFilters.value };
    const trimmed = value.trim();
    if (!trimmed) delete next[field];
    else next[field] = value;
    columnFilters.value = next;

    if (columnFilterDebounceTimer) clearTimeout(columnFilterDebounceTimer);
    columnFilterDebounceTimer = setTimeout(() => {
      page.value = 1;
      void fetchPage({ page: 1 });
    }, 300);
  }

  function clearColumnFilters() {
    columnFilters.value = {};
    page.value = 1;
    void fetchPage({ page: 1 });
  }

  async function onTableOptionsUpdate(options: OcDatasetPickerTableOptions) {
    const nextSort = options.sortBy?.length
      ? options.sortBy.map((s) => ({
          key: s.key,
          order: (s.order === 'desc' ? 'desc' : 'asc') as 'asc' | 'desc',
        }))
      : sortBy.value;
    await fetchPage({
      page: options.page,
      itemsPerPage: options.itemsPerPage,
      sortBy: nextSort,
    });
  }

  async function ensureSelectedLabels(ids: string[]) {
    if (import.meta.server || !ids.length) return;

    const missing = ids.filter((id) => id && !labelCache.value[id]);
    if (!missing.length) return;

    const cfg = getConfig();
    const dataset = cfg.dataset?.trim();
    if (!dataset) return;

    await Promise.all(
      missing.map(async (id) => {
        try {
          const result = await ocListDatasetPage(dataset, {
            limit: 1,
            filter: `${cfg.valueField}:eq:${id}`,
            expand: true,
          });
          const mapped = rowsToItems(result.items);
          cacheLabels(mapped);
          if (!labelCache.value[id]) {
            labelCache.value = { ...labelCache.value, [id]: id };
          }
        } catch {
          labelCache.value = { ...labelCache.value, [id]: id };
        }
      })
    );
  }

  function labelFor(id: string): string {
    return labelCache.value[id] ?? id;
  }

  return {
    items,
    loading,
    totalItems,
    page,
    itemsPerPage,
    searchTerm,
    sortBy,
    columnFilters,
    labelFor,
    resetAndFetch,
    onSearchUpdate,
    setColumnFilter,
    clearColumnFilters,
    onTableOptionsUpdate,
    ensureSelectedLabels,
  };
}

export type OcDatasetPickerApi = ReturnType<typeof useOcDatasetPicker>;
