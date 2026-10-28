<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcWorkspaceTree from '@/components/apps/operation-core/OcWorkspaceTree.vue';
import OcBoardDashboardLink from '@/components/apps/operation-core/OcBoardDashboardLink.vue';
import OcDashboardView from '@/components/apps/operation-core/dashboards/OcDashboardView.vue';
import OcBoardPanel from '@/components/apps/operation-core/OcBoardPanel.client.vue';
import { useResizableTreePanel } from '@/composables/useResizableTreePanel';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useOperationCoreStore } from '@/stores/apps/operationCore';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocGetDashboardRecord } from '@/services/operationCoreService';
import {
  ocCreateWorkspaceFolder,
  ocDeleteWorkspaceFolder,
  ocMoveWorkspaceFolder,
  ocMoveWorkspaceToFolder,
  ocUpdateWorkspaceFolder,
} from '@/services/operationCore/workspaceFolders';
import { useAuthStore } from '@/stores/auth';
import type { OcFolderDialogMode } from '@/components/apps/operation-core/OcWorkspaceFolderDialog.vue';
import OcWorkspaceFolderDialog from '@/components/apps/operation-core/OcWorkspaceFolderDialog.vue';
import OcWorkspaceLibraryPanel from '@/components/apps/operation-core/OcWorkspaceLibraryPanel.vue';
import OcWorkspaceSprints from '@/components/apps/operation-core/OcWorkspaceSprints.vue';
import OcWorkspaceCalendar from '@/components/apps/operation-core/OcWorkspaceCalendar.vue';
import OcWorkspaceGantt from '@/components/apps/operation-core/OcWorkspaceGantt.vue';
import { ocGetWorkspace } from '@/services/operationCoreService';
import { readOcWorkspaceViews, type OcWorkspaceViews } from '@/utils/ocWorkspaceViews';
import { buildOcNavTree, collectFolderDescendantIds, folderAncestorIds } from '@/utils/ocWorkspaceTree';
import type { OcNavNode, OpBoard, OpWorkspace, OpWorkspaceFolder } from '@/types/apps/operationCore';
import {
  LayoutSidebarLeftCollapseIcon,
  LayoutSidebarLeftExpandIcon,
  ChevronDownIcon,
  ChevronUpIcon,
} from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const router = useRouter();
const route = useRoute();
const store = useOperationCoreStore();
const auth = useAuthStore();
const canManageFolders = computed(() => auth.isManager || auth.isAdmin);

const {
  treeWidth,
  treeCollapsed,
  resizeActive,
  startResize,
  toggleTreeCollapse,
} = useResizableTreePanel('operation-core-workspace-tree', {
  minWidth: 220,
  maxWidth: 480,
  defaultWidth: 300,
});

const treeRef = ref<InstanceType<typeof OcWorkspaceTree> | null>(null);
const selectedWorkspaceId = ref<string | null>(null);
const selectedBoardId = ref<string | null>(null);
const workspacePane = ref<'home' | 'library' | 'sprints' | 'calendar' | 'gantt'>('home');
const boardsReady = ref(false);
const viewsReady = ref(false);
const viewFlags = ref<OcWorkspaceViews>({ sprints: false, calendar: false, gantt: false });
const treeLoading = ref(false);

const dashboardNameById = ref<Record<string, string>>({});
const dashboardNameInflight = new Set<string>();

async function ensureDashboardName(dashboardId: string) {
  const id = dashboardId?.trim();
  if (!id || dashboardNameById.value[id] || dashboardNameInflight.has(id)) return;
  dashboardNameInflight.add(id);
  try {
    const rec = await ocGetDashboardRecord(id);
    if (rec?.name) {
      dashboardNameById.value = { ...dashboardNameById.value, [id]: rec.name };
    }
  } catch {
    // pano adı opsiyonel — tree'de id gösterilir
  } finally {
    dashboardNameInflight.delete(id);
  }
}

async function ensureDashboardNamesForBoards(boards: OpBoard[]) {
  const ids = [...new Set(boards.map((b) => b.defaultDashboardId).filter(Boolean) as string[])];
  await Promise.all(ids.map(ensureDashboardName));
}

// Tree'de yalnızca workspace / board seçimi; pano sağ panel toggle ile açılır.

const selectedWorkspace = computed(() =>
  selectedWorkspaceId.value
    ? store.workspaces.find((w) => w.__dataId === selectedWorkspaceId.value) ?? null
    : null
);

