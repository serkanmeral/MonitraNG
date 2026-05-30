<script setup lang="ts">
import { computed, watch, onMounted, onUnmounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcBoardCatalogLabel from '@/components/apps/operation-core/OcBoardCatalogLabel.vue';
import OcBoardKanban from '@/components/apps/operation-core/OcBoardKanban.vue';
import OcBoardListFilters from '@/components/apps/operation-core/OcBoardListFilters.vue';
import type { OcBoardFilterColumn, OcBoardFilterKind } from '@/components/apps/operation-core/OcBoardListFilters.vue';
import OcWorkItemFormDialog from '@/components/apps/operation-core/OcWorkItemFormDialog.vue';
import OcSlaStatusChip from '@/components/apps/operation-core/OcSlaStatusChip.vue';
import { useOcBoardListLookups } from '@/composables/useOcBoardListLookups';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useOperationCoreStore } from '@/stores/apps/operationCore';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocDeleteWorkItem, ocExtractDgErrorMessage, ocListPoolFieldsForWorkspace } from '@/services/operationCoreService';
import type { OcBoardListFilter, OcBoardListRequest, OcColumnFormat, OcWorkItemCard, OpField } from '@/types/apps/operationCore';
import {
  defaultFormatForKey,
  isBuiltInListColumn,
  isCoreListColumn,
  isSystemListColumn,
  listTableCellValue,
  listTablePoolCellValue,
  normalizeListTableColumns,
  systemColumnRawValue,
} from '@/utils/ocBoardListColumns';
import { formatCellValue } from '@/utils/ocColumnFormat';

definePageMeta({ layout: 'default' });

const { t, locale } = useAppI18n();
const route = useRoute();
const router = useRouter();
const store = useOperationCoreStore();

const boardId = computed(() => String(route.params.boardId ?? ''));

type BoardDisplayMode = 'list' | 'kanban';

const displayMode = ref<BoardDisplayMode>('list');

const boardIsKanban = computed(() => store.boardContext?.viewType === 'kanban');

const showKanbanToggle = computed(() => boardIsKanban.value);

function syncDisplayModeFromRoute() {
  const v = route.query.view;
  if (v === 'kanban' && boardIsKanban.value) {
    displayMode.value = 'kanban';
    return;
  }
  displayMode.value = 'list';
}

function applyDefaultDisplayMode() {
  if (route.query.view === 'kanban' && boardIsKanban.value) {
    displayMode.value = 'kanban';
  } else {
    displayMode.value = 'list';
  }
}

function setDisplayMode(mode: BoardDisplayMode) {
  displayMode.value = mode;
  router.replace({
    query: { ...route.query, view: mode },
  });
}

const showList = computed(() => !boardIsKanban.value || displayMode.value === 'list');
const showKanban = computed(() => boardIsKanban.value && displayMode.value === 'kanban');

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

const workspaceId = computed(() => store.boardContext?.workspaceId ?? null);
const boardCatalogs = computed(() => store.boardContext?.catalogs ?? null);
const boardPeople = computed(() => store.boardPeople);

const {
  resolveState,
  resolvePriority,
  resolveType,
  resolveAssigneeName,
  resolvePersonValue,
  stateById,
  priorityById,
  typeById,
} = useOcBoardListLookups(workspaceId, boardCatalogs, boardPeople);

const poolFields = ref<OpField[]>([]);

const poolFieldLabelByKey = computed(
  () => new Map(poolFields.value.filter((f) => f.key).map((f) => [f.key, f.label?.trim() || f.key]))
);

const poolFieldKeys = computed(() => poolFields.value.map((f) => f.key).filter((k): k is string => !!k));

const personPoolKeySet = computed(
  () =>
    new Set(
      poolFields.value
        .filter((f) => f.key && ['persons', 'person'].includes((f.fieldType || '').toLowerCase()))
        .map((f) => f.key)
    )
);

const listColumnsMeta = computed(() => store.boardContext?.listColumns ?? []);

const sortableKeySet = computed(
  () => new Set(listColumnsMeta.value.filter((c) => c.sortable).map((c) => c.key))
);

