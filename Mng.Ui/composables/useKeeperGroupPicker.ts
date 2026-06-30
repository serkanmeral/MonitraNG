import { ref } from 'vue';
import { useGroupStore } from '@/stores/apps/group';
import {
  KEEPER_DIRECTORY_PICKER_PAGE_SIZE,
  mapGroupToDirectoryRow,
  type KeeperDirectoryPickerRow,
  type KeeperGroupValueKey,
} from '@/utils/keeperDirectoryPicker';

export type KeeperGroupPickerOptions = {
  valueKey?: KeeperGroupValueKey;
};

/**
 * Keeper grup modal seçici — arama, sunucu sayfalama, by-ids etiket önbelleği.
 * Yalnızca aktif + uygulama kapsamındaki gruplar (fetchGroupsForSelection).
 */
export function useKeeperGroupPicker(options?: KeeperGroupPickerOptions) {
  const valueKey = options?.valueKey ?? 'id';
  const groupStore = useGroupStore();

  const items = ref<KeeperDirectoryPickerRow[]>([]);
  const loading = ref(false);
  const totalItems = ref(0);
  const page = ref(1);
  const itemsPerPage = ref(KEEPER_DIRECTORY_PICKER_PAGE_SIZE);
  const searchTerm = ref('');
  const labelCache = ref<Record<string, string>>({});

  let searchDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  let fetchGeneration = 0;

  function cacheLabels(rows: KeeperDirectoryPickerRow[]) {
    if (!rows.length) return;
    const next = { ...labelCache.value };
    for (const row of rows) {
      next[row.value] = row.title;
    }
    labelCache.value = next;
  }

  function mapGroupsFromStore(): KeeperDirectoryPickerRow[] {
    return groupStore.groups
      .map((g) => mapGroupToDirectoryRow(g, valueKey))
      .filter((x): x is KeeperDirectoryPickerRow => x != null);
  }

  async function fetchPage(options?: { page?: number; itemsPerPage?: number; search?: string }) {
    if (import.meta.server) return;

    const generation = ++fetchGeneration;
    const nextPage = options?.page ?? page.value;
    const nextItemsPerPage = options?.itemsPerPage ?? itemsPerPage.value;
    const nextSearch = options?.search ?? searchTerm.value;

    loading.value = true;
    try {
      await groupStore.fetchGroupsForSelection({
        page: nextPage,
        pageSize: nextItemsPerPage,
        search: nextSearch.trim() || undefined,
      });

      if (generation !== fetchGeneration) return;

      const mapped = mapGroupsFromStore();
      items.value = mapped;
      totalItems.value = groupStore.totalCount ?? mapped.length;
      page.value = groupStore.page ?? nextPage;
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

    if (valueKey === 'name') {
      await groupStore.fetchGroupsForSelection({ page: 1, pageSize: 500 });
      for (const name of missing) {
        const group = groupStore.groups.find(
          (g) =>
            (g.name ?? g.groupName ?? '').localeCompare(name, undefined, {
              sensitivity: 'accent',
            }) === 0
        );
        if (group) {
          const row = mapGroupToDirectoryRow(group, valueKey);
          if (row) {
            labelCache.value = { ...labelCache.value, [name]: row.title };
            continue;
          }
        }
        labelCache.value = { ...labelCache.value, [name]: name };
      }
      return;
    }

    const resolved = await groupStore.fetchGroupsByIds(missing);
    for (const group of resolved) {
      const row = mapGroupToDirectoryRow(group, valueKey);
      if (row) {
        labelCache.value = { ...labelCache.value, [row.value]: row.title };
      }
    }

    for (const id of missing) {
      if (!labelCache.value[id]) {
        const cached = groupStore.getGroupById(id);
        if (cached) {
          const row = mapGroupToDirectoryRow(cached, valueKey);
          if (row) {
            labelCache.value = { ...labelCache.value, [id]: row.title };
            continue;
          }
        }
        labelCache.value = { ...labelCache.value, [id]: id };
      }
    }
  }

  function labelFor(id: string): string {
    return labelCache.value[id] ?? id;
  }

  return {
    entity: 'group' as const,
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

export type KeeperGroupPickerApi = ReturnType<typeof useKeeperGroupPicker>;