const selectedBoard = computed(() => {
  if (!selectedBoardId.value || !selectedWorkspaceId.value) return null;
  return store.boardsForWorkspace(selectedWorkspaceId.value).find((b) => b.__dataId === selectedBoardId.value) ?? null;
});

const selectedBoardDashboardId = computed(() => selectedBoard.value?.defaultDashboardId ?? null);
const selectedBoardDashboardName = computed(() =>
  selectedBoardDashboardId.value ? dashboardNameById.value[selectedBoardDashboardId.value] ?? null : null
);

// Board seçiliyken merkez panel: varsayılan board; pano isteğe bağlı toggle ile.
const centerView = ref<'board' | 'dashboard'>('board');

watch(selectedBoardDashboardId, (dashId) => {
  if (dashId) void ensureDashboardName(dashId);
});

watch(selectedBoardId, (id, prev) => {
  if (id) {
    centerView.value = 'board';
  } else if (prev) {
    store.clearBoardState();
  }
});

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  workspace: computed(() =>
    selectedWorkspace.value
      ? { id: selectedWorkspace.value.__dataId, name: selectedWorkspace.value.name }
      : null
  ),
  board: computed(() =>
    selectedBoard.value ? { id: selectedBoard.value.__dataId, name: selectedBoard.value.name } : null
  ),
  showWorkspaceExplorer: computed(() => !selectedWorkspaceId.value && !selectedBoardId.value),
});

const navNodes = computed((): OcNavNode[] =>
  buildOcNavTree(store.folders, store.workspaces, (workspaceId) => store.boardsForWorkspace(workspaceId))
);

const revealIds = computed(() => {
  const workspace = selectedWorkspace.value;
  if (!workspace?.folderId) return [];
  return folderAncestorIds(store.folders, workspace.folderId);
});

type FolderAction =
  | { mode: 'create'; parentFolderId: string | null }
  | { mode: 'rename'; folder: OpWorkspaceFolder }
  | { mode: 'move-folder'; folder: OpWorkspaceFolder }
  | { mode: 'move-workspace'; workspace: OpWorkspace }
  | { mode: 'delete'; folder: OpWorkspaceFolder };

const folderAction = ref<FolderAction | null>(null);
const folderDialogOpen = ref(false);
const folderSaving = ref(false);
const folderError = ref<string | null>(null);

const folderDialogMode = computed((): OcFolderDialogMode => {
  const action = folderAction.value;
  if (!action || action.mode === 'create') return 'create';
  if (action.mode === 'rename') return 'rename';
  if (action.mode === 'delete') return 'delete';
  return 'move';
});

const folderDialogName = computed(() => {
  const action = folderAction.value;
  if (action?.mode === 'rename') return action.folder.name;
  return '';
});

const folderDialogTarget = computed(() => {
  const action = folderAction.value;
  if (action?.mode === 'move-folder') return action.folder.parentFolderId || '__root__';
  if (action?.mode === 'move-workspace') return action.workspace.folderId || '__root__';
  return '__root__';
});

const folderDialogGroups = computed(() => {
  const action = folderAction.value;
  if (action?.mode === 'rename') return action.folder.viewGroups;
  return [];
});

const moveTargets = computed(() => {
  const action = folderAction.value;
  const blocked =
    action?.mode === 'move-folder' ? collectFolderDescendantIds(store.folders, action.folder.__dataId) : new Set<string>();
  const currentParent =
    action?.mode === 'move-folder'
      ? action.folder.parentFolderId
      : action?.mode === 'move-workspace'
        ? action.workspace.folderId ?? null
        : null;
  const rows: { id: string; title: string; disabled?: boolean }[] = [
    { id: '__root__', title: t('operationCore.workspace.folders.rootTarget'), disabled: currentParent == null },
  ];
  const byParent = new Map<string | null, OpWorkspaceFolder[]>();
  for (const folder of store.folders) {
    const key = folder.parentFolderId;
    const list = byParent.get(key) ?? [];
    list.push(folder);
    byParent.set(key, list);
  }
  const walk = (parentId: string | null, prefix: string) => {
    const children = [...(byParent.get(parentId) ?? [])].sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
    for (const folder of children) {
      rows.push({
        id: folder.__dataId,
        title: `${prefix}${folder.name}`,
        disabled: blocked.has(folder.__dataId) || folder.__dataId === currentParent,
      });
      walk(folder.__dataId, `${prefix}${folder.name} / `);
    }
  };
  walk(null, '');
  return rows;
});

