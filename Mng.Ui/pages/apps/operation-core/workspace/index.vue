<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcWorkspaceTree from '@/components/apps/operation-core/OcWorkspaceTree.vue';
import { useResizableTreePanel } from '@/composables/useResizableTreePanel';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useOperationCoreStore } from '@/stores/apps/operationCore';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocListDashboardsForWorkspace } from '@/services/operationCoreService';
import type { OcDashboardListItem, OcWorkspaceTreeNode } from '@/types/apps/operationCore';
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
const treeLoading = ref(false);

const dashboards = ref<OcDashboardListItem[]>([]);
const dashboardsLoading = ref(false);

async function loadDashboards(workspaceId: string | null) {
  if (!workspaceId) {
    dashboards.value = [];
    return;
  }
  dashboardsLoading.value = true;
  try {
    dashboards.value = await ocListDashboardsForWorkspace(workspaceId);
  } catch {
    dashboards.value = [];
  } finally {
    dashboardsLoading.value = false;
  }
}

function openDashboard(dashboardId: string) {
  router.push({
    path: `/apps/operation-core/dashboards/${encodeURIComponent(dashboardId)}`,
    query: selectedWorkspaceId.value ? { workspaceId: selectedWorkspaceId.value } : undefined,
  });
}

const selectedWorkspace = computed(() =>
  selectedWorkspaceId.value
    ? store.workspaces.find((w) => w.__dataId === selectedWorkspaceId.value) ?? null
    : null
);

const selectedBoard = computed(() => {
  if (!selectedBoardId.value || !selectedWorkspaceId.value) return null;
  return store.boardsForWorkspace(selectedWorkspaceId.value).find((b) => b.__dataId === selectedBoardId.value) ?? null;
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

const treeNodes = computed((): OcWorkspaceTreeNode[] =>
  store.workspaces.map((w) => ({
    type: 'workspace' as const,
    data: w,
    children: store.boardsForWorkspace(w.__dataId).map((b) => ({
      type: 'board' as const,
      data: b,
    })),
  }))
);

function syncFromRoute() {
  const ws = typeof route.query.workspaceId === 'string' ? route.query.workspaceId : null;
  const bd = typeof route.query.boardId === 'string' ? route.query.boardId : null;
  selectedWorkspaceId.value = ws;
  selectedBoardId.value = bd;
}

async function loadTreeData() {
  treeLoading.value = true;
  try {
    await store.loadWorkspaces();
    await store.loadAllBoards();
  } finally {
    treeLoading.value = false;
  }
}

function onSelectWorkspace(workspaceId: string) {
  selectedWorkspaceId.value = workspaceId;
  selectedBoardId.value = null;
  router.replace({
    path: '/apps/operation-core/workspace',
    query: { workspaceId },
  });
}

function onSelectBoard(workspaceId: string, boardId: string) {
  selectedWorkspaceId.value = workspaceId;
  selectedBoardId.value = boardId;
  router.replace({
    path: '/apps/operation-core/workspace',
    query: { workspaceId, boardId },
  });
}

function openSelectedBoard() {
  if (!selectedBoardId.value) return;
  router.push({
    path: `/apps/operation-core/boards/${encodeURIComponent(selectedBoardId.value)}`,
    query: { view: 'list' },
  });
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

watch(selectedWorkspaceId, (id) => loadDashboards(id), { immediate: false });

onMounted(async () => {
  syncFromRoute();
  await loadTreeData();
  await store.pingOperations();
  if (selectedWorkspaceId.value) {
    await store.loadBoardsForWorkspace(selectedWorkspaceId.value);
    await loadDashboards(selectedWorkspaceId.value);
  }
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
                :workspace-nodes="treeNodes"
                :selected-workspace-id="selectedWorkspaceId"
                :selected-board-id="selectedBoardId"
                :empty-label="t('operationCore.workspace.noWorkspaces')"
                :label-workspaces-root="t('operationCore.workspace.workspacesRoot')"
                @select-workspace="onSelectWorkspace"
                @select-board="onSelectBoard"
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
          </v-card-title>
          <v-divider />
          <v-card-text class="flex-grow-1 overflow-auto pa-4 pa-md-6">
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

              <!-- Panolar -->
              <p class="text-subtitle-2 font-weight-bold mb-3 mt-6">
                {{ t('operationCore.dashboards.listTitle') }}
              </p>
              <div v-if="dashboardsLoading" class="d-flex py-2">
                <v-progress-circular indeterminate color="primary" size="22" />
              </div>
              <div
                v-else-if="!dashboards.length"
                class="text-body-2 text-medium-emphasis"
              >
                {{ t('operationCore.dashboards.noneInWorkspace') }}
              </div>
              <v-row v-else dense>
                <v-col
                  v-for="dash in dashboards"
                  :key="dash.id"
                  cols="12"
                  sm="6"
                  md="4"
                >
                  <v-card
                    variant="outlined"
                    class="rounded-lg h-100 oc-board-pick-card"
                    hover
                    @click="openDashboard(dash.id)"
                  >
                    <v-card-text class="pa-4">
                      <div class="d-flex align-center gap-2 mb-2">
                        <v-icon icon="mdi-view-dashboard-outline" size="22" color="primary" />
                        <span class="text-subtitle-1 font-weight-medium text-truncate">
                          {{ dash.name }}
                        </span>
                        <v-spacer />
                        <v-chip v-if="dash.isDefault" size="x-small" variant="tonal" color="primary">
                          {{ t('operationCore.dashboards.defaultChip') }}
                        </v-chip>
                      </div>
                      <p
                        v-if="dash.description"
                        class="text-caption text-medium-emphasis mb-0 text-truncate"
                      >
                        {{ dash.description }}
                      </p>
                    </v-card-text>
                  </v-card>
                </v-col>
              </v-row>
            </div>

            <!-- Board seçili -->
            <div
              v-else-if="selectedWorkspace && selectedBoard"
              class="d-flex align-center justify-center h-100"
            >
              <div class="text-center pa-4 oc-empty-state">
                <v-icon icon="mdi-view-column-outline" size="56" color="primary" class="mb-4 opacity-70" />
                <p class="text-h6 font-weight-medium mb-1">
                  {{ selectedBoard.name }}
                </p>
                <p class="text-body-2 text-medium-emphasis mb-1">
                  {{ selectedWorkspace.name }}
                </p>
                <v-chip size="small" variant="tonal" class="text-capitalize mb-4">
                  {{ boardViewTypeLabel(selectedBoard.viewType) }}
                </v-chip>
                <p class="text-body-2 text-medium-emphasis mb-4 mx-auto" style="max-width: 420px">
                  {{ t('operationCore.workspace.selectedBoardHint') }}
                </p>
                <v-btn
                  color="primary"
                  variant="flat"
                  rounded="lg"
                  class="text-none"
                  size="large"
                  @click="openSelectedBoard"
                >
                  {{ t('operationCore.workspace.openBoard') }}
                  <v-icon icon="mdi-chevron-right" end />
                </v-btn>
              </div>
            </div>
          </v-card-text>
        </v-card>
      </div>
    </div>
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
