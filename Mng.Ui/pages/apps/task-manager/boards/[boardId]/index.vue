<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { VueDraggableNext } from 'vue-draggable-next';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { useUserStore } from '@/stores/apps/user';
import { useAuthStore } from '@/stores/auth';
import { assigneeUserId } from '@/composables/useTaskManagerHelpers';
import TmIssueCard from '@/components/apps/task-manager/TmIssueCard.vue';
import type { TmIssue } from '@/types/apps/taskManager';
import {
  resolveKanbanColumns,
  orphanStatusColumns,
  getEffectiveWorkflow,
  isTransitionAllowed,
  getInitialStatusId,
  projectUsesKanban,
} from '@/utils/taskManagerWorkflow';
import {
  resolveBoardTableColumnIds,
  buildBoardTableRow,
  boardTableColumnTitle,
} from '@/utils/boardTableColumns';
import {
  emptyIssueForm,
  issueCreateDialogMaxWidth,
  resolveEffectiveIssueCreateLayout,
  resolveNewIssueFormRows,
  normalizeDueDateInput,
  pruneIssueExtraFields,
} from '@/utils/taskManagerNewIssueForm';
import type { IssueFormModel } from '@/utils/taskManagerNewIssueForm';
import TmNewIssueFormFields from '@/components/apps/task-manager/TmNewIssueFormFields.vue';
import TmIssueEditDialog from '@/components/apps/task-manager/TmIssueEditDialog.vue';

definePageMeta({ layout: 'default' });

const route = useRoute();
const boardId = computed(() => String(route.params.boardId ?? ''));

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const store = useTaskManagerStore();
const userStore = useUserStore();
const auth = useAuthStore();

/** Tablo sütunları ayarı: yalnızca admin veya manager */
const canConfigureBoardColumns = computed(() => auth.isManager);
const columnIssues = ref<Record<string, TmIssue[]>>({});

const searchQuery = ref('');
const assigneeFilter = ref<string | null>(null);
const priorityFilter = ref<string | null>(null);

const board = computed(() => store.boards.find((b) => b.__dataId === boardId.value));
const project = computed(() => (board.value ? store.projects.find((p) => p.__dataId === board.value!.projectId) : null));
const projectId = computed(() => board.value?.projectId ?? '');

const listOnly = computed(() => !projectUsesKanban(project.value));

const filterActive = computed(
  () => !!searchQuery.value.trim() || !!assigneeFilter.value || !!priorityFilter.value
);

const projectIssues = computed(() => store.issues.filter((i) => i.projectId === projectId.value));

const filteredIssues = computed(() => {
  let list = projectIssues.value;
  const q = searchQuery.value.trim().toLowerCase();
  if (q) {
    list = list.filter((i) => i.title.toLowerCase().includes(q) || i.key.toLowerCase().includes(q));
  }
  if (assigneeFilter.value) {
    list = list.filter((i) => assigneeUserId(i.assignee) === assigneeFilter.value);
  }
  if (priorityFilter.value) {
    list = list.filter((i) => i.priorityId === priorityFilter.value);
  }
  return list;
});

const columnDefs = computed(() => {
  const base = resolveKanbanColumns(board.value, project.value ?? null, store.statuses);
  const wf = getEffectiveWorkflow(project.value ?? null, store.statuses);
  const orphan = orphanStatusColumns(
    filteredIssues.value.map((i) => i.statusId),
    wf,
    store.statuses
  );
  const seen = new Set(base.map((c) => c.statusId));
  const extra = orphan.filter((c) => !seen.has(c.statusId));
  return [...base, ...extra];
});

function rebuildColumns() {
  const next: Record<string, TmIssue[]> = {};
  for (const c of columnDefs.value) {
    next[c.statusId] = filteredIssues.value
      .filter((i) => i.statusId === c.statusId)
      .sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  }
  columnIssues.value = next;
}

watch(
  () => [filteredIssues.value, board.value?.config, store.statuses],
  () => rebuildColumns(),
  { deep: true }
);

function priorityColor(issue: TmIssue): string | null {
  if (!issue.priorityId) return null;
  return store.priorities.find((p) => p.__dataId === issue.priorityId)?.color ?? null;
}

