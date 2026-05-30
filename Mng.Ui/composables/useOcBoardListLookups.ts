import { computed, ref, watch, type Ref } from 'vue';
import {
  ocListPrioritiesForWorkspace,
  ocListStatesForWorkspace,
  ocListWorkItemTypesForWorkspace,
} from '@/services/operationCoreService';
import type {
  OcBoardCatalogs,
  OcCatalogDisplayEntry,
  OcPersonDisplay,
  OpPriority,
  OpState,
  OpWorkItemType,
} from '@/types/apps/operationCore';
import {
  buildCatalogDisplayMap,
  resolveCatalogDisplayItem,
  type OcCatalogDisplayItem,
} from '@/utils/ocCatalogDisplay';

type CatalogEntryRecord = Record<string, OcCatalogDisplayEntry>;

function entriesToDisplayMap(record: CatalogEntryRecord | undefined): Map<string, OcCatalogDisplayItem> {
  const map = new Map<string, OcCatalogDisplayItem>();
  if (!record) return map;
  for (const [id, entry] of Object.entries(record)) {
    map.set(id, {
      id: entry.id || id,
      name: entry.name ?? '',
      color: entry.color ?? null,
      icon: entry.icon ?? null,
    });
  }
  return map;
}

/**
 * Board liste tablosu — durum/öncelik/tip katalogları ve atanan kişi etiketleri.
 *
 * `catalogsSource` (board context map'leri) verildiğinde client-side join yapılmaz;
 * map'ler doğrudan MO cache'inden gelen veriyle kurulur. Yoksa workspace bazlı fetch'e düşer.
 */
export function useOcBoardListLookups(
  workspaceId: Ref<string | undefined | null>,
  catalogsSource?: Ref<OcBoardCatalogs | null | undefined>,
  peopleSource?: Ref<Record<string, OcPersonDisplay> | null | undefined>
) {
  const states = ref<OpState[]>([]);
  const priorities = ref<OpPriority[]>([]);
  const types = ref<OpWorkItemType[]>([]);
  const loadingCatalogs = ref(false);

  function contextMap(kind: keyof OcBoardCatalogs): Map<string, OcCatalogDisplayItem> | null {
    const src = catalogsSource?.value;
    const record = src?.[kind];
    if (!record || Object.keys(record).length === 0) return null;
    return entriesToDisplayMap(record);
  }

  // Her context map'i yalnız catalogsSource değişince bir kez kur (computed cache'i).
  // Eskiden hasContextCatalogs + her *ById çağrısı contextMap'i tekrar çalıştırıp Map'i
  // baştan üretiyordu (kaynak başına ~6 kurulum).
  const statesContext = computed(() => contextMap('states'));
  const prioritiesContext = computed(() => contextMap('priorities'));
  const typesContext = computed(() => contextMap('types'));

  const hasContextCatalogs = computed(
    () => !!(statesContext.value || prioritiesContext.value || typesContext.value)
  );

  const stateById = computed(() => statesContext.value ?? buildCatalogDisplayMap(states.value));
  const priorityById = computed(() => prioritiesContext.value ?? buildCatalogDisplayMap(priorities.value));
  const typeById = computed(() => typesContext.value ?? buildCatalogDisplayMap(types.value));

  async function loadCatalogs() {
    // Board context map'leri mevcutsa ayrı fetch'e gerek yok (cache'ten geliyor).
    if (hasContextCatalogs.value) {
      return;
    }

    const id = workspaceId.value?.trim();
    if (!id) {
      states.value = [];
      priorities.value = [];
      types.value = [];
      return;
    }

    loadingCatalogs.value = true;
    try {
      const [stateRows, priorityRows, typeRows] = await Promise.all([
        ocListStatesForWorkspace(id, { fallbackAll: true }),
        ocListPrioritiesForWorkspace(id, { fallbackAll: true }),
        ocListWorkItemTypesForWorkspace(id, { fallbackAll: true }),
      ]);
      states.value = stateRows;
      priorities.value = priorityRows;
      types.value = typeRows;
    } catch {
      states.value = [];
      priorities.value = [];
      types.value = [];
    } finally {
      loadingCatalogs.value = false;
    }
  }

  watch(
    [workspaceId, hasContextCatalogs],
    () => {
      void loadCatalogs();
    },
    { immediate: true }
  );

  function resolveState(id: string | null | undefined, fallbackName?: string | null): OcCatalogDisplayItem | null {
    return resolveCatalogDisplayItem(stateById.value, id, fallbackName);
  }

  function resolvePriority(id: string | null | undefined): OcCatalogDisplayItem | null {
    return resolveCatalogDisplayItem(priorityById.value, id);
  }

  function resolveType(id: string | null | undefined): OcCatalogDisplayItem | null {
    return resolveCatalogDisplayItem(typeById.value, id);
  }

  /** Tek bir person id'sini MO people map'inden ada çevirir. */
  function resolvePersonName(id: string | null | undefined): string {
    const key = id?.trim();
    if (!key) return '—';
    const hit = peopleSource?.value?.[key];
    return hit?.name?.trim() || key;
  }

  /** assignee (tek person) — geriye dönük isim. */
  function resolveAssigneeName(id: string | null | undefined): string {
    return resolvePersonName(id);
  }

  /** Pool person alan değeri (tek veya çoklu) — "Ad, Ad" olarak çözer. */
  function resolvePersonValue(value: unknown): string {
    const ids = collectPersonIds(value);
    if (!ids.length) return '—';
    const names = ids.map((id) => resolvePersonName(id)).filter((n) => n && n !== '—');
    return names.length ? names.join(', ') : '—';
  }

  return {
    loadingCatalogs,
    stateById,
    priorityById,
    typeById,
    resolveState,
    resolvePriority,
    resolveType,
    resolveAssigneeName,
    resolvePersonName,
    resolvePersonValue,
  };
}

function collectPersonIds(value: unknown): string[] {
  if (value === null || value === undefined || value === '') return [];
  if (Array.isArray(value)) {
    return value.flatMap((v) => collectPersonIds(v));
  }
  if (typeof value === 'object') {
    const o = value as Record<string, unknown>;
    const id = o.__dataId ?? o.id ?? o.userId;
    return id != null ? [String(id).trim()].filter(Boolean) : [];
  }
  const s = String(value).trim();
  return s ? [s] : [];
}