function openFolderAction(action: FolderAction) {
  folderAction.value = action;
  folderError.value = null;
  folderDialogOpen.value = true;
}

async function refreshTree() {
  await store.loadWorkspaces(true);
  await store.loadFolders();
}

async function onFolderSave(payload: { name: string; viewGroups: string[]; targetId: string | null }) {
  const action = folderAction.value;
  if (!action) return;
  folderSaving.value = true;
  folderError.value = null;
  try {
    if (action.mode === 'create') {
      await ocCreateWorkspaceFolder({
        name: payload.name,
        parentFolderId: action.parentFolderId,
        viewGroups: payload.viewGroups,
      });
    } else if (action.mode === 'rename') {
      await ocUpdateWorkspaceFolder(action.folder.__dataId, {
        name: payload.name,
        viewGroups: payload.viewGroups,
      });
    } else if (action.mode === 'move-folder') {
      await ocMoveWorkspaceFolder(action.folder.__dataId, payload.targetId === '__root__' ? null : payload.targetId);
    } else if (action.mode === 'move-workspace') {
      await ocMoveWorkspaceToFolder(action.workspace.__dataId, payload.targetId === '__root__' ? null : payload.targetId);
    } else {
      await ocDeleteWorkspaceFolder(action.folder.__dataId);
    }
    folderDialogOpen.value = false;
    await refreshTree();
  } catch (e: unknown) {
    folderError.value = e instanceof Error ? e.message : String(e);
  } finally {
    folderSaving.value = false;
  }
}

function syncFromRoute() {
  const ws = typeof route.query.workspaceId === 'string' ? route.query.workspaceId : null;
  const bd = typeof route.query.boardId === 'string' ? route.query.boardId : null;
  selectedWorkspaceId.value = ws;
  selectedBoardId.value = bd;
  const view = typeof route.query.view === 'string' ? route.query.view : '';
  workspacePane.value =
    ws && !bd && view === 'library'
      ? 'library'
      : ws && !bd && view === 'sprints'
        ? 'sprints'
        : ws && !bd && view === 'calendar'
          ? 'calendar'
          : ws && !bd && view === 'gantt'
            ? 'gantt'
            : 'home';
  if (bd) centerView.value = 'board';
}

async function loadTreeData() {
  treeLoading.value = true;
  try {
    await store.loadWorkspaces();
    await store.loadFolders();
  } finally {
    treeLoading.value = false;
  }
}

async function onExpandWorkspace(workspaceId: string) {
  await store.loadBoardsForWorkspace(workspaceId);
  await ensureDashboardNamesForBoards(store.boardsForWorkspace(workspaceId));
}

async function onExpandAllWorkspaces() {
  await store.loadAllBoards();
  for (const w of store.workspaces) {
    await ensureDashboardNamesForBoards(store.boardsForWorkspace(w.__dataId));
  }
}

// Library pane is separate from the board view.
function openLibrary() {
  if (!selectedWorkspaceId.value) return;
  workspacePane.value = 'library';
  selectedBoardId.value = null;
  router.replace({
    path: '/apps/operation-core/workspace',
    query: { workspaceId: selectedWorkspaceId.value, view: 'library' },
  });
}

function openSprints() {
  if (!selectedWorkspaceId.value || !viewFlags.value.sprints) return;
  workspacePane.value = 'sprints';
  selectedBoardId.value = null;
  router.replace({
    path: '/apps/operation-core/workspace',
    query: { workspaceId: selectedWorkspaceId.value, view: 'sprints' },
  });
}

function openGantt() {
  if (!selectedWorkspaceId.value || !viewFlags.value.gantt) return;
  workspacePane.value = 'gantt';
  selectedBoardId.value = null;
  router.replace({
    path: '/apps/operation-core/workspace',
    query: { workspaceId: selectedWorkspaceId.value, view: 'gantt' },
  });
}

async function loadViewFlags(workspaceId: string) {
  viewsReady.value = false;
  try {
    const detail = await ocGetWorkspace(workspaceId);
    viewFlags.value = readOcWorkspaceViews(detail?.settings);
  } catch {
    viewFlags.value = { sprints: false, calendar: false, gantt: false };
  } finally {
    viewsReady.value = true;
  }
}

