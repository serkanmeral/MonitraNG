import { defineStore } from 'pinia';
import {
  ocBuildColumnQueryRequest,
  ocExecuteQuery,
  ocGetBoardContext,
  ocGetBoardListPage,
  ocListBoardsForWorkspace,
  ocListWorkspaces,
  ocOperationsLive,
} from '@/services/operationCoreService';
import type {
  OcBoardColumn,
  OcBoardListRequest,
  OcBoardRuntimeContext,
  OcColumnItemsState,
  OcPersonDisplay,
  OcWorkItemCard,
  OpBoard,
  OpWorkspace,
} from '@/types/apps/operationCore';

const emptyColumnState = (): OcColumnItemsState => ({ items: [], total: 0, error: null });

export const useOperationCoreStore = defineStore('operationCore', {
  state: () => ({
    workspaces: [] as OpWorkspace[],
    boardsByWorkspace: {} as Record<string, OpBoard[]>,
    loadingWorkspaces: false,
    loadingBoards: {} as Record<string, boolean>,
    error: null as string | null,
    operationsLive: null as boolean | null,

    activeBoardId: null as string | null,
    boardContext: null as OcBoardRuntimeContext | null,
    columnItems: {} as Record<string, OcColumnItemsState>,
    columnLoading: {} as Record<string, boolean>,
    boardPeople: {} as Record<string, OcPersonDisplay>,
    loadingBoardContext: false,
    boardError: null as string | null,

    // Liste görünümü (server-side sayfalama/sıralama/filtre/arama)
    listItems: [] as OcWorkItemCard[],
    listTotal: 0,
    listLoading: false,
    listError: null as string | null,
  }),

  getters: {
    boardsForWorkspace: (state) => (workspaceId: string) => state.boardsByWorkspace[workspaceId] ?? [],

    isListPreferred: (state) => (state.boardContext?.viewType ?? 'list') !== 'kanban',

    allBoardItems(state): { item: OcColumnItemsState['items'][number]; columnTitle: string }[] {
      if (!state.boardContext) return [];
      const seen = new Set<string>();
      const out: { item: OcColumnItemsState['items'][number]; columnTitle: string }[] = [];
      for (const col of state.boardContext.columns) {
        const bucket = state.columnItems[col.stateId];
        for (const item of bucket?.items ?? []) {
          if (seen.has(item.id)) continue;
          seen.add(item.id);
          out.push({ item, columnTitle: col.title || col.stateId });
        }
      }
      return out;
    },
  },

  actions: {
    async loadWorkspaces() {
      this.loadingWorkspaces = true;
      this.error = null;
      try {
        this.workspaces = await ocListWorkspaces();
      } catch (e: unknown) {
        this.error = e instanceof Error ? e.message : String(e);
        this.workspaces = [];
      } finally {
        this.loadingWorkspaces = false;
      }
    },

    async loadBoardsForWorkspace(workspaceId: string, force = false) {
      if (!workspaceId) return;
      if (!force && this.boardsByWorkspace[workspaceId]) return;

      this.loadingBoards = { ...this.loadingBoards, [workspaceId]: true };
      this.error = null;
      try {
        const boards = await ocListBoardsForWorkspace(workspaceId);
        this.boardsByWorkspace = { ...this.boardsByWorkspace, [workspaceId]: boards };
      } catch (e: unknown) {
        this.error = e instanceof Error ? e.message : String(e);
        this.boardsByWorkspace = { ...this.boardsByWorkspace, [workspaceId]: [] };
      } finally {
        this.loadingBoards = { ...this.loadingBoards, [workspaceId]: false };
      }
    },

    async loadAllBoards() {
      await Promise.all(this.workspaces.map((w) => this.loadBoardsForWorkspace(w.__dataId, true)));
    },

    async pingOperations() {
      try {
        await ocOperationsLive();
        this.operationsLive = true;
      } catch {
        this.operationsLive = false;
      }
    },

    clearBoardState() {
      this.activeBoardId = null;
      this.boardContext = null;
      this.columnItems = {};
      this.columnLoading = {};
      this.boardPeople = {};
      this.boardError = null;
      this.listItems = [];
      this.listTotal = 0;
      this.listError = null;
    },

    async loadBoard(boardId: string, force = false) {
      if (!boardId) return;
      if (!force && this.activeBoardId === boardId && this.boardContext) {
        return;
      }

      this.activeBoardId = boardId;
      this.loadingBoardContext = true;
      this.boardError = null;
      this.columnItems = {};
      this.columnLoading = {};
      this.boardPeople = {};

      this.listItems = [];
      this.listTotal = 0;
      this.listError = null;

      try {
        const ctx = await ocGetBoardContext(boardId);
        this.boardContext = ctx;
        // Liste görünümü server-side sayfalama kullanır (kolonları toplu yüklemez); kanban kolon sorgularıyla çalışır.
        const isList = (ctx.viewType ?? 'list') !== 'kanban';
        if (!isList) {
          await this.loadAllColumns(ctx.columns);
        }
      } catch (e: unknown) {
        this.boardContext = null;
        this.boardError = e instanceof Error ? e.message : String(e);
      } finally {
        this.loadingBoardContext = false;
      }
    },

    async loadBoardListPage(request: OcBoardListRequest) {
      if (!this.activeBoardId) return;
      this.listLoading = true;
      this.listError = null;
      try {
        const res = await ocGetBoardListPage(this.activeBoardId, request);
        this.listItems = res.items;
        this.listTotal = res.total;
        if (res.people && Object.keys(res.people).length > 0) {
          this.boardPeople = { ...this.boardPeople, ...res.people };
        }
      } catch (e: unknown) {
        this.listItems = [];
        this.listTotal = 0;
        this.listError = e instanceof Error ? e.message : String(e);
      } finally {
        this.listLoading = false;
      }
    },

    async loadColumn(column: OcBoardColumn) {
      const stateId = column.stateId;
      this.columnLoading = { ...this.columnLoading, [stateId]: true };
      this.columnItems = {
        ...this.columnItems,
        [stateId]: { ...emptyColumnState(), error: null },
      };

      try {
        const res = await ocExecuteQuery(column.queryKey, ocBuildColumnQueryRequest(column));
        this.columnItems = {
          ...this.columnItems,
          [stateId]: { items: res.items, total: res.total, error: null },
        };
        if (res.people && Object.keys(res.people).length > 0) {
          this.boardPeople = { ...this.boardPeople, ...res.people };
        }
      } catch (e: unknown) {
        this.columnItems = {
          ...this.columnItems,
          [stateId]: {
            items: [],
            total: 0,
            error: e instanceof Error ? e.message : String(e),
          },
        };
      } finally {
        this.columnLoading = { ...this.columnLoading, [stateId]: false };
      }
    },

    async loadAllColumns(columns: OcBoardColumn[]) {
      await Promise.all(columns.map((col) => this.loadColumn(col)));
    },

    async refreshBoard() {
      if (!this.activeBoardId) return;
      await this.loadBoard(this.activeBoardId, true);
    },

    clearError() {
      this.error = null;
    },

    clearBoardError() {
      this.boardError = null;
    },
  },
});