function typeName(issue: TmIssue): string {
  return store.issueTypes.find((t) => t.__dataId === issue.issueTypeId)?.name ?? '';
}

function assigneeInitials(issue: TmIssue): string {
  const id = assigneeUserId(issue.assignee);
  if (!id) return '';
  const u = userStore.getUserById(id);
  if (!u) return id.slice(0, 2).toUpperCase();
  const a = `${u.firstName?.[0] || ''}${u.lastName?.[0] || ''}`;
  if (a) return a.toUpperCase();
  return (u.username || '?').slice(0, 2).toUpperCase();
}

const boardColumnIds = computed(() =>
  resolveBoardTableColumnIds(board.value ?? null, project.value ?? null, store.fieldDefinitions)
);

const boardTableCtx = computed(() => ({
  store,
  userStore,
  labels: store.labels,
}));

const tableHeaders = computed(() => [
  ...boardColumnIds.value.map((id) => ({
    title: boardTableColumnTitle(id, store.fieldDefinitions, mt),
    key: id,
    sortable: true,
  })),
  {
    title: mt('taskManager.tableColumnActions', 'İşlem'),
    key: 'actions',
    sortable: false,
    width: 136,
    align: 'center' as const,
  },
]);

const tableItems = computed(() =>
  filteredIssues.value.map((i) => buildBoardTableRow(i, boardColumnIds.value, boardTableCtx.value))
);

const issueEditOpen = ref(false);
const issueBeingEdited = ref<TmIssue | null>(null);

const issueEditProject = computed(() => {
  const i = issueBeingEdited.value;
  if (!i) return project.value ?? null;
  return store.projects.find((p) => p.__dataId === i.projectId) ?? project.value ?? null;
});

watch(issueEditOpen, (v) => {
  if (!v) issueBeingEdited.value = null;
});

function resolveIssueFromTableRow(item: Record<string, unknown>): TmIssue | undefined {
  const id = String(item.__issueId ?? '');
  return projectIssues.value.find((x) => x.__dataId === id);
}

function issueProfilePathFromRow(item: Record<string, unknown>): string {
  const k = item?.key;
  const key = typeof k === 'string' && k ? k : resolveIssueFromTableRow(item)?.key ?? '';
  if (!key) return '#';
  const qs = new URLSearchParams();
  qs.set('from', 'board');
  if (boardId.value) qs.set('board', boardId.value);
  return `/apps/task-manager/issues/${encodeURIComponent(key)}/profile?${qs.toString()}`;
}

function openIssueEditFromTableRow(item: Record<string, unknown>) {
  const iss = resolveIssueFromTableRow(item);
  if (iss) {
    issueBeingEdited.value = iss;
    issueEditOpen.value = true;
  }
}

function onListRowClick(_e: unknown, row: { item: Record<string, unknown> }) {
  openIssueEditFromTableRow(row.item);
}

async function onIssueEditSaved() {
  const id = issueBeingEdited.value?.__dataId;
  const pid = projectId.value;
  if (pid) await store.loadIssues(pid);
  rebuildColumns();
  if (id) {
    issueBeingEdited.value = store.issues.find((x) => x.__dataId === id) ?? issueBeingEdited.value;
  }
}

const issueDeleteDialog = ref(false);
const issuePendingDelete = ref<TmIssue | null>(null);
const deletingIssue = ref(false);

function requestDeleteIssueFromRow(item: Record<string, unknown>) {
  const iss = resolveIssueFromTableRow(item);
  if (!iss) return;
  issuePendingDelete.value = iss;
  issueDeleteDialog.value = true;
}

async function confirmDeleteIssueFromTable() {
  const i = issuePendingDelete.value;
  if (!i) return;
  deletingIssue.value = true;
  try {
    await store.deleteIssue(i.__dataId, i.projectId);
    issueDeleteDialog.value = false;
    issuePendingDelete.value = null;
    if (issueBeingEdited.value?.__dataId === i.__dataId) {
      issueEditOpen.value = false;
      issueBeingEdited.value = null;
    }
    const pid = projectId.value;
    if (pid) await store.loadIssues(pid);
    rebuildColumns();
  } finally {
    deletingIssue.value = false;
  }
}

