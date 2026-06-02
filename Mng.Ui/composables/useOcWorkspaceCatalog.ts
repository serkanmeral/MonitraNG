import { inject, ref, watch, type InjectionKey, type Ref } from 'vue';
import {
  ocExtractDgErrorMessage,
  ocGetWorkspace,
  ocListBoardsForWorkspace,
  ocListPrioritiesForWorkspace,
  ocListStatesForWorkspace,
  ocListWorkItemTypesForWorkspace,
} from '@/services/operationCoreService';
import type {
  OpBoard,
  OpPriority,
  OpState,
  OpWorkItemType,
  OpWorkspaceDetail,
} from '@/types/apps/operationCore';

export type OcWorkspaceCatalogContext = ReturnType<typeof useOcWorkspaceCatalog>;

export const OC_WORKSPACE_CATALOG_KEY: InjectionKey<OcWorkspaceCatalogContext> =
  Symbol('ocWorkspaceCatalog');

type CatalogSnapshot = {
  boards: OpBoard[];
  types: OpWorkItemType[];
  priorities: OpPriority[];
  states: OpState[];
  workspace: OpWorkspaceDetail | null;
};

const inflight = new Map<string, Promise<CatalogSnapshot>>();

async function fetchCatalogSnapshot(workspaceId: string, force = false): Promise<CatalogSnapshot> {
  if (force) {
    inflight.delete(workspaceId);
  }

  const pending = inflight.get(workspaceId);
  if (pending) {
    return pending;
  }

  const promise = (async () => {
    const [boards, types, priorities, states, workspace] = await Promise.all([
      ocListBoardsForWorkspace(workspaceId),
      ocListWorkItemTypesForWorkspace(workspaceId, { fallbackAll: true }),
      ocListPrioritiesForWorkspace(workspaceId, { fallbackAll: true }),
      ocListStatesForWorkspace(workspaceId, { fallbackAll: true, includeFlowStates: true }),
      ocGetWorkspace(workspaceId),
    ]);
    return { boards, types, priorities, states, workspace };
  })();

  inflight.set(workspaceId, promise);
  try {
    return await promise;
  } finally {
    if (inflight.get(workspaceId) === promise) {
      inflight.delete(workspaceId);
    }
  }
}

/**
 * Workspace tanımlama ekranı — paylaşımlı katalog (boards, types, priorities, states, workspace).
 * Tek workspaceId için istekler birleştirilir; sekmeler inject ile paylaşır.
 */
export function useOcWorkspaceCatalog(workspaceId: Ref<string>) {
  const boards = ref<OpBoard[]>([]);
  const types = ref<OpWorkItemType[]>([]);
  const priorities = ref<OpPriority[]>([]);
  const states = ref<OpState[]>([]);
  const workspace = ref<OpWorkspaceDetail | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  let loadGeneration = 0;
  let loadedForId = '';

  function clearCatalog() {
    boards.value = [];
    types.value = [];
    priorities.value = [];
    states.value = [];
    workspace.value = null;
    loadedForId = '';
  }

  async function load(force = false) {
    const id = workspaceId.value?.trim() ?? '';
    if (!id) {
      clearCatalog();
      error.value = null;
      loading.value = false;
      return;
    }

    if (!force && loadedForId === id && !error.value) {
      return;
    }

    const gen = ++loadGeneration;
    loading.value = true;
    error.value = null;

    try {
      const snap = await fetchCatalogSnapshot(id, force);
      if (gen !== loadGeneration) return;

      boards.value = snap.boards;
      types.value = snap.types;
      priorities.value = snap.priorities;
      states.value = snap.states;
      workspace.value = snap.workspace;
      loadedForId = id;
    } catch (e: unknown) {
      if (gen !== loadGeneration) return;
      clearCatalog();
      error.value = ocExtractDgErrorMessage(e, 'Workspace katalog yüklenemedi.');
    } finally {
      if (gen === loadGeneration) {
        loading.value = false;
      }
    }
  }

  async function whenReady(): Promise<void> {
    const id = workspaceId.value?.trim() ?? '';
    if (!id) return;

    if (loadedForId !== id && !loading.value) {
      await load();
    }

    if (loading.value) {
      await new Promise<void>((resolve) => {
        const stop = watch(loading, (busy) => {
          if (!busy) {
            stop();
            resolve();
          }
        });
      });
    }

    if (error.value) {
      throw new Error(error.value);
    }
  }

  async function refresh() {
    await load(true);
  }

  watch(
    workspaceId,
    () => {
      void load();
    },
    { immediate: true }
  );

  return {
    boards,
    types,
    priorities,
    states,
    workspace,
    loading,
    error,
    refresh,
    whenReady,
    ensureLoaded: () => load(),
  };
}

export function useOcWorkspaceCatalogInject(): OcWorkspaceCatalogContext {
  const ctx = inject(OC_WORKSPACE_CATALOG_KEY);
  if (!ctx) {
    throw new Error('useOcWorkspaceCatalogInject: OC_WORKSPACE_CATALOG_KEY provider missing.');
  }
  return ctx;
}