const listColumnKeys = computed(() => {
  const meta = listColumnsMeta.value;
  if (meta.length) return meta.map((c) => c.key);
  return normalizeListTableColumns(store.boardContext?.cardFieldKeys, poolFieldKeys.value);
});

function columnLabel(key: string): string {
  if (isBuiltInListColumn(key)) {
    return t(`operationCore.workspaceDefinitions.boards.listTableColumns.${key}`);
  }
  return poolFieldLabelByKey.value.get(key) ?? key;
}

const initialStateId = computed(() => store.boardContext?.initialStateId ?? null);

const columnFormatByKey = computed(() => {
  const map = new Map<string, OcColumnFormat | null>();
  for (const c of listColumnsMeta.value) {
    map.set(c.key, c.format ?? defaultFormatForKey(c.key));
  }
  return map;
});

function columnFormat(key: string): OcColumnFormat | null {
  return columnFormatByKey.value.get(key) ?? defaultFormatForKey(key);
}

function isColumnSortable(key: string): boolean {
  // listColumns meta varsa onu kullan; yoksa (eski board) key/title client-side.
  if (listColumnsMeta.value.length) return sortableKeySet.value.has(key);
  return key === 'key' || key === 'title';
}

const listHeaders = computed(() => {
  const cols = listColumnKeys.value.map((key) => ({
    title: columnLabel(key),
    key,
    sortable: isColumnSortable(key),
  }));
  cols.push({
    title: t('operationCore.board.actions.header'),
    key: 'actions',
    sortable: false,
    align: 'end',
    width: 140,
  } as (typeof cols)[number]);
  return cols;
});

function filterKind(key: string): OcBoardFilterKind {
  if (key === 'stateId') return 'state';
  if (key === 'priorityId') return 'priority';
  if (key === 'typeId') return 'type';
  if (key === 'assignee' || key === 'createdBy' || personPoolKeySet.value.has(key)) return 'person';
  return 'text';
}

const filterableColumns = computed<OcBoardFilterColumn[]>(() =>
  listColumnsMeta.value
    .filter((c) => c.filterable)
    .map((c) => ({ key: c.key, label: columnLabel(c.key), kind: filterKind(c.key) }))
);

const stateFilterOptions = computed(() =>
  Array.from(stateById.value.values()).map((c) => ({ value: c.id, title: c.name || c.id }))
);
const priorityFilterOptions = computed(() =>
  Array.from(priorityById.value.values()).map((c) => ({ value: c.id, title: c.name || c.id }))
);
const typeFilterOptions = computed(() =>
  Array.from(typeById.value.values()).map((c) => ({ value: c.id, title: c.name || c.id }))
);

const listRows = computed(() =>
  store.listItems.map((item) => {
    const stateLabel = resolveState(item.stateId, null)?.name ?? item.stateId ?? null;
    const row: Record<string, unknown> = {
      id: item.id,
      keyText: item.key ?? '',
      titleText: item.title ?? '',
      profileTo: `/apps/operation-core/work-items/${encodeURIComponent(item.id)}/profile?from=board&boardId=${encodeURIComponent(boardId.value)}`,
      stateColumnTitle: stateLabel,
      rawStateId: item.stateId ?? null,
      rawAssignee: item.assignee ?? null,
      rawPriorityId: item.priorityId ?? null,
      rawTypeId: item.typeId ?? null,
      rawCreatedBy: item.createdBy ?? null,
      // SLA chip + person sütunları slot ile render edilir; ham kartı taşı.
      __card: item as OcWorkItemCard,
    };
    for (const key of listColumnKeys.value) {
      // createdBy (kişi) ve sla (chip) slot ile render edilir — satır metni gerekmez.
      if (key === 'createdBy' || key === 'sla') continue;
      if (isSystemListColumn(key)) {
        row[key] = formatCellValue(systemColumnRawValue(item, key), columnFormat(key), {
          locale: locale(),
          anchorEnd: key === 'age' ? item.closedAt : null,
        });
      } else if (isCoreListColumn(key)) {
        row[key] = listTableCellValue(item, key, { stateLabel: stateLabel ?? undefined });
      } else if (personPoolKeySet.value.has(key)) {
        row[key] = resolvePersonValue(item.fields?.[key]);
      } else {
        row[key] = listTablePoolCellValue(item.fields, key);
      }
    }
    return row;
  })
);

