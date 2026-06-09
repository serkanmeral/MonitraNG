import { ref } from 'vue';
import { ocListDatasetPage } from '@/services/operationCoreService';
import { recordToDatasetItems } from '@/utils/ocDynamicFormField';

export type OcDatasetPickerConfig = {
  dataset: string;
  valueField: string;
  labelField: string;
  pageSize: number;
  baseFilter?: string | null;
  dependsOnFilter?: string | null;
  searchFields?: string[];
};

export type OcDatasetPickerRow = {
  title: string;
  value: string;
  raw: Record<string, unknown>;
};

/**
 * Dataset relation modal picker — arama (debounce), sunucu sayfalama, seçili id etiket önbelleği.
 */
export function useOcDatasetPicker(getConfig: () => OcDatasetPickerConfig) {
  const items = ref<OcDatasetPickerRow[]>([]);
  const loading = ref(false);
  const totalItems = ref(0);
  const page = ref(1);
  const itemsPerPage = ref(20);
  const searchTerm = ref('');
  const labelCache = ref<Record<string, string>>({});

  let searchDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  let fetchGeneration = 0;

  function buildFilter(): string | undefined {
    const cfg = getConfig();
    const parts = [cfg.baseFilter, cfg.dependsOnFilter].filter(Boolean);
    return parts.length ? parts.join(',') : undefined;
  }

  function rowsToItems(rows: unknown[]): OcDatasetPickerRow[] {
    const cfg = getConfig();
    const selectItems = recordToDatasetItems(rows, {
      idKey: cfg.valueField,
      labelKey: cfg.labelField,
    });
    const rawById = new Map<string, Record<string, unknown>>();
    for (const row of rows) {
      if (!row || typeof row !== 'object') continue;
      const o = row as Record<string, unknown>;
      const id = String(o[cfg.valueField] ?? o.__dataId ?? o.dataId ?? '').trim();
      if (id) rawById.set(id, o);
    }
    return selectItems.map((si) => ({
      ...si,
      raw: rawById.get(si.value) ?? {},
    }));
  }

  function cacheLabels(rows: OcDatasetPickerRow[]) {
    if (!rows.length) return;
    const next = { ...labelCache.value };
    for (const row of rows) {
      next[row.value] = row.title;
    }
    labelCache.value = next;
  }

  async function fetchPage(options?: { page?: number; itemsPerPage?: number; search?: string }) {
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

    loading.value = true;
    try {
      const skip = (nextPage - 1) * nextItemsPerPage;
      const result = await ocListDatasetPage(dataset, {
        skip,
        limit: nextItemsPerPage,
        filter: buildFilter(),
        search: nextSearch.trim() || undefined,
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

  async function onTableOptionsUpdate(options: { page: number; itemsPerPage: number }) {
    await fetchPage({ page: options.page, itemsPerPage: options.itemsPerPage });
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
    labelFor,
    resetAndFetch,
    onSearchUpdate,
    onTableOptionsUpdate,
    ensureSelectedLabels,
  };
}

export type OcDatasetPickerApi = ReturnType<typeof useOcDatasetPicker>;