const assigneeItems = computed(() => {
  const m = new Map<string, string>();
  for (const i of projectIssues.value) {
    const id = assigneeUserId(i.assignee);
    if (!id || m.has(id)) continue;
    const u = userStore.getUserById(id);
    m.set(id, u ? `${u.firstName} ${u.lastName}`.trim() || u.username : id);
  }
  return [...m.entries()].map(([value, title]) => ({ value, title }));
});

const effectiveIssueCreateLayout = computed(() =>
  resolveEffectiveIssueCreateLayout(project.value ?? null, board.value ?? null)
);

const issueDialogMaxWidth = computed(() => issueCreateDialogMaxWidth(effectiveIssueCreateLayout.value));

const issueFormRows = computed(() =>
  resolveNewIssueFormRows(project.value ?? null, store.fieldDefinitions, effectiveIssueCreateLayout.value)
);

const issueTypeSelectItems = computed(() => {
  const p = project.value;
  const ids = p?.selections?.issueTypeIds;
  let list = store.issueTypes;
  if (ids?.length) list = list.filter((t) => ids.includes(t.__dataId));
  return list.map((t) => ({ title: t.name, value: t.__dataId }));
});

const prioritySelectItems = computed(() => {
  const p = project.value;
  const ids = p?.selections?.priorityIds;
  let list = store.priorities;
  if (ids?.length) list = list.filter((x) => ids.includes(x.__dataId));
  return list.map((x) => ({ title: x.name, value: x.__dataId }));
});

const labelSelectItems = computed(() => {
  const pid = projectId.value;
  if (!pid) return [];
  return store.labels
    .filter((l) => l.projectId === pid)
    .map((l) => ({ title: l.name, value: l.__dataId }));
});

const userSelectItems = computed(() =>
  userStore.activeUsers.map((u) => ({
    title: `${u.firstName} ${u.lastName}`.trim() || u.username,
    value: u.id,
  }))
);

const newIssueDialogSubtitle = computed(() => {
  const p = project.value;
  const b = board.value;
  if (!p) return '';
  if (b) return `${p.name} · ${b.name}`;
  return p.name;
});

const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('taskManager.pageTitle', 'Görevler'), disabled: false, href: '/apps/task-manager' },
  { text: mt('taskManager.projectsListTitle', 'Projeler'), disabled: false, href: '/apps/task-manager/projects' },
  { text: project.value?.name ?? '…', disabled: false, href: project.value ? `/apps/task-manager/projects/${project.value.__dataId}` : '#' },
  { text: board.value?.name ?? 'Board', disabled: true, href: '#' },
]);

const issueDialog = ref(false);
const issueForm = ref<IssueFormModel>(emptyIssueForm());
const creating = ref(false);
const transitionSnackbar = ref(false);
const transitionMessage = ref('');

function clearFilters() {
  searchQuery.value = '';
  assigneeFilter.value = null;
  priorityFilter.value = null;
}

onMounted(async () => {
  try {
    await userStore.fetchUsers({ page: 1, pageSize: 500, isActive: true }).catch(() => {});
    await store.loadLookups();
    await store.loadFieldDefinitions().catch(() => {});
    await store.loadProjects();
    await store.loadBoard(boardId.value);
    const b = board.value;
    if (b?.projectId) {
      await store.loadLabels(b.projectId).catch(() => {});
      await store.loadIssues(b.projectId);
      rebuildColumns();
    }
  } catch (e: any) {
    store.error = e?.message ?? 'Yükleme hatası';
  }
});

async function persistOrdersForColumns(statusIds: string[]) {
  const pid = projectId.value;
  if (!pid) return;
  const seen = new Set<string>();
  for (const sid of statusIds) {
    if (!sid || seen.has(sid)) continue;
    seen.add(sid);
    const list = columnIssues.value[sid] ?? [];
    for (let i = 0; i < list.length; i++) {
      const issue = list[i];
      const statusMismatch = issue.statusId !== sid;
      const orderMismatch = (issue.order ?? -1) !== i;
      if (!statusMismatch && !orderMismatch) continue;
      const patch: Record<string, unknown> = { order: i, projectId: pid };
      if (statusMismatch) patch.statusId = sid;
      await store.updateIssue(issue.__dataId, patch as any, { skipReload: true });
    }
  }
  await store.loadIssues(pid);
  rebuildColumns();
}

