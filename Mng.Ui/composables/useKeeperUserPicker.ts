import { ref } from 'vue';
import { useUserStore } from '@/stores/apps/user';
import {
  fetchAllMatchingDirectoryRows,
  KEEPER_DIRECTORY_PICKER_PAGE_SIZE,
  mapUserToDirectoryRow,
  type KeeperDirectoryPickerRow,
} from '@/utils/keeperDirectoryPicker';

/**
 * Keeper kullanıcı modal seçici — arama, sunucu sayfalama, by-ids etiket önbelleği.
 * Yalnızca aktif + uygulama kapsamındaki kullanıcılar (fetchUsersForSelection).
 */
export function useKeeperUserPicker() {
  const userStore = useUserStore();

  const items = ref<KeeperDirectoryPickerRow[]>([]);
  const loading = ref(false);
  const totalItems = ref(0);
  const page = ref(1);
  const itemsPerPage = ref(KEEPER_DIRECTORY_PICKER_PAGE_SIZE);
  const searchTerm = ref('');
  const sortBy = ref<string | undefined>('firstName');
  const sortOrder = ref<'asc' | 'desc' | undefined>('asc');
  const labelCache = ref<Record<string, string>>({});

  const sortFields: Record<string, string> = {
    title: 'firstName',
    extra_username: 'username',
    extra_email: 'email',
    extra_department: 'department',
  };

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

  function mapUsersFromStore(): KeeperDirectoryPickerRow[] {
    return userStore.users
      .map((u) => mapUserToDirectoryRow(u))
      .filter((x): x is KeeperDirectoryPickerRow => x != null);
  }

  async function fetchPage(options?: {
    page?: number;
    itemsPerPage?: number;
    search?: string;
    sortBy?: string;
    sortOrder?: 'asc' | 'desc';
  }) {
    if (import.meta.server) return;

    const generation = ++fetchGeneration;
    const nextPage = options?.page ?? page.value;
    const nextItemsPerPage = options?.itemsPerPage ?? itemsPerPage.value;
    const nextSearch = options?.search ?? searchTerm.value;
    const nextSortBy = options && 'sortBy' in options ? options.sortBy : sortBy.value;
    const nextSortOrder = options && 'sortOrder' in options ? options.sortOrder : sortOrder.value;

    loading.value = true;
    try {
      await userStore.fetchUsersForSelection({
        page: nextPage,
        pageSize: nextItemsPerPage,
        search: nextSearch.trim() || undefined,
        sortBy: nextSortBy,
        sortOrder: nextSortOrder,
      });

      if (generation !== fetchGeneration) return;

      const mapped = mapUsersFromStore();
      items.value = mapped;
      totalItems.value = userStore.totalCount ?? mapped.length;
      page.value = userStore.page ?? nextPage;
      itemsPerPage.value = nextItemsPerPage;
      searchTerm.value = nextSearch;
      sortBy.value = nextSortBy;
      sortOrder.value = nextSortOrder;
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

  async function onTableOptionsUpdate(options: {
    page: number;
    itemsPerPage: number;
    sortBy?: { key: string; order?: boolean | 'asc' | 'desc' }[];
  }) {
    const primary = options.sortBy?.find((item) => item?.key);
    const field = primary?.key ? sortFields[primary.key] : undefined;
    const order = primary?.order === 'desc' ? 'desc' : primary?.order === 'asc' ? 'asc' : undefined;
    await fetchPage({
      page: options.page,
      itemsPerPage: options.itemsPerPage,
      sortBy: field,
      sortOrder: field ? order ?? 'asc' : undefined,
    });
  }

  async function ensureSelectedLabels(ids: string[]) {
    if (import.meta.server || !ids.length) return;

    const missing = ids.filter((id) => id && !labelCache.value[id]);
    if (!missing.length) return;

    const resolved = await userStore.fetchUsersByIds(missing);
    for (const user of resolved) {
      const row = mapUserToDirectoryRow(user);
      if (row) {
        labelCache.value = { ...labelCache.value, [row.value]: row.title };
      }
    }

    for (const id of missing) {
      if (!labelCache.value[id]) {
        const cached = userStore.getUserById(id);
        if (cached) {
          const row = mapUserToDirectoryRow(cached);
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

  async function fetchAllMatchingRows(): Promise<KeeperDirectoryPickerRow[]> {
    if (import.meta.server) return [];

    const search = searchTerm.value.trim() || undefined;
    const savedPage = page.value;
    const savedPageSize = itemsPerPage.value;

    const rows = await fetchAllMatchingDirectoryRows({
      totalItems: totalItems.value,
      getBatchTotal: () => userStore.totalCount ?? totalItems.value,
      fetchBatch: async (batchPage, batchSize) => {
        await userStore.fetchUsersForSelection({
          page: batchPage,
          pageSize: batchSize,
          search,
          sortBy: sortBy.value,
          sortOrder: sortOrder.value,
        });
        return mapUsersFromStore();
      },
    });

    cacheLabels(rows);
    await fetchPage({ page: savedPage, itemsPerPage: savedPageSize, search: searchTerm.value });
    return rows;
  }

  return {
    entity: 'user' as const,
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
    fetchAllMatchingRows,
  };
}

export type KeeperUserPickerApi = ReturnType<typeof useKeeperUserPicker>;
