import { ref } from 'vue';
import { useUserStore } from '@/stores/apps/user';
import {
  collectPersonIdsFromValue,
  mapUserToOcPersonPickerItem,
  OC_PERSON_PICKER_LOAD_MORE_VALUE,
  OC_PERSON_PICKER_PAGE_SIZE,
  type OcPersonPickerItem,
} from '@/utils/ocPersonPicker';

function mergePersonItems(
  existing: OcPersonPickerItem[],
  incoming: OcPersonPickerItem[]
): OcPersonPickerItem[] {
  const map = new Map<string, OcPersonPickerItem>();
  for (const item of existing) map.set(item.value, item);
  for (const item of incoming) map.set(item.value, item);
  return [...map.values()].sort((a, b) => a.title.localeCompare(b.title, 'tr'));
}

/**
 * Keeper kullanıcı seçici — arama (debounce), sayfalama, seçili id çözümleme.
 */
export function useOcPersonPicker() {
  const userStore = useUserStore();

  const items = ref<OcPersonPickerItem[]>([]);
  const loading = ref(false);
  const loadingMore = ref(false);
  const page = ref(1);
  const totalPages = ref(1);
  const searchTerm = ref('');
  const hasMore = ref(false);
  /** Seçim sonrası arama sıfırlanınca listeden düşmesin diye tutulan id'ler. */
  const pinnedIds = ref<string[]>([]);

  let searchDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  let fetchGeneration = 0;

  function pinnedItems(): OcPersonPickerItem[] {
    if (!pinnedIds.value.length) return [];
    return items.value.filter((i) => pinnedIds.value.includes(i.value));
  }

  function setPinnedFromValues(values: unknown) {
    const ids = collectPersonIdsFromValue(values);
    if (!ids.length) return;
    const set = new Set([...pinnedIds.value, ...ids]);
    pinnedIds.value = [...set];
  }

  function mapUsersFromStore(): OcPersonPickerItem[] {
    return userStore.users
      .map((u) => mapUserToOcPersonPickerItem(u))
      .filter((x): x is OcPersonPickerItem => x != null);
  }

  async function fetchPage(options: { append: boolean; search: string }) {
    if (import.meta.server) return;
    const generation = ++fetchGeneration;
    const nextPage = options.append ? page.value + 1 : 1;

    if (options.append) loadingMore.value = true;
    else loading.value = true;

    try {
      await userStore.fetchUsersForSelection({
        page: nextPage,
        pageSize: OC_PERSON_PICKER_PAGE_SIZE,
        search: options.search.trim() || undefined,
      });

      if (generation !== fetchGeneration) return;

      const mapped = mapUsersFromStore();
      if (options.append) {
        items.value = mergePersonItems(items.value, mapped);
      } else {
        items.value = mergePersonItems(pinnedItems(), mapped);
      }

      page.value = userStore.page || nextPage;
      totalPages.value = userStore.totalPages || 1;
      hasMore.value = page.value < totalPages.value;
    } catch {
      if (generation !== fetchGeneration) return;
      if (!options.append) items.value = [];
      hasMore.value = false;
    } finally {
      if (generation === fetchGeneration) {
        loading.value = false;
        loadingMore.value = false;
      }
    }
  }

  async function resetAndFetch(search = '') {
    searchTerm.value = search;
    page.value = 1;
    await fetchPage({ append: false, search });
  }

  function onSearchUpdate(query: string | null) {
    const q = (query ?? '').trim();
    if (searchDebounceTimer) clearTimeout(searchDebounceTimer);
    searchDebounceTimer = setTimeout(() => {
      if (q === searchTerm.value) return;
      void resetAndFetch(q);
    }, 300);
  }

  async function loadMore() {
    if (!hasMore.value || loading.value || loadingMore.value) return;
    await fetchPage({ append: true, search: searchTerm.value });
  }

  async function ensureSelectedIds(ids: string[]) {
    if (import.meta.server || !ids.length) return;

    const missing = ids.filter((id) => id && !items.value.some((i) => i.value === id));
    if (!missing.length) return;

    const resolved: OcPersonPickerItem[] = [];
    await Promise.all(
      missing.map(async (id) => {
        const cached = userStore.getUserById(id);
        let user = cached;
        if (!user) {
          try {
            await userStore.fetchUserById(id);
            user = userStore.getUserById(id);
          } catch {
            user = undefined;
          }
        }
        const mapped = user ? mapUserToOcPersonPickerItem(user) : null;
        if (mapped) {
          resolved.push({ ...mapped, value: id });
        } else {
          resolved.push({ value: id, title: id, subtitle: '' });
        }
      })
    );

    if (resolved.length) {
      items.value = mergePersonItems(items.value, resolved);
    }
    setPinnedFromValues(ids);
  }

  /** Seçim anında etiket kaybolmasın (menü kapanınca boş arama listeyi yeniler). */
  async function onSelectionChanged(value: unknown) {
    const ids = collectPersonIdsFromValue(value);
    setPinnedFromValues(value);
    if (ids.length) await ensureSelectedIds(ids);
  }

  function itemsWithLoadMoreRow(
    loadMoreLabel: string,
    loadMoreHint: string
  ): OcPersonPickerItem[] {
    const base = items.value;
    if (!hasMore.value) return base;
    return [
      ...base,
      {
        title: loadMoreLabel,
        subtitle: loadMoreHint,
        value: OC_PERSON_PICKER_LOAD_MORE_VALUE,
      },
    ];
  }

  function isLoadMoreValue(value: unknown): boolean {
    return value === OC_PERSON_PICKER_LOAD_MORE_VALUE;
  }

  return {
    items,
    loading,
    loadingMore,
    hasMore,
    searchTerm,
    resetAndFetch,
    onSearchUpdate,
    loadMore,
    ensureSelectedIds,
    onSelectionChanged,
    setPinnedFromValues,
    itemsWithLoadMoreRow,
    isLoadMoreValue,
    OC_PERSON_PICKER_LOAD_MORE_VALUE,
  };
}

export type OcPersonPickerApi = ReturnType<typeof useOcPersonPicker>;