async function onKanbanChange(colStatusId: string, evt: any) {
  if (filterActive.value) return;
  const pid = projectId.value;
  if (!pid || !evt) return;
  const wf = getEffectiveWorkflow(project.value ?? null, store.statuses);
  try {
    if (evt.added) {
      const issue = evt.added.element as TmIssue;
      const from = issue?.statusId;
      if (from && !isTransitionAllowed(wf, from, colStatusId)) {
        transitionMessage.value = mt(
          'taskManager.transitionDenied',
          'Bu durum geçişine izin verilmiyor. İş akışı ayarlarını kontrol edin.'
        );
        transitionSnackbar.value = true;
        await store.loadIssues(pid);
        rebuildColumns();
        return;
      }
      await persistOrdersForColumns([colStatusId, from].filter(Boolean) as string[]);
    } else if (evt.removed || evt.moved) {
      await persistOrdersForColumns([colStatusId]);
    }
  } catch (_) {
    await store.loadIssues(pid);
    rebuildColumns();
  }
}

function openIssueDialog() {
  issueForm.value = emptyIssueForm();
  issueDialog.value = true;
}

async function createIssue() {
  const pid = projectId.value;
  const pk = project.value?.key;
  const st =
    getInitialStatusId(project.value ?? null, store.statuses) ?? columnDefs.value[0]?.statusId ?? store.firstStatusId;
  const it = issueForm.value.issueTypeId ?? store.defaultTaskIssueTypeId;
  if (!pid || !pk || !st || !it || !issueForm.value.title.trim()) return;
  creating.value = true;
  try {
    await store.createIssue({
      projectId: pid,
      projectKey: pk,
      title: issueForm.value.title,
      description: issueForm.value.description.trim() || undefined,
      statusId: st,
      issueTypeId: it,
      priorityId: issueForm.value.priorityId || undefined,
      assignee: issueForm.value.assignee || undefined,
      labels: issueForm.value.labels?.length ? issueForm.value.labels : undefined,
      dueDate: normalizeDueDateInput(issueForm.value.dueDate) || undefined,
      storyPoints:
        issueForm.value.storyPoints != null && !Number.isNaN(Number(issueForm.value.storyPoints))
          ? Number(issueForm.value.storyPoints)
          : undefined,
      extraFields: pruneIssueExtraFields(issueForm.value.extra),
      initialComment: issueForm.value.initialComment.trim() || undefined,
      initialCommentAuthorId: auth.userInfo?.sub ?? null,
    });
    issueForm.value = emptyIssueForm();
    issueDialog.value = false;
    rebuildColumns();
  } finally {
    creating.value = false;
  }
}
</script>

