<script setup lang="ts">
import { computed, watch, onMounted, onUnmounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcBoardKanban from '@/components/apps/operation-core/OcBoardKanban.vue';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useOperationCoreStore } from '@/stores/apps/operationCore';
import { useAppI18n } from '@/composables/useAppI18n';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();
const store = useOperationCoreStore();

const boardId = computed(() => String(route.params.boardId ?? ''));

type BoardDisplayMode = 'list' | 'kanban';

const displayMode = ref<BoardDisplayMode>('list');

const showKanbanToggle = computed(() => Boolean(store.boardContext));

function syncDisplayModeFromRoute() {
  const v = route.query.view;
  if (v === 'kanban' || v === 'list') {
    displayMode.value = v;
  }
}

function setDisplayMode(mode: BoardDisplayMode) {
  displayMode.value = mode;
  router.replace({
    query: { ...route.query, view: mode },
  });
}

const showList = computed(() => displayMode.value === 'list');
const showKanban = computed(() => displayMode.value === 'kanban');

const workspaceName = computed(() => {
  const wsId = store.boardContext?.workspaceId;
  if (!wsId) return '';
  return store.workspaces.find((w) => w.__dataId === wsId)?.name ?? '';
});

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  workspace: computed(() => {
    const ctx = store.boardContext;
    if (!ctx) return null;
    return {
      id: ctx.workspaceId,
      name: workspaceName.value || ctx.workspaceId,
    };
  }),
  board: computed(() => {
    const ctx = store.boardContext;
    if (!ctx) return null;
    return { id: ctx.boardId, name: ctx.name || ctx.boardId };
  }),
});

const listHeaders = computed(() => [
  { title: t('operationCore.board.colKey'), key: 'key', sortable: true },
  { title: t('operationCore.board.colTitle'), key: 'title', sortable: true },
  { title: t('operationCore.board.colState'), key: 'columnTitle', sortable: true },
  { title: t('operationCore.board.colAssignee'), key: 'assignee', sortable: false },
]);

const listRows = computed(() =>
  store.allBoardItems.map(({ item, columnTitle }) => ({
    id: item.id,
    key: item.key,
    title: item.title,
    columnTitle,
    assignee: item.assignee ?? '—',
    profileTo: `/apps/operation-core/work-items/${encodeURIComponent(item.id)}/profile?from=board&boardId=${encodeURIComponent(boardId.value)}`,
  }))
);

const createWorkItemTo = computed(() => {
  const ctx = store.boardContext;
  if (!ctx) return '/apps/operation-core/work-items/new';
  const qs = new URLSearchParams({
    workspaceId: ctx.workspaceId,
    boardId: ctx.boardId,
  });
  return `/apps/operation-core/work-items/new?${qs.toString()}`;
});

const backToWorkspaceTo = computed(() => {
  const ctx = store.boardContext;
  if (!ctx) return '/apps/operation-core/workspace';
  const qs = new URLSearchParams({
    workspaceId: ctx.workspaceId,
    boardId: ctx.boardId,
  });
  return `/apps/operation-core/workspace?${qs.toString()}`;
});

async function loadPage() {
  if (!store.workspaces.length) {
    await store.loadWorkspaces();
  }
  await store.loadBoard(boardId.value);
}

watch(boardId, () => {
  void loadPage();
});

watch(
  () => route.query.view,
  () => syncDisplayModeFromRoute()
);

onMounted(() => {
  syncDisplayModeFromRoute();
  void loadPage();
});

onUnmounted(() => {
  store.clearBoardState();
});
</script>