// --- Server-side liste durumu ---
const page = ref(1);
const itemsPerPage = ref(25);
const sortBy = ref<{ key: string; order: 'asc' | 'desc' }[]>([]);
const searchInput = ref('');
const activeFilters = ref<OcBoardListFilter[]>([]);
const itemsPerPageOptions = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
  { value: 100, title: '100' },
];

let lastSignature = '';
let searchTimer: ReturnType<typeof setTimeout> | null = null;

function buildListRequest(): OcBoardListRequest {
  const primary = sortBy.value[0];
  const sort = primary
    ? { field: primary.key, direction: primary.order === 'desc' ? 'desc' : 'asc' as const }
    : (store.boardContext?.defaultSort ?? null);
  return {
    skip: Math.max(0, (page.value - 1) * itemsPerPage.value),
    take: itemsPerPage.value,
    sort,
    filters: activeFilters.value,
    search: (searchInput.value ?? '').trim() || null,
  };
}

async function fetchList(force = false) {
  if (!store.boardContext) return;
  const req = buildListRequest();
  const sig = JSON.stringify(req);
  if (!force && sig === lastSignature) return;
  lastSignature = sig;
  await store.loadBoardListPage(req);
}

function onListOptions(opts: { page: number; itemsPerPage: number; sortBy: { key: string; order: 'asc' | 'desc' }[] }) {
  page.value = opts.page;
  itemsPerPage.value = opts.itemsPerPage > 0 ? opts.itemsPerPage : 25;
  sortBy.value = Array.isArray(opts.sortBy) ? opts.sortBy : [];
  void fetchList();
}

function onFiltersUpdate(filters: OcBoardListFilter[]) {
  activeFilters.value = filters;
  page.value = 1;
  void fetchList();
}

watch(searchInput, () => {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    page.value = 1;
    void fetchList();
  }, 400);
});

const hasSearch = computed(() => (searchInput.value ?? '').trim().length > 0);

function clearSearch() {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = null;
  searchInput.value = '';
  page.value = 1;
  void fetchList(true);
}

async function reloadList(force = true) {
  await fetchList(force);
}

async function loadPoolFields() {
  const wsId = workspaceId.value;
  if (!wsId) {
    poolFields.value = [];
    return;
  }
  try {
    poolFields.value = await ocListPoolFieldsForWorkspace(wsId);
  } catch {
    poolFields.value = [];
  }
}

watch(workspaceId, () => {
  void loadPoolFields();
}, { immediate: true });

const canEdit = computed(() => store.boardContext?.permissions.canEdit === true);

type FormDialogMode = 'create' | 'edit';
const formDialogOpen = ref(false);
const formDialogMode = ref<FormDialogMode>('create');
const editWorkItemId = ref<string | null>(null);

function openCreateDialog() {
  formDialogMode.value = 'create';
  editWorkItemId.value = null;
  formDialogOpen.value = true;
}

function openEditDialog(id: string) {
  formDialogMode.value = 'edit';
  editWorkItemId.value = id;
  formDialogOpen.value = true;
}

function onRefresh() {
  if (showKanban.value) {
    void store.refreshBoard();
  } else {
    void reloadList(true);
  }
}

function onWorkItemSaved() {
  if (showKanban.value) {
    void store.refreshBoard();
  } else {
    void reloadList(true);
  }
}

const deleteDialogOpen = ref(false);
const deleting = ref(false);
const deleteError = ref<string | null>(null);
const deleteTarget = ref<{ id: string; key: string; title: string } | null>(null);

function askDelete(row: { id: string; keyText: string; titleText: string }) {
  deleteTarget.value = { id: row.id, key: row.keyText, title: row.titleText };
  deleteError.value = null;
  deleteDialogOpen.value = true;
}

