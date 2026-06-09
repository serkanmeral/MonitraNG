import { computed, ref } from 'vue';
import { useDatasetStore } from '@/stores/apps/dataset';
import { useDatasetCategoryStore } from '@/stores/apps/datasetCategory';

export type OcLookupDatasetItem = { title: string; value: string; subtitle?: string };

/**
 * Lookup kaynağı olarak seçilebilir DG dataset listesi.
 * isSystemCategory=true kategorilerindeki dataset'ler hariç tutulur.
 */
export function useOcLookupDatasetCatalog() {
  const datasetStore = useDatasetStore();
  const categoryStore = useDatasetCategoryStore();
  const loaded = ref(false);
  const loading = ref(false);
  const error = ref<string | null>(null);

  async function load(force = false) {
    if (loaded.value && !force) return;
    loading.value = true;
    error.value = null;
    try {
      await Promise.all([
        categoryStore.fetchCategories({ pageNumber: 1, pageSize: 500 }),
        datasetStore.fetchDatasets({ pageNumber: 1, pageSize: 500 }),
      ]);
      loaded.value = true;
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : String(e);
      throw e;
    } finally {
      loading.value = false;
    }
  }

  const systemCategoryIds = computed(() => {
    const ids = new Set<string>();
    for (const cat of categoryStore.categories) {
      if (cat.isSystemCategory) ids.add(cat.dataId);
    }
    return ids;
  });

  const selectableDatasets = computed((): OcLookupDatasetItem[] => {
    const blocked = systemCategoryIds.value;
    return datasetStore.datasets
      .filter((ds) => {
        if (!ds.category) return true;
        return !blocked.has(ds.category);
      })
      .map((ds) => ({
        title: ds.name,
        value: ds.name,
        subtitle: ds.description?.trim() || undefined,
      }))
      .sort((a, b) => a.title.localeCompare(b.title, 'tr'));
  });

  return {
    load,
    loaded,
    loading,
    error,
    selectableDatasets,
    systemCategoryIds,
  };
}