function openCalendar() {
  if (!selectedWorkspaceId.value || !viewFlags.value.calendar) return;
  workspacePane.value = 'calendar';
  selectedBoardId.value = null;
  router.replace({
    path: '/apps/operation-core/workspace',
    query: { workspaceId: selectedWorkspaceId.value, view: 'calendar' },
  });
}

function openWorkspaceHome() {
  if (!selectedWorkspaceId.value) return;
  workspacePane.value = 'home';
  router.replace({
    path: '/apps/operation-core/workspace',
    query: { workspaceId: selectedWorkspaceId.value },
  });
}

function onSelectWorkspace(workspaceId: string) {
  selectedWorkspaceId.value = workspaceId;
  selectedBoardId.value = null;
  workspacePane.value = 'home';
  boardsReady.value = false;
  viewsReady.value = false;
  router.replace({
    path: '/apps/operation-core/workspace',
    query: { workspaceId },
  });
  void loadViewFlags(workspaceId);
  void store.loadBoardsForWorkspace(workspaceId).then(async () => {
    await ensureDashboardNamesForBoards(store.boardsForWorkspace(workspaceId));
    boardsReady.value = true;
  });
}

function onSelectBoard(workspaceId: string, boardId: string) {
  selectedWorkspaceId.value = workspaceId;
  selectedBoardId.value = boardId;
  centerView.value = 'board';
  router.replace({
    path: '/apps/operation-core/workspace',
    query: { workspaceId, boardId },
  });
}

async function onBoardDashboardAssigned(dashboardId: string | null) {
  if (!selectedWorkspaceId.value) return;
  await store.loadBoardsForWorkspace(selectedWorkspaceId.value, true);
  if (dashboardId) {
    await ensureDashboardName(dashboardId);
  }
}

function selectBoardFromList(workspaceId: string, boardId: string) {
  onSelectBoard(workspaceId, boardId);
}

const boardsForSelectedWorkspace = computed(() =>
  selectedWorkspaceId.value ? store.boardsForWorkspace(selectedWorkspaceId.value) : []
);

function boardViewTypeLabel(viewType?: string): string {
  if (viewType === 'kanban') return t('operationCore.workspace.viewTypeKanban');
  if (viewType === 'list') return t('operationCore.workspace.viewTypeList');
  return viewType || '—';
}

watch(
  () => route.query,
  () => syncFromRoute(),
  { deep: true }
);

watch([viewsReady, viewFlags, workspacePane], () => {
  if (!viewsReady.value) return;
  const pane = workspacePane.value;
  if (pane === 'sprints' && !viewFlags.value.sprints) openWorkspaceHome();
  if (pane === 'calendar' && !viewFlags.value.calendar) openWorkspaceHome();
  if (pane === 'gantt' && !viewFlags.value.gantt) openWorkspaceHome();
});

onMounted(async () => {
  syncFromRoute();
  await loadTreeData();
  await store.pingOperations();
  if (selectedWorkspaceId.value) {
    await loadViewFlags(selectedWorkspaceId.value);
    await store.loadBoardsForWorkspace(selectedWorkspaceId.value);
    await ensureDashboardNamesForBoards(store.boardsForWorkspace(selectedWorkspaceId.value));
  }
  boardsReady.value = true;
});
</script>

