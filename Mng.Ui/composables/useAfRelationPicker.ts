import { ref } from 'vue';
import { ocListDatasetPage } from '@/services/operationCoreService';

export type AfRelationPickerConfig = {
  dataset: string;
  valueField: string;
  labelField: string;
  pageSize?: number;
  filter?: string | null;
  excludeId?: string | null;
};

export type AfRelationPickerItem = {
  title: string;
  value: string;
  subtitle?: string;
};

/**
 * Automated Forms relation autocomplete — debounced search, server paging, seçili etiket önbelleği.
 */
export function useAfRelationPicker(getConfig: () => AfRelationPickerConfig) {
  const items = ref<AfRelationPickerItem[]>([]);
  const loading = ref(false);
  const labelCache = ref<Record<string, string>>({});

  let searchDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  let fetchGeneration = 0;

  function mapRows(rows: unknown[]): AfRelationPickerItem[] {
    const cfg = getConfig();
    const excludeId = cfg.excludeId?.trim();
    const mapped: AfRelationPickerItem[] = [];

    for (const row of rows) {
      if (!row || typeof row !== 'object') continue;
      const o = row as Record<string, unknown>;
      const value = String(o[cfg.valueField] ?? o.__dataId ?? o.dataId ?? '').trim();
      if (!value || (excludeId && value === excludeId)) continue;

      const title = String(o[cfg.labelField] ?? value).trim();
      const subtitle =
        cfg.labelField !== cfg.valueField
          ? `${cfg.valueField}: ${value}`
          : undefined;

      mapped.push({ title, value, subtitle });
      labelCache.value = { ...labelCache.value, [value]: title };
    }

    return mapped;
  }

  async function fetchPage(search = '') {
    if (import.meta.server) return;

    const cfg = getConfig();
    const dataset = cfg.dataset?.trim();
    if (!dataset) {
      items.value = [];
      return;
    }

    const generation = ++fetchGeneration;
    loading.value = true;
    try {
      const result = await ocListDatasetPage(dataset, {
        skip: 0,
        limit: cfg.pageSize ?? 25,
        filter: cfg.filter ?? undefined,
        search: search.trim() || undefined,
      });

      if (generation !== fetchGeneration) return;
      items.value = mapRows(result.items);
    } catch {
      if (generation !== fetchGeneration) return;
      items.value = [];
    } finally {
      if (generation === fetchGeneration) {
        loading.value = false;
      }
    }
  }

  async function ensureSelectedLabel(id: string | null | undefined) {
    const value = id?.trim();
    if (!value || import.meta.server || labelCache.value[value]) return;

    const cfg = getConfig();
    const dataset = cfg.dataset?.trim();
    if (!dataset) return;

    try {
      const result = await ocListDatasetPage(dataset, {
        limit: 1,
        filter: `${cfg.valueField}:eq:${value}`,
      });
      const mapped = mapRows(result.items);
      if (mapped.length) {
        labelCache.value = { ...labelCache.value, [value]: mapped[0].title };
        const exists = items.value.some((item) => item.value === value);
        if (!exists) {
          items.value = [mapped[0], ...items.value];
        }
      }
    } catch {
      // ignore — seçili değer etiketi yüklenemezse ham id gösterilir
    }
  }

  function onSearchUpdate(query: string) {
    if (searchDebounceTimer) clearTimeout(searchDebounceTimer);
    searchDebounceTimer = setTimeout(() => {
      void fetchPage(query);
    }, 300);
  }

  return {
    items,
    loading,
    labelCache,
    fetchPage,
    onSearchUpdate,
    ensureSelectedLabel,
  };
}