<template>
  <div class="tm-flow">
    <BaseBreadcrumb
      :title="board?.name ?? (listOnly ? mt('taskManager.boardListViewTitle', 'Liste') : 'Kanban')"
      :breadcrumbs="breadcrumbs"
    />
    <v-alert v-if="store.error" type="error" variant="tonal" class="mb-4" closable @click:close="store.error = null">
      {{ store.error }}
    </v-alert>
    <v-alert v-if="!board" type="warning" variant="tonal" class="mb-4">
      {{ mt('taskManager.boardNotFound', 'Board bulunamadı.') }}
    </v-alert>

    <template v-else>
      <div class="tm-hero d-flex flex-column flex-md-row flex-wrap align-start align-md-center justify-space-between gap-3 mb-4">
        <div>
          <div class="tm-hero-title text-h5">{{ board.name }}</div>
          <div class="tm-hero-sub">
            {{
              project?.key
            }}
            ·
            {{
              listOnly
                ? mt('taskManager.listViewHint', 'Bu projede liste görünümü kullanılıyor; durumu görev detayından güncelleyin.')
                : mt('taskManager.kanbanHint', 'Sürükleyerek durum değiştirin. Filtre açıkken sürükleme kapalıdır.')
            }}
          </div>
        </div>
        <div class="d-flex flex-wrap gap-2">
          <v-btn
            v-if="canConfigureBoardColumns"
            variant="outlined"
            rounded="lg"
            size="large"
            class="text-none"
            :to="`/apps/task-manager/boards/${boardId}/settings`"
          >
            <v-icon icon="mdi-table-cog" start />
            {{ mt('taskManager.boardSettingsTitle', 'Tablo sütunları') }}
          </v-btn>
          <v-btn color="primary" rounded="lg" size="large" class="text-none" @click="openIssueDialog">
            {{ mt('taskManager.newIssue', 'Yeni görev') }}
          </v-btn>
        </div>
      </div>

      <div class="tm-panel pa-4 mb-4">
        <div class="d-flex flex-wrap tm-toolbar-filters align-center">
          <v-text-field
            v-model="searchQuery"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            style="min-width: 200px; max-width: 280px"
            :placeholder="mt('taskManager.searchIssues', 'Görev veya anahtar ara…')"
            prepend-inner-icon="mdi-magnify"
          />
          <v-select
            v-model="assigneeFilter"
            :items="assigneeItems"
            item-title="title"
            item-value="value"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            style="min-width: 180px; max-width: 220px"
            :label="mt('taskManager.filterAssignee', 'Atanan')"
          />
          <v-select
            v-model="priorityFilter"
            :items="store.priorities.map((p) => ({ title: p.name, value: p.__dataId }))"
            item-title="title"
            item-value="value"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            style="min-width: 160px; max-width: 220px"
            :label="mt('taskManager.filterPriority', 'Öncelik')"
          />
          <v-btn v-if="filterActive" variant="text" size="small" class="text-none" @click="clearFilters">
            {{ mt('taskManager.clearFilters', 'Filtreleri temizle') }}
          </v-btn>
        </div>
        <v-alert v-if="filterActive && !listOnly" type="info" variant="tonal" density="compact" class="mt-3 mb-0">
          {{ mt('taskManager.filterBlocksDrag', 'Filtre etkin: sıralama sürüklemesi devre dışı.') }}
        </v-alert>
      </div>

      <v-data-table
        v-if="listOnly"
        item-value="__issueId"
        :headers="tableHeaders"
        :items="tableItems"
        :items-per-page="25"
        class="tm-board-list-table tm-panel elevation-0"
        hover
        @click:row="onListRowClick"
      >
        <template #item.actions="{ item }">
          <div class="d-inline-flex align-center justify-center">
            <v-btn
              icon="mdi-card-account-details-outline"
              size="small"
              variant="text"
              :to="issueProfilePathFromRow(item)"
              :aria-label="mt('taskManager.openIssueProfile', 'Profil')"
              :title="mt('taskManager.openIssueProfile', 'Profil')"
              @click.stop
            />
            <v-btn
              icon="mdi-pencil"
              size="small"
              variant="text"
              :aria-label="mt('taskManager.editIssue', 'Görevi düzenle')"
              :title="mt('taskManager.editIssue', 'Görevi düzenle')"
              @click.stop="openIssueEditFromTableRow(item)"
            />
            <v-btn
              icon="mdi-delete-outline"
              size="small"
              variant="text"
              color="error"
              :aria-label="mt('taskManager.deleteIssue', 'Görevi sil')"
              :title="mt('taskManager.deleteIssue', 'Görevi sil')"
              @click.stop="requestDeleteIssueFromRow(item)"
            />
          </div>
        </template>
      </v-data-table>

      <div v-else class="tm-kanban-wrap">
        <div class="d-flex flex-nowrap pb-2" style="overflow-x: auto; align-items: flex-start; gap: 12px">
          <div v-for="col in columnDefs" :key="col.statusId" class="tm-col">
            <div class="tm-col-head">{{ col.title }}</div>
            <VueDraggableNext
              :list="columnIssues[col.statusId] ?? []"
              group="tm-kanban"
              item-key="__dataId"
              class="d-flex flex-column ga-2"
              :disabled="filterActive"
              @change="(e) => onKanbanChange(col.statusId, e)"
            >
              <TmIssueCard
                v-for="element in columnIssues[col.statusId] ?? []"
                :key="element.__dataId"
                :issue="element"
                :board-id="boardId"
                :priority-color="priorityColor(element)"
                :type-name="typeName(element)"
                :assignee-initials="assigneeInitials(element)"
              />
            </VueDraggableNext>
          </div>
        </div>
      </div>
    </template>

    <v-dialog
      v-model="issueDialog"
      :max-width="issueDialogMaxWidth"
      scrollable
      content-class="tm-new-issue-dialog-overlay"
      @keyup.escape="issueDialog = false"
    >
      <v-card class="tm-new-issue-dialog tm-flow" rounded="xl" elevation="12">
        <v-card-item class="pb-0 pt-6 px-6">
          <v-card-title class="text-h6 font-weight-bold pa-0 text-wrap">
            {{ mt('taskManager.newIssue', 'Yeni görev') }}
          </v-card-title>
          <v-card-subtitle v-if="newIssueDialogSubtitle" class="text-body-2 pa-0 mt-2 text-medium-emphasis">
            {{ newIssueDialogSubtitle }}
          </v-card-subtitle>
        </v-card-item>
        <v-card-text class="px-6 pt-5 pb-2">
          <TmNewIssueFormFields
            v-model="issueForm"
            :rows="issueFormRows"
            :field-definitions="store.fieldDefinitions"
            :issue-type-items="issueTypeSelectItems"
            :priority-items="prioritySelectItems"
            :label-items="labelSelectItems"
            :user-items="userSelectItems"
            :issue-create-layout="effectiveIssueCreateLayout"
          />
          <div class="tm-new-issue-initial-comment mt-6">
            <div class="text-subtitle-2 font-weight-medium mb-2">
              {{ mt('taskManager.newIssueInitialComment', 'İlk yorum (isteğe bağlı)') }}
            </div>
            <v-textarea
              v-model="issueForm.initialComment"
              :placeholder="
                mt('taskManager.newIssueInitialCommentHint', 'Görev oluşturulunca bu metin ilk yorum olarak eklenir.')
              "
              rows="2"
              auto-grow
              density="compact"
              variant="outlined"
              hide-details="auto"
            />
          </div>
        </v-card-text>
        <v-divider class="border-opacity-25" />
        <v-card-actions class="px-6 py-4 d-flex align-center flex-wrap gap-2">
          <v-btn variant="text" class="text-none" rounded="lg" @click="issueDialog = false">
            {{ mt('taskManager.cancel', 'İptal') }}
          </v-btn>
          <v-spacer />
          <v-btn
            color="primary"
            variant="flat"
            size="large"
            rounded="lg"
            class="text-none px-6"
            prepend-icon="mdi-check"
            :loading="creating"
            :disabled="!issueForm.title.trim()"
            @click="createIssue"
          >
            {{ mt('taskManager.save', 'Kaydet') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <TmIssueEditDialog
      v-model="issueEditOpen"
      :issue="issueBeingEdited"
      :project="issueEditProject"
      :board="board ?? null"
      @saved="onIssueEditSaved"
    />

    <v-dialog v-model="issueDeleteDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.deleteIssueTitle', 'Görev silinsin mi?') }}</v-card-title>
        <v-card-text>
          <template v-if="issuePendingDelete">
            <span class="font-weight-medium">{{ issuePendingDelete.key }}</span>
            <span class="text-medium-emphasis"> — {{ issuePendingDelete.title }}</span>
          </template>
          <div class="text-body-2 mt-2">{{ mt('taskManager.deleteIssueBody', 'Bu işlem geri alınamaz.') }}</div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" rounded="lg" @click="issueDeleteDialog = false">
            {{ mt('taskManager.cancel', 'İptal') }}
          </v-btn>
          <v-btn color="error" variant="flat" rounded="lg" class="text-none" :loading="deletingIssue" @click="confirmDeleteIssueFromTable">
            {{ mt('taskManager.delete', 'Sil') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="transitionSnackbar" color="error" location="top" timeout="4000">
      {{ transitionMessage }}
    </v-snackbar>
  </div>
</template>