<template>
  <div class="oc-flow oc-workspace-page">
    <BaseBreadcrumb
      :title="t('operationCore.workspace.title')"
      :breadcrumbs="breadcrumbs"
    />

    <v-alert
      v-if="store.error"
      type="error"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="store.clearError()"
    >
      {{ store.error }}
    </v-alert>

    <div class="d-flex workspace-shell" style="min-height: 420px">
      <template v-if="!treeCollapsed">
        <div class="flex-shrink-0 overflow-hidden" :style="{ width: treeWidth + 'px' }">
          <v-card variant="outlined" class="h-100 d-flex flex-column rounded-lg">
            <v-card-title class="d-flex align-center py-3 flex-wrap gap-1">
              <span class="text-subtitle-1">{{ t('operationCore.workspace.treePanel') }}</span>
              <v-spacer />
              <v-chip
                v-if="store.operationsLive !== null"
                size="x-small"
                :color="store.operationsLive ? 'success' : 'warning'"
                variant="tonal"
                class="mr-1"
              >
                {{ store.operationsLive ? t('operationCore.workspace.apiLive') : t('operationCore.workspace.apiOffline') }}
              </v-chip>
              <v-btn
                icon
                size="small"
                variant="text"
                :title="t('operationCore.workspace.collapseTree')"
                @click="toggleTreeCollapse"
              >
                <LayoutSidebarLeftCollapseIcon size="18" />
              </v-btn>
              <v-btn
                icon
                size="small"
                variant="text"
                :title="t('operationCore.workspace.expandAll')"
                @click="treeRef?.expandAll()"
              >
                <ChevronDownIcon size="18" />
              </v-btn>
              <v-btn
                icon
                size="small"
                variant="text"
                :title="t('operationCore.workspace.collapseAll')"
                @click="treeRef?.collapseAll()"
              >
                <ChevronUpIcon size="18" />
              </v-btn>
            </v-card-title>
            <v-divider />
            <v-card-text class="pa-0 flex-grow-1 overflow-auto workspace-tree-scroll">
              <div v-if="treeLoading || store.loadingWorkspaces" class="pa-6 d-flex justify-center">
                <v-progress-circular indeterminate color="primary" size="32" />
              </div>
              <OcWorkspaceTree
                v-else
                ref="treeRef"
                :nodes="navNodes"
                :selected-workspace-id="selectedWorkspaceId"
                :selected-board-id="selectedBoardId"
                :can-manage="canManageFolders"
                :reveal-ids="revealIds"
                :empty-label="t('operationCore.workspace.noWorkspaces')"
                :label-workspaces-root="t('operationCore.workspace.workspacesRoot')"
                @select-workspace="onSelectWorkspace"
                @select-board="onSelectBoard"
                @expand-workspace="onExpandWorkspace"
                @expand-all-workspaces="onExpandAllWorkspaces"
                @create-folder="openFolderAction({ mode: 'create', parentFolderId: $event })"
                @rename-folder="openFolderAction({ mode: 'rename', folder: $event })"
                @move-folder="openFolderAction({ mode: 'move-folder', folder: $event })"
                @delete-folder="openFolderAction({ mode: 'delete', folder: $event })"
                @move-workspace="openFolderAction({ mode: 'move-workspace', workspace: $event })"
              />
            </v-card-text>
          </v-card>
        </div>
        <div
          class="oc-tree-resize-handle flex-shrink-0"
          :class="{ 'oc-tree-resize-active': resizeActive }"
          @mousedown="startResize"
        />
      </template>

      <div class="flex-grow-1 min-width-0">
        <v-card variant="outlined" class="h-100 rounded-lg d-flex flex-column">
          <v-card-title class="d-flex align-center py-3 flex-wrap gap-2">
            <v-btn
              v-if="treeCollapsed"
              icon
              variant="tonal"
              size="small"
              class="mr-1"
              :title="t('operationCore.workspace.showTree')"
              @click="toggleTreeCollapse"
            >
              <LayoutSidebarLeftExpandIcon size="20" />
            </v-btn>
            <span class="text-subtitle-1 text-truncate">
              <template v-if="selectedBoard && selectedWorkspace">
                {{ selectedWorkspace.name }} · {{ selectedBoard.name }}
              </template>
              <template v-else-if="selectedWorkspace">
                {{ selectedWorkspace.name }}
              </template>
              <template v-else>
                {{ t('operationCore.workspace.mainTitle') }}
              </template>
            </span>
            <v-btn
              v-if="selectedWorkspace && selectedBoard"
              size="small"
              class="text-none"
              variant="text"
              prepend-icon="mdi-bookshelf"
              @click="openLibrary"
            >
              {{ t('operationCore.workspace.library.open') }}
            </v-btn>
            <template v-if="selectedBoard">
              <v-spacer />
              <v-btn-toggle
                v-if="selectedBoardDashboardId"
                v-model="centerView"
                mandatory
                density="comfortable"
                color="primary"
                variant="outlined"
                divided
                class="mr-1"
              >
                <v-btn value="board" size="small" class="text-none" prepend-icon="mdi-view-column-outline">
                  {{ t('operationCore.workspace.viewBoard') }}
                </v-btn>
                <v-btn value="dashboard" size="small" class="text-none" prepend-icon="mdi-view-dashboard-outline">
                  {{ t('operationCore.workspace.openDashboard') }}
                </v-btn>
              </v-btn-toggle>
              <OcBoardDashboardLink
                v-if="selectedBoard && selectedWorkspace"
                :workspace-id="selectedWorkspace.__dataId"
                :board="selectedBoard"
                :dashboard-name="selectedBoardDashboardName"
                density="compact"
                class="mr-1"
                @assigned="onBoardDashboardAssigned"
              />
            </template>
          </v-card-title>
          <v-divider />
          <v-card-text
            class="flex-grow-1 overflow-auto"
            :class="selectedBoard && centerView === 'board' ? 'pa-0' : 'pa-4 pa-md-6'"
          >
            <!-- Hiç seçim yok -->
            <div
              v-if="!selectedWorkspace"
              class="d-flex align-center justify-center h-100"
            >
              <div class="text-center pa-4 oc-empty-state">
                <v-icon icon="mdi-view-dashboard-outline" size="56" color="primary" class="mb-4 opacity-70" />
                <p class="text-h6 font-weight-medium mb-2">
                  {{ t('operationCore.workspace.emptyTitle') }}
                </p>
                <p class="text-body-2 text-medium-emphasis mb-0 mx-auto" style="max-width: 420px">
                  {{ t('operationCore.workspace.emptyHint') }}
                </p>
              </div>
            </div>

            <!-- Workspace seçili, board henüz yok -->
            <div v-else-if="selectedWorkspace && !selectedBoard">
              <div class="oc-hero mb-5 pa-4 pa-md-5 rounded-lg">
                <p class="text-overline text-medium-emphasis mb-1">
                  {{ t('operationCore.breadcrumbRoot') }}
                </p>
                <h2 class="text-h5 font-weight-bold mb-2">
                  {{ selectedWorkspace.name }}
                </h2>
                <p
                  v-if="selectedWorkspace.description"
                  class="text-body-2 text-medium-emphasis mb-3"
                >
                  {{ selectedWorkspace.description }}
                </p>
                <div class="d-flex flex-wrap gap-2">
                  <v-chip
                    v-if="selectedWorkspace.workItemKeyPrefix"
                    size="small"
                    variant="tonal"
                    color="primary"
                  >
                    {{ t('operationCore.workspace.keyPrefix') }}: {{ selectedWorkspace.workItemKeyPrefix }}
                  </v-chip>
                  <v-chip size="small" variant="outlined">
                    {{ t('operationCore.workspace.boardCount', { count: boardsForSelectedWorkspace.length }) }}
                  </v-chip>
                </div>
              </div>

              <v-btn-toggle
                :model-value="workspacePane"
                mandatory
                density="comfortable"
                color="primary"
                variant="outlined"
                divided
                class="mb-4"
              >
                <v-btn value="home" size="small" class="text-none" @click="openWorkspaceHome">
                  {{ t('operationCore.workspace.library.boards') }}
                </v-btn>
                <v-btn value="library" size="small" class="text-none" prepend-icon="mdi-bookshelf" @click="openLibrary">
                  {{ t('operationCore.workspace.library.open') }}
                </v-btn>
                <v-btn
                  v-if="viewsReady && viewFlags.calendar"
                  value="calendar"
                  size="small"
                  class="text-none"
                  prepend-icon="mdi-calendar-month"
                  @click="openCalendar"
                >
                  {{ t('operationCore.calendar.title') }}
                </v-btn>
                <v-btn
                  v-if="viewsReady && viewFlags.gantt"
                  value="gantt"
                  size="small"
                  class="text-none"
                  prepend-icon="mdi-chart-gantt"
                  @click="openGantt"
                >
                  {{ t('operationCore.gantt.title') }}
                </v-btn>
                <v-btn
                  v-if="viewsReady && viewFlags.sprints"
                  value="sprints"
                  size="small"
                  class="text-none"
                  prepend-icon="mdi-calendar-clock"
                  @click="openSprints"
                >
                  {{ t('operationCore.sprints.title') }}
                </v-btn>
              </v-btn-toggle>

              <OcWorkspaceLibraryPanel
                v-if="workspacePane === 'library'"
                :workspace-id="selectedWorkspace.__dataId"
              />
              <OcWorkspaceCalendar
                v-else-if="workspacePane === 'calendar' && viewFlags.calendar"
                :workspace-id="selectedWorkspace.__dataId"
                :show-sprints="viewFlags.sprints"
              />
              <OcWorkspaceGantt
                v-else-if="workspacePane === 'gantt' && viewFlags.gantt"
                :workspace-id="selectedWorkspace.__dataId"
                :show-sprints="viewFlags.sprints"
              />
              <OcWorkspaceSprints
                v-else-if="workspacePane === 'sprints' && viewFlags.sprints"
                :workspace-id="selectedWorkspace.__dataId"
              />

              <template v-else>
              <p class="text-subtitle-2 font-weight-bold mb-3">
                {{ t('operationCore.workspace.boardsList') }}
              </p>
              <div v-if="!boardsForSelectedWorkspace.length" class="text-body-2 text-medium-emphasis">
                {{ t('operationCore.workspace.noBoardsInWorkspace') }}
              </div>
              <v-row v-else dense>
                <v-col
                  v-for="board in boardsForSelectedWorkspace"
                  :key="board.__dataId"
                  cols="12"
                  sm="6"
                  md="4"
                >
                  <v-card
                    variant="outlined"
                    class="rounded-lg h-100 oc-board-pick-card"
                    hover
                    @click="selectBoardFromList(selectedWorkspace.__dataId, board.__dataId)"
                  >
                    <v-card-text class="pa-4">
                      <div class="d-flex align-center gap-2 mb-2">
                        <v-icon icon="mdi-view-column-outline" size="22" color="primary" />
                        <span class="text-subtitle-1 font-weight-medium text-truncate">
                          {{ board.name }}
                        </span>
                      </div>
                      <v-chip size="x-small" variant="tonal" class="text-capitalize">
                        {{ boardViewTypeLabel(board.viewType) }}
                      </v-chip>
                    </v-card-text>
                  </v-card>
                </v-col>
              </v-row>
              </template>
            </div>

            <!-- Board seçili + inline board -->
            <OcBoardPanel
              v-else-if="selectedWorkspace && selectedBoard && centerView === 'board' && selectedBoardId"
              :key="selectedBoardId"
              :board-id="selectedBoardId"
              embedded
              @dashboard-assigned="onBoardDashboardAssigned"
            />

            <!-- Board seçili + pano görünümü -->
            <div
              v-else-if="selectedWorkspace && selectedBoard && centerView === 'dashboard' && selectedBoardDashboardId"
            >
              <OcDashboardView
                :key="selectedBoardDashboardId"
                :dashboard-id="selectedBoardDashboardId"
                :show-description="true"
              />
            </div>

            <!-- Board seçili ama pano atanmamış — pano görünümü seçilemez; board zaten üstte -->
            <div
              v-else-if="selectedWorkspace && selectedBoard && centerView === 'dashboard' && !selectedBoardDashboardId"
              class="d-flex align-center justify-center h-100"
            >
              <div class="text-center pa-4 oc-empty-state">
                <v-icon icon="mdi-view-dashboard-outline" size="56" color="primary" class="mb-4 opacity-70" />
                <p class="text-body-2 text-medium-emphasis mb-4 mx-auto" style="max-width: 420px">
                  {{ t('operationCore.workspace.noDashboardAssigned') }}
                </p>
                <OcBoardDashboardLink
                  v-if="selectedWorkspace"
                  :workspace-id="selectedWorkspace.__dataId"
                  :board="selectedBoard"
                  class="justify-center"
                  @assigned="onBoardDashboardAssigned"
                />
              </div>
            </div>
          </v-card-text>
        </v-card>
      </div>
    </div>
    <OcWorkspaceFolderDialog
      v-model="folderDialogOpen"
      :mode="folderDialogMode"
      :name="folderDialogName"
      :view-groups="folderDialogGroups"
      :target-id="folderDialogTarget"
      :targets="moveTargets"
      :saving="folderSaving"
      :error="folderError"
      @save="onFolderSave"
    />
  </div>
</template>

<style scoped>
.oc-tree-resize-handle {
  width: 6px;
  cursor: col-resize;
  margin: 0 4px;
  border-radius: 4px;
  transition: background-color 0.15s ease;
}
.oc-tree-resize-handle:hover,
.oc-tree-resize-active {
  background-color: rgba(var(--v-theme-primary), 0.25);
}
.min-width-0 {
  min-width: 0;
}
.oc-board-pick-card {
  cursor: pointer;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}
.oc-board-pick-card:hover {
  border-color: rgba(var(--v-theme-primary), 0.45);
}
</style>