async function confirmDelete() {
  const target = deleteTarget.value;
  if (!target) return;
  deleting.value = true;
  deleteError.value = null;
  try {
    await ocDeleteWorkItem(target.id);
    deleteDialogOpen.value = false;
    deleteTarget.value = null;
    if (showKanban.value) {
      await store.refreshBoard();
    } else {
      await reloadList(true);
    }
  } catch (e: unknown) {
    deleteError.value = ocExtractDgErrorMessage(e, t('operationCore.board.actions.deleteError'));
  } finally {
    deleting.value = false;
  }
}

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
  applyDefaultDisplayMode();
  // Liste görünümü server-side ilk sayfayı çeker (kanban kolon sorgularıyla yüklenir).
  lastSignature = '';
  if (showList.value) {
    void fetchList(true);
  }
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
          :loading="store.loadingBoardContext || store.listLoading"
          @click="onRefresh()"
        >
          <v-icon icon="mdi-refresh" start size="18" />
          {{ t('operationCore.board.refresh') }}
        </v-btn>
        <v-btn
          v-if="canEdit"
          color="primary"
          size="small"
          variant="flat"
          rounded="lg"
          class="text-none"
          @click="openCreateDialog"
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
        <div class="pa-3 pb-0">
          <div class="d-flex align-center flex-wrap ga-2 mb-2">
            <v-text-field
              v-model="searchInput"
              :placeholder="t('operationCore.board.searchPlaceholder')"
              prepend-inner-icon="mdi-magnify"
              variant="outlined"
              density="compact"
              hide-details
              clearable
              style="max-width: 360px"
              @click:clear="clearSearch"
            />
            <v-btn
              v-if="hasSearch"
              variant="text"
              size="small"
              class="text-none"
              prepend-icon="mdi-close"
              @click="clearSearch"
            >
              {{ t('operationCore.board.searchClear') }}
            </v-btn>
            <v-spacer />
            <v-alert
              v-if="store.listError"
              type="error"
              variant="tonal"
              density="compact"
              class="mb-0 py-1"
            >
              {{ store.listError }}
            </v-alert>
          </div>
          <OcBoardListFilters
            v-if="filterableColumns.length"
            :columns="filterableColumns"
            :state-options="stateFilterOptions"
            :priority-options="priorityFilterOptions"
            :type-options="typeFilterOptions"
            class="mb-2"
            @update:filters="onFiltersUpdate"
          />
        </div>
        <v-divider />
        <v-data-table-server
          :headers="listHeaders"
          :items="listRows"
          :items-length="store.listTotal"
          :page="page"
          :items-per-page="itemsPerPage"
          :items-per-page-options="itemsPerPageOptions"
          :sort-by="sortBy"
          item-value="id"
          density="comfortable"
          :loading="store.listLoading"
          class="oc-board-list-table"
          @update:options="onListOptions"
        >
          <template v-if="listColumnKeys.includes('key')" #item.key="{ item }">
            <NuxtLink :to="item.profileTo" class="text-primary font-weight-medium text-decoration-none">
              {{ item.key }}
            </NuxtLink>
          </template>
          <template v-if="listColumnKeys.includes('title')" #item.title="{ item }">
            <NuxtLink :to="item.profileTo" class="text-decoration-none text-reset">
              {{ item.title }}
            </NuxtLink>
          </template>
          <template v-if="listColumnKeys.includes('stateId')" #item.stateId="{ item }">
            <OcBoardCatalogLabel
              :item="resolveState(item.rawStateId, item.stateColumnTitle)"
            />
          </template>
          <template v-if="listColumnKeys.includes('priorityId')" #item.priorityId="{ item }">
            <OcBoardCatalogLabel :item="resolvePriority(item.rawPriorityId)" />
          </template>
          <template v-if="listColumnKeys.includes('typeId')" #item.typeId="{ item }">
            <OcBoardCatalogLabel :item="resolveType(item.rawTypeId)" />
          </template>
          <template v-if="listColumnKeys.includes('assignee')" #item.assignee="{ item }">
            <span>{{ resolveAssigneeName(item.rawAssignee) }}</span>
          </template>
          <template v-if="listColumnKeys.includes('createdBy')" #item.createdBy="{ item }">
            <span>{{ resolveAssigneeName(item.rawCreatedBy) }}</span>
          </template>
          <template v-if="listColumnKeys.includes('sla')" #item.sla="{ item }">
            <OcSlaStatusChip
              :sla="item.__card?.sla"
              :state-id="item.rawStateId"
              :initial-state-id="initialStateId"
              :closed-at="item.__card?.closedAt"
              dense
            />
          </template>
          <template #item.actions="{ item }">
            <div class="d-inline-flex align-center justify-end ga-1">
              <v-btn
                icon="mdi-eye-outline"
                variant="text"
                size="small"
                density="comfortable"
                :to="item.profileTo"
                :title="t('operationCore.board.actions.viewProfile')"
              />
              <v-btn
                v-if="canEdit"
                icon="mdi-pencil-outline"
                variant="text"
                size="small"
                density="comfortable"
                :title="t('operationCore.board.actions.edit')"
                @click="openEditDialog(item.id)"
              />
              <v-btn
                v-if="canEdit"
                icon="mdi-trash-can-outline"
                variant="text"
                size="small"
                density="comfortable"
                color="error"
                :title="t('operationCore.board.actions.delete')"
                @click="askDelete(item)"
              />
            </div>
          </template>
        </v-data-table-server>
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

    <OcWorkItemFormDialog
      v-if="store.boardContext"
      v-model="formDialogOpen"
      :mode="formDialogMode"
      :workspace-id="store.boardContext.workspaceId"
      :board-id="store.boardContext.boardId"
      :work-item-id="editWorkItemId"
      @saved="onWorkItemSaved"
    />

    <v-dialog v-model="deleteDialogOpen" max-width="460" persistent>
      <v-card rounded="xl">
        <v-card-title class="d-flex align-center ga-2 pt-4">
          <v-icon icon="mdi-alert-circle-outline" color="error" />
          <span class="text-h6 font-weight-bold">{{ t('operationCore.board.actions.deleteTitle') }}</span>
        </v-card-title>
        <v-card-text>
          <p class="text-body-2 mb-2">
            {{ t('operationCore.board.actions.deleteConfirm') }}
          </p>
          <p v-if="deleteTarget" class="text-body-2 font-weight-medium mb-0">
            <span v-if="deleteTarget.key" class="text-primary">{{ deleteTarget.key }}</span>
            <span v-if="deleteTarget.key && deleteTarget.title"> — </span>
            <span>{{ deleteTarget.title }}</span>
          </p>
          <p class="text-caption text-medium-emphasis mt-2 mb-0">
            {{ t('operationCore.board.actions.deleteIrreversible') }}
          </p>
          <v-alert
            v-if="deleteError"
            type="error"
            variant="tonal"
            class="mt-3 rounded-lg"
            density="compact"
          >
            {{ deleteError }}
          </v-alert>
        </v-card-text>
        <v-card-actions class="px-4 pb-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" :disabled="deleting" @click="deleteDialogOpen = false">
            {{ t('operationCore.create.cancel') }}
          </v-btn>
          <v-btn
            color="error"
            variant="flat"
            rounded="lg"
            class="text-none"
            :loading="deleting"
            @click="confirmDelete"
          >
            {{ t('operationCore.board.actions.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.min-width-0 {
  min-width: 0;
}

/* "İşlemler" sütunu (her zaman son sütun) sağa sabitlenir — çok sütunlu/yatay scroll'da hep görünür. */
.oc-board-list-table :deep(table) > thead > tr > th:last-child,
.oc-board-list-table :deep(table) > tbody > tr > td:last-child {
  position: sticky;
  right: 0;
  background: rgb(var(--v-theme-surface));
  box-shadow: -6px 0 6px -6px rgba(0, 0, 0, 0.18);
}

.oc-board-list-table :deep(table) > tbody > tr > td:last-child {
  z-index: 1;
}

.oc-board-list-table :deep(table) > thead > tr > th:last-child {
  z-index: 2;
}
</style>