<template>
  <div class="oc-flow oc-board-page">
    <BaseBreadcrumb
      :title="store.boardContext?.name || t('operationCore.board.placeholderTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <v-alert
      v-if="store.boardError"
      type="error"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="store.clearBoardError()"
    >
      {{ store.boardError }}
    </v-alert>

    <v-card variant="outlined" class="rounded-lg mb-4">
      <v-card-title class="d-flex align-center flex-wrap gap-2 py-3">
        <v-btn
          icon="mdi-arrow-left"
          variant="text"
          size="small"
          :to="backToWorkspaceTo"
          :title="t('operationCore.board.backToWorkspace')"
        />
        <div class="min-width-0">
          <div class="text-subtitle-1 font-weight-bold text-truncate">
            {{ store.boardContext?.name || t('operationCore.board.loadingTitle') }}
          </div>
          <div v-if="workspaceName" class="text-caption text-medium-emphasis text-truncate">
            {{ workspaceName }}
          </div>
        </div>
        <v-spacer />
        <v-btn-toggle
          v-if="store.boardContext && showKanbanToggle"
          :model-value="displayMode"
          mandatory
          density="compact"
          color="primary"
          variant="outlined"
          divided
          class="mr-1"
          @update:model-value="setDisplayMode($event as BoardDisplayMode)"
        >
          <v-btn value="list" size="small" class="text-none px-3">
            <v-icon icon="mdi-format-list-bulleted" start size="18" />
            {{ t('operationCore.board.viewList') }}
          </v-btn>
          <v-btn value="kanban" size="small" class="text-none px-3">
            <v-icon icon="mdi-view-column-outline" start size="18" />
            {{ t('operationCore.board.viewKanban') }}
          </v-btn>
        </v-btn-toggle>
        <v-btn
          variant="tonal"
          color="primary"
          size="small"
          rounded="lg"
          class="text-none"
          :loading="store.loadingBoardContext"
          @click="store.refreshBoard()"
        >
          <v-icon icon="mdi-refresh" start size="18" />
          {{ t('operationCore.board.refresh') }}
        </v-btn>
        <v-btn
          v-if="store.boardContext?.permissions.canEdit"
          color="primary"
          size="small"
          variant="flat"
          rounded="lg"
          class="text-none"
          :to="createWorkItemTo"
        >
          <v-icon icon="mdi-plus" start size="18" />
          {{ t('operationCore.board.newWorkItem') }}
        </v-btn>
      </v-card-title>
    </v-card>

    <div v-if="store.loadingBoardContext && !store.boardContext" class="d-flex justify-center py-16">
      <v-progress-circular indeterminate color="primary" size="40" />
    </div>

    <template v-else-if="store.boardContext">
      <v-card v-if="showList" variant="outlined" class="rounded-lg">
        <v-data-table
          :headers="listHeaders"
          :items="listRows"
          item-value="id"
          density="comfortable"
          :loading="store.loadingBoardContext"
          class="oc-board-list-table"
        >
          <template #item.key="{ item }">
            <NuxtLink :to="item.profileTo" class="text-primary font-weight-medium text-decoration-none">
              {{ item.key }}
            </NuxtLink>
          </template>
          <template #item.title="{ item }">
            <NuxtLink :to="item.profileTo" class="text-decoration-none text-reset">
              {{ item.title }}
            </NuxtLink>
          </template>
        </v-data-table>
      </v-card>

      <OcBoardKanban
        v-else-if="showKanban"
        :columns="store.boardContext.columns"
        :column-items="store.columnItems"
        :column-loading="store.columnLoading"
        :board-id="boardId"
      />
    </template>

    <v-card v-else-if="!store.loadingBoardContext" variant="outlined" class="rounded-lg">
      <v-card-text class="pa-8 text-center text-medium-emphasis">
        {{ t('operationCore.board.notFound') }}
        <div class="mt-4">
          <v-btn variant="tonal" color="primary" class="text-none" to="/apps/operation-core/workspace">
            {{ t('operationCore.board.backToWorkspace') }}
          </v-btn>
        </div>
      </v-card-text>
    </v-card>
  </div>
</template>

<style scoped>
.min-width-0 {
  min-width: 0;
}
</style>
