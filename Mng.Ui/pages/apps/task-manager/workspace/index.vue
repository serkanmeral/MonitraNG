<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import TmWorkspaceTree from '@/components/apps/task-manager/TmWorkspaceTree.vue';
import { useResizableTreePanel } from '@/composables/useResizableTreePanel';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';
import { assigneeUserId, assigneeDisplayLabel } from '@/composables/useTaskManagerHelpers';
import { tmListDataset, TM_DATASETS } from '@/services/taskManagerService';
import {
  TM_WORKSPACE_FILTER_ASSIGNED_TO_ME as TM_FILTER_ASSIGNED_TO_ME,
  type TmTreeProjectNode,
  type TmIssue,
} from '@/types/apps/taskManager';
import { getEffectiveWorkflow, projectUsesKanban } from '@/utils/taskManagerWorkflow';
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
import {
  LayoutSidebarLeftCollapseIcon,
  LayoutSidebarLeftExpandIcon,
  ChevronDownIcon,
  ChevronUpIcon,
} from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const router = useRouter();
const route = useRoute();
const store = useTaskManagerStore();
const auth = useAuthStore();
const userStore = useUserStore();

const canEdit = computed(() => auth.isManager);

/** Yeni proje: yalnızca admin veya manager (JWT: isAdmin / is_manager) */
const canCreateProject = computed(() => auth.isAdmin || auth.isManager);

/** Tablo sütunları (board ayarı): yalnızca admin veya manager — store.isManager ikisini de kapsar */
const canConfigureBoardColumns = computed(() => auth.isManager);

const {
  treeWidth,
  treeCollapsed,
  resizeActive,
  startResize,
  toggleTreeCollapse,
} = useResizableTreePanel('task-manager-workspace-tree', {
  minWidth: 220,
  maxWidth: 480,
  defaultWidth: 300,
});

const treeRef = ref<InstanceType<typeof TmWorkspaceTree> | null>(null);

const selectedProjectId = ref<string | null>(null);
const selectedBoardId = ref<string | null>(null);
const selectedFilterId = ref<string | null>(null);
const filterIssuesCache = ref<TmIssue[] | null>(null);
const loadingFilterIssues = ref(false);
const listSearch = ref('');

const TM_BOARD_FORM_PROJECT_DEFAULT = '__tm_project_default__';

const boardDialog = ref(false);
const boardName = ref('');
/** Proje varsayılanı için sentinel; aksi halde form şablonu id */
const newBoardIssueFormId = ref<string>(TM_BOARD_FORM_PROJECT_DEFAULT);
const newBoardIssueProfileFormId = ref<string>(TM_BOARD_FORM_PROJECT_DEFAULT);
const savingBoard = ref(false);
const treeLoading = ref(false);

const issueDialog = ref(false);
const issueForm = ref<IssueFormModel>(emptyIssueForm());
const creatingIssue = ref(false);

const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('taskManager.pageTitle', 'Görevler'), disabled: false, href: '/apps/task-manager' },
  { text: mt('taskManager.workspaceExplorerTitle', 'Çalışma alanı'), disabled: true, href: '#' },
]);

const treeNodes = computed((): TmTreeProjectNode[] => {
  return store.visibleProjects.map((p) => ({
    type: 'project' as const,
    data: p,
    children: store.boardsForProject(p.__dataId).map((b) => ({
      type: 'board' as const,
      data: b,
    })),
  }));
});

const selectedBoard = computed(() =>
  selectedBoardId.value ? store.boards.find((b) => b.__dataId === selectedBoardId.value) ?? null : null
);

const selectedProject = computed(() =>
  selectedProjectId.value ? store.projects.find((p) => p.__dataId === selectedProjectId.value) ?? null : null
);

/** Seçili board’un projesi (Kanban / liste butonu için board üzerinden çözülür) */
const selectedBoardProject = computed(() => {
  const b = selectedBoard.value;
  if (!b) return null;
  return store.projects.find((p) => p.__dataId === b.projectId) ?? null;
});

/** Proje seçili, board yokken sağ panel özeti */
const projectOverview = computed(() => {
  const p = selectedProject.value;
  const pid = selectedProjectId.value;
  if (!p || !pid) return null;
  const boards = store.boardsForProject(pid);
  const issueCount = store.issues.filter((i) => i.projectId === pid).length;
  const wf = getEffectiveWorkflow(p, store.statuses);
  const stepCount = wf.statusIds?.length ?? 0;
  const leadLabel = assigneeDisplayLabel(p.lead, (id) => userStore.getUserById(id));
  const sel = p.selections;

  const statusNames = (wf.statusIds ?? [])
    .map((id) => store.statusById(id)?.name)
    .filter((n): n is string => !!n);

  const priorityNames = (sel?.priorityIds ?? [])
    .map((id) => store.priorities.find((pr) => pr.__dataId === id)?.name)
    .filter((n): n is string => !!n);

  const issueTypeNames = (sel?.issueTypeIds ?? [])
    .map((id) => store.issueTypes.find((t) => t.__dataId === id)?.name)
    .filter((n): n is string => !!n);

  const fieldItems = (sel?.fieldKeys ?? []).map((key) => {
    const fd = store.fieldDefinitions.find((f) => f.key === key);
    return { key, label: fd?.label ?? key };
  });

  const openPerm = mt('taskManager.workspacePermUnrestricted', 'Kısıt yok');
  const personWord = mt('taskManager.workspacePermPersonsShort', 'kişi');
  const groupWord = mt('taskManager.workspacePermGroupsShort', 'grup');
  const perm = p.permissions;
  const permRows = (
    [
      { k: 'view' as const, title: mt('taskManager.permView', 'Görüntüleme') },
      { k: 'edit' as const, title: mt('taskManager.permEdit', 'Düzenleme') },
      { k: 'admin' as const, title: mt('taskManager.permAdmin', 'Yönetim') },
    ] as const
  ).map(({ k, title }) => {
    const v = perm?.[k];
    const pi = v?.personIds?.length ?? 0;
    const gi = v?.groupIds?.length ?? 0;
    let text: string;
    if (pi === 0 && gi === 0) text = openPerm;
    else {
      const parts: string[] = [];
      if (pi) parts.push(`${pi} ${personWord}`);
      if (gi) parts.push(`${gi} ${groupWord}`);
      text = parts.join(' · ');
    }
    return { title, text };
  });

  return {
    boardCount: boards.length,
    issueCount,
    stepCount,
    leadLabel,
    useKanban: projectUsesKanban(p),
    key: p.key,
    name: p.name,
    description: p.description?.trim() || '',
    avatarUrl: p.avatarUrl?.trim() || null,
    statusNames,
    priorityNames,
    issueTypeNames,
    fieldItems,
    permRows,
  };
});

const projectStatCards = computed(() => {
  const o = projectOverview.value;
  if (!o) return [];
  return [
    { value: o.boardCount, label: mt('taskManager.workspaceStatBoards', 'Board') },
    { value: o.issueCount, label: mt('taskManager.workspaceStatIssues', 'Görev') },
    { value: o.stepCount, label: mt('taskManager.workspaceStatSteps', 'Durum adımı') },
  ];
});

/** Board seçiliyken store.issues üzerinden (board kolon kapsamı) */
const boardListIssues = computed((): TmIssue[] => {
  if (!selectedBoardId.value || !selectedProjectId.value) return [];
  const board = selectedBoard.value;
  let list = store.issues.filter((i) => i.projectId === selectedProjectId.value);
  const cols = board?.config?.columns;
  if (cols?.length) {
    const allow = new Set(cols.map((c) => c.statusId));
    list = list.filter((i) => allow.has(i.statusId));
  }
  return [...list].sort((a, b) => (a.key || '').localeCompare(b.key || ''));
});

const activeIssues = computed((): TmIssue[] => {
  let list: TmIssue[];
  if (selectedFilterId.value === TM_FILTER_ASSIGNED_TO_ME && filterIssuesCache.value) {
    list = filterIssuesCache.value;
  } else {
    list = boardListIssues.value;
  }
  const q = listSearch.value.trim().toLowerCase();
  if (!q) return list;
  return list.filter((i) => i.title.toLowerCase().includes(q) || i.key.toLowerCase().includes(q));
});

const tableLoading = computed(() => {
  if (selectedFilterId.value === TM_FILTER_ASSIGNED_TO_ME) return loadingFilterIssues.value;
  return store.loading && !!selectedBoardId.value;
});

const firstStatusForCreate = computed(() => {
  const board = selectedBoard.value;
  const cols = board?.config?.columns;
  if (cols?.length) return cols[0].statusId;
  return store.firstStatusId;
});

const effectiveIssueCreateLayout = computed(() =>
  resolveEffectiveIssueCreateLayout(selectedProject.value ?? null, selectedBoard.value ?? null)
);

const issueDialogMaxWidth = computed(() => issueCreateDialogMaxWidth(effectiveIssueCreateLayout.value));

const issueFormRows = computed(() =>
  resolveNewIssueFormRows(selectedProject.value ?? null, store.fieldDefinitions, effectiveIssueCreateLayout.value)
);

/** Yeni görev modalı — proje / board bağlamı */
const newIssueDialogSubtitle = computed(() => {
  const p = selectedProject.value;
  const b = selectedBoard.value;
  if (!p) return '';
  if (b) return `${p.name} · ${b.name}`;
  return p.name;
});

const issueTypeSelectItems = computed(() => {
  const p = selectedProject.value;
  const ids = p?.selections?.issueTypeIds;
  let list = store.issueTypes;
  if (ids?.length) list = list.filter((t) => ids.includes(t.__dataId));
  return list.map((t) => ({ title: t.name, value: t.__dataId }));
});

const prioritySelectItems = computed(() => {
  const p = selectedProject.value;
  const ids = p?.selections?.priorityIds;
  let list = store.priorities;
  if (ids?.length) list = list.filter((x) => ids.includes(x.__dataId));
  return list.map((x) => ({ title: x.name, value: x.__dataId }));
});

const labelSelectItems = computed(() => {
  const pid = selectedProjectId.value;
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

const boardColumnIds = computed(() =>
  resolveBoardTableColumnIds(selectedBoard.value ?? null, selectedProject.value ?? null, store.fieldDefinitions)
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
  activeIssues.value.map((i) => buildBoardTableRow(i, boardColumnIds.value, boardTableCtx.value))
);

function onSelectProject(projectId: string) {
  selectedProjectId.value = projectId;
  selectedBoardId.value = null;
  selectedFilterId.value = null;
  filterIssuesCache.value = null;
}

function onSelectBoard(projectId: string, boardId: string) {
  selectedProjectId.value = projectId;
  selectedBoardId.value = boardId;
  selectedFilterId.value = null;
  filterIssuesCache.value = null;
}

function onSelectFilter(filterId: string) {
  selectedFilterId.value = filterId;
  selectedProjectId.value = null;
  selectedBoardId.value = null;
}

watch(selectedBoardId, async (bid) => {
  if (!bid || !selectedProjectId.value) return;
  try {
    await store.loadIssues(selectedProjectId.value);
  } catch (_) {}
});

watch(selectedProjectId, async (pid) => {
  if (!pid) return;
  try {
    await store.loadLabels(pid);
  } catch (_) {}
});

/** Proje seçili, board yokken görev sayısı / özet için issue listesini yükle */
watch(
  [selectedProjectId, selectedBoardId, selectedFilterId],
  async ([pid, bid, fid]) => {
    if (fid || !pid || bid) return;
    try {
      await store.loadIssues(pid);
    } catch (_) {}
  }
);

watch(selectedFilterId, async (fid) => {
  if (fid === TM_FILTER_ASSIGNED_TO_ME) {
    await loadAssignedIssues();
  } else {
    filterIssuesCache.value = null;
  }
});

function mapIssueRaw(raw: Record<string, unknown>): TmIssue {
  const rid = (v: unknown) => {
    if (v == null) return '';
    if (typeof v === 'string') return v;
    if (typeof v === 'object' && v !== null && ('__dataId' in v || 'dataId' in v))
      return String((v as { __dataId?: string }).__dataId ?? (v as { dataId?: string }).dataId ?? '');
    return String(v);
  };
  const lx = raw.labels ?? raw.Labels;
  let labels: string[] | null = null;
  if (Array.isArray(lx)) {
    const ids = lx.map((x) => (typeof x === 'string' ? x : rid(x))).filter(Boolean);
    if (ids.length) labels = ids;
  }
  return {
    __dataId: rid(raw.__dataId ?? raw.DataId ?? raw.dataId),
    key: String(raw.key ?? raw.Key ?? ''),
    projectKey: String(raw.projectKey ?? raw.ProjectKey ?? ''),
    projectId: rid(raw.projectId ?? raw.ProjectId),
    issueTypeId: rid(raw.issueTypeId ?? raw.IssueTypeId),
    title: String(raw.title ?? raw.Title ?? ''),
    description: (raw.description ?? raw.Description) as string | null,
    statusId: rid(raw.statusId ?? raw.StatusId),
    priorityId: raw.priorityId != null ? rid(raw.priorityId) : null,
    assignee: raw.assignee ?? raw.Assignee,
    epicId: raw.epicId != null ? rid(raw.epicId) : null,
    sprintId: raw.sprintId != null ? rid(raw.sprintId) : null,
    labels,
    dueDate: (raw.dueDate ?? raw.DueDate) as string | null,
    storyPoints: raw.storyPoints ?? raw.StoryPoints ?? null,
    order: raw.order ?? raw.Order ?? null,
  };
}

async function loadAssignedIssues() {
  const uid = auth.userInfo?.sub;
  loadingFilterIssues.value = true;
  filterIssuesCache.value = null;
  if (!uid) {
    filterIssuesCache.value = [];
    loadingFilterIssues.value = false;
    return;
  }
  try {
    const merged: TmIssue[] = [];
    for (const p of store.visibleProjects) {
      const raw = await tmListDataset(TM_DATASETS.issues, {
        limit: 2000,
        filter: `projectId:eq:${p.__dataId}`,
        sort: 'order:asc',
      });
      for (const x of raw as Record<string, unknown>[]) {
        const issue = mapIssueRaw(x);
        if (assigneeUserId(issue.assignee) === uid) merged.push(issue);
      }
    }
    merged.sort((a, b) => (a.key || '').localeCompare(b.key || ''));
    filterIssuesCache.value = merged;
  } catch {
    filterIssuesCache.value = [];
  } finally {
    loadingFilterIssues.value = false;
  }
}

function openIssueDialog() {
  issueForm.value = emptyIssueForm();
  issueDialog.value = true;
}

async function submitNewIssue() {
  const pid = selectedProjectId.value;
  const pk = selectedProject.value?.key;
  const st = firstStatusForCreate.value;
  const it = issueForm.value.issueTypeId ?? store.defaultTaskIssueTypeId;
  if (!pid || !pk || !st || !it || !issueForm.value.title.trim()) return;
  creatingIssue.value = true;
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
    issueDialog.value = false;
    if (selectedFilterId.value === TM_FILTER_ASSIGNED_TO_ME) {
      await loadAssignedIssues();
    } else if (pid) {
      await store.loadIssues(pid);
    }
  } finally {
    creatingIssue.value = false;
  }
}

async function bootstrap() {
  treeLoading.value = true;
  store.error = null;
  try {
    await userStore.fetchUsers({ page: 1, pageSize: 500, isActive: true }).catch(() => {});
    await store.loadLookups();
    await store.loadFieldDefinitions().catch(() => {});
    await store.loadProjects();
    const uid = auth.userInfo?.sub;
    if (uid && !auth.isAdmin && !auth.isManager) store.filterProjectsForUser(uid);
    await store.loadAllBoards();
  } catch (e: unknown) {
    const err = e as { message?: string };
    store.error = err?.message ?? 'Yükleme hatası';
  } finally {
    treeLoading.value = false;
  }
}

function queryParamOne(v: string | string[] | undefined | null): string | null {
  if (v == null) return null;
  const s = Array.isArray(v) ? v[0] : v;
  return s ? String(s) : null;
}

/** Ayar sayfasından `?project=&board=` ile dönüşte ağaç seçimini açar */
function applyWorkspaceSelectionFromQuery() {
  const pid = queryParamOne(route.query.project);
  const bid = queryParamOne(route.query.board);
  if (!pid || !store.projects.some((p) => p.__dataId === pid)) return;
  selectedFilterId.value = null;
  filterIssuesCache.value = null;
  selectedProjectId.value = pid;
  if (bid && store.boards.some((b) => b.__dataId === bid && b.projectId === pid)) {
    selectedBoardId.value = bid;
  } else {
    selectedBoardId.value = null;
  }
  router.replace({ path: route.path, query: {} });
}

onMounted(async () => {
  await bootstrap();
  applyWorkspaceSelectionFromQuery();
});

function openProjectDialog() {
  router.push('/apps/task-manager/projects/new');
}

const newBoardFormSelectItems = computed(() => {
  const p = store.projects.find((x) => x.__dataId === selectedProjectId.value);
  const items: { title: string; value: string }[] = [
    { title: mt('taskManager.boardFormUseProjectDefault', 'Proje varsayılanı'), value: TM_BOARD_FORM_PROJECT_DEFAULT },
  ];
  for (const f of p?.issueCreateForms ?? []) {
    items.push({ title: f.name || f.id, value: f.id });
  }
  return items;
});

const newBoardProfileFormSelectItems = computed(() => {
  const p = store.projects.find((x) => x.__dataId === selectedProjectId.value);
  const items: { title: string; value: string }[] = [
    { title: mt('taskManager.boardFormUseProjectDefault', 'Proje varsayılanı'), value: TM_BOARD_FORM_PROJECT_DEFAULT },
  ];
  for (const f of p?.issueProfileForms ?? []) {
    items.push({ title: f.name || f.id, value: f.id });
  }
  return items;
});

function openBoardDialog() {
  if (!selectedProjectId.value) return;
  boardName.value = '';
  newBoardIssueFormId.value = TM_BOARD_FORM_PROJECT_DEFAULT;
  newBoardIssueProfileFormId.value = TM_BOARD_FORM_PROJECT_DEFAULT;
  boardDialog.value = true;
}

async function submitBoard() {
  if (!selectedProjectId.value || !boardName.value.trim()) return;
  savingBoard.value = true;
  try {
    const proj = store.projects.find((p) => p.__dataId === selectedProjectId.value);
    const boardType = projectUsesKanban(proj) ? 'kanban' : 'list';
    const formId =
      newBoardIssueFormId.value === TM_BOARD_FORM_PROJECT_DEFAULT ? null : newBoardIssueFormId.value;
    const profileFormId =
      newBoardIssueProfileFormId.value === TM_BOARD_FORM_PROJECT_DEFAULT ? null : newBoardIssueProfileFormId.value;
    await store.createBoard(selectedProjectId.value, boardName.value, boardType, formId, profileFormId);
    await store.loadAllBoards();
    boardDialog.value = false;
    boardName.value = '';
    newBoardIssueFormId.value = TM_BOARD_FORM_PROJECT_DEFAULT;
    newBoardIssueProfileFormId.value = TM_BOARD_FORM_PROJECT_DEFAULT;
  } finally {
    savingBoard.value = false;
  }
}

const issueEditOpen = ref(false);
const issueBeingEdited = ref<TmIssue | null>(null);

const issueEditProject = computed(() => {
  const i = issueBeingEdited.value;
  if (!i) return null;
  return store.projects.find((p) => p.__dataId === i.projectId) ?? null;
});

watch(issueEditOpen, (v) => {
  if (!v) issueBeingEdited.value = null;
});

function issueProfilePathFromWorkspaceRow(item: Record<string, unknown>): string {
  const k = item?.key;
  let key = typeof k === 'string' && k ? k : '';
  if (!key) {
    const id = String(item.__issueId ?? '');
    key = activeIssues.value.find((x) => x.__dataId === id)?.key ?? '';
  }
  if (!key) return '#';
  const bid = selectedBoardId.value;
  const qs = new URLSearchParams();
  qs.set('from', 'workspace');
  if (bid) qs.set('board', bid);
  return `/apps/task-manager/issues/${encodeURIComponent(key)}/profile?${qs.toString()}`;
}

async function openIssueEditFromWorkspaceRow(item: Record<string, unknown>) {
  const id = String(item.__issueId ?? '');
  const iss = activeIssues.value.find((x) => x.__dataId === id);
  if (!iss) return;
  await store.loadLabels(iss.projectId).catch(() => {});
  issueBeingEdited.value = iss;
  issueEditOpen.value = true;
}

function onRowClick(_e: unknown, row: { item: Record<string, unknown> }) {
  void openIssueEditFromWorkspaceRow(row.item);
}

async function onWorkspaceIssueEditSaved() {
  const cur = issueBeingEdited.value;
  if (!cur) return;
  const id = cur.__dataId;
  if (selectedFilterId.value === TM_FILTER_ASSIGNED_TO_ME) {
    await loadAssignedIssues();
  } else if (cur.projectId) {
    await store.loadIssues(cur.projectId);
  }
  issueBeingEdited.value =
    activeIssues.value.find((x) => x.__dataId === id) ?? store.issues.find((x) => x.__dataId === id) ?? cur;
}

const issueDeleteDialog = ref(false);
const issuePendingDelete = ref<TmIssue | null>(null);
const deletingIssue = ref(false);

function requestDeleteIssueFromWorkspaceRow(item: Record<string, unknown>) {
  const id = String(item.__issueId ?? '');
  const iss = activeIssues.value.find((x) => x.__dataId === id);
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
    if (selectedFilterId.value === TM_FILTER_ASSIGNED_TO_ME) {
      await loadAssignedIssues();
    } else if (i.projectId) {
      await store.loadIssues(i.projectId);
    }
  } finally {
    deletingIssue.value = false;
  }
}
</script>

<template>
  <div class="tm-flow tm-workspace-page">
    <BaseBreadcrumb :title="mt('taskManager.workspaceExplorerTitle', 'Çalışma alanı')" :breadcrumbs="breadcrumbs" />
    <v-alert v-if="store.error" type="error" variant="tonal" class="mb-4" closable @click:close="store.error = null">
      {{ store.error }}
    </v-alert>

    <div class="d-flex workspace-shell" style="min-height: 420px">
      <template v-if="!treeCollapsed">
        <div class="flex-shrink-0 overflow-hidden" :style="{ width: treeWidth + 'px' }">
          <v-card variant="outlined" class="h-100 d-flex flex-column rounded-lg">
            <v-card-title class="d-flex align-center py-3 flex-wrap gap-1">
              <span class="text-subtitle-1">{{ mt('taskManager.workspaceTreePanelNav', 'Gezinme') }}</span>
              <v-spacer />
              <v-btn
                v-if="canCreateProject"
                color="primary"
                size="small"
                variant="tonal"
                class="text-none"
                rounded="lg"
                @click="openProjectDialog"
              >
                {{ mt('taskManager.newProject', 'Yeni proje') }}
              </v-btn>
              <v-btn
                icon
                size="small"
                variant="text"
                :title="mt('taskManager.workspaceCollapseTree', 'Paneli gizle')"
                @click="toggleTreeCollapse"
              >
                <LayoutSidebarLeftCollapseIcon size="18" />
              </v-btn>
              <v-btn
                icon
                size="small"
                variant="text"
                :title="mt('taskManager.workspaceExpandTree', 'Tümünü aç')"
                @click="treeRef?.expandAll()"
              >
                <ChevronDownIcon size="18" />
              </v-btn>
              <v-btn
                icon
                size="small"
                variant="text"
                :title="mt('taskManager.workspaceCollapseAllTree', 'Tümünü kapat')"
                @click="treeRef?.collapseAll()"
              >
                <ChevronUpIcon size="18" />
              </v-btn>
            </v-card-title>
            <v-divider />
            <v-card-text class="pa-0 flex-grow-1 overflow-auto workspace-tree-scroll">
              <div v-if="treeLoading" class="pa-6 d-flex justify-center">
                <v-progress-circular indeterminate color="primary" size="32" />
              </div>
              <TmWorkspaceTree
                v-else
                ref="treeRef"
                :project-nodes="treeNodes"
                :selected-project-id="selectedProjectId"
                :selected-board-id="selectedBoardId"
                :selected-filter-id="selectedFilterId"
                :empty-label="mt('taskManager.noProjects', 'Henüz proje yok.')"
                :label-projects-root="mt('taskManager.workspaceRootProjects', 'Projeler')"
                :label-filters-root="mt('taskManager.workspaceRootFilters', 'Filtreler')"
                :label-assigned-filter="mt('taskManager.myWorkTitle', 'Bana atananlar')"
                @select-project="onSelectProject"
                @select-board="onSelectBoard"
                @select-filter="onSelectFilter"
              />
            </v-card-text>
          </v-card>
        </div>
        <div
          class="tm-tree-resize-handle flex-shrink-0"
          :class="{ 'tm-tree-resize-active': resizeActive }"
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
              :title="mt('taskManager.workspaceShowTree', 'Paneli göster')"
              @click="toggleTreeCollapse"
            >
              <LayoutSidebarLeftExpandIcon size="20" />
            </v-btn>
            <span class="text-subtitle-1 text-truncate">
              <template v-if="selectedFilterId === TM_FILTER_ASSIGNED_TO_ME">
                {{ mt('taskManager.myWorkTitle', 'Bana atananlar') }}
              </template>
              <template v-else-if="selectedBoard && selectedProject">
                {{ selectedProject.name }} · {{ selectedBoard.name }}
              </template>
              <template v-else-if="selectedProject && !selectedBoardId">
                {{ selectedProject.name }}
              </template>
              <template v-else>
                {{ mt('taskManager.workspaceListTitle', 'Görev listesi') }}
              </template>
            </span>
            <v-spacer />
            <v-btn
              v-if="selectedBoardId"
              color="primary"
              size="small"
              variant="flat"
              rounded="lg"
              class="text-none"
              @click="openIssueDialog"
            >
              <v-icon icon="mdi-plus" start size="small" />
              {{ mt('taskManager.newIssue', 'Yeni görev') }}
            </v-btn>
            <v-btn
              v-if="canEdit && selectedProjectId && !selectedBoardId && !selectedFilterId"
              color="primary"
              size="small"
              variant="tonal"
              rounded="lg"
              class="text-none"
              @click="openBoardDialog"
            >
              {{ mt('taskManager.newBoard', 'Yeni board') }}
            </v-btn>
            <v-btn
              v-if="selectedBoardId && canConfigureBoardColumns"
              :to="`/apps/task-manager/boards/${selectedBoardId}/settings`"
              variant="outlined"
              size="small"
              rounded="lg"
              class="text-none"
            >
              <v-icon icon="mdi-cog-outline" start size="small" />
              {{ mt('taskManager.workspaceBoardSettings', 'Ayarlar') }}
            </v-btn>
            <v-btn
              v-if="selectedBoardId && selectedBoardProject && projectUsesKanban(selectedBoardProject)"
              :to="`/apps/task-manager/boards/${selectedBoardId}`"
              variant="tonal"
              size="small"
              rounded="lg"
              class="text-none"
            >
              {{ mt('taskManager.workspaceOpenKanban', 'Kanban') }}
            </v-btn>
          </v-card-title>
          <v-divider />
          <v-card-text class="flex-grow-1 pa-4">
            <template v-if="!selectedBoardId && !selectedFilterId">
              <div v-if="selectedProject && projectOverview" class="tm-ws-project-overview">
                <div class="d-flex flex-column flex-sm-row gap-4 align-start mb-5">
                  <v-avatar size="72" rounded="lg" class="tm-ws-avatar flex-shrink-0" color="primary" variant="tonal">
                    <v-img v-if="projectOverview.avatarUrl" :src="projectOverview.avatarUrl" cover />
                    <span v-else class="text-h5 font-weight-bold text-primary">{{ (projectOverview.key || '?').slice(0, 4) }}</span>
                  </v-avatar>
                  <div class="flex-grow-1 min-width-0">
                    <div class="text-overline text-primary font-weight-bold">{{ projectOverview.key }}</div>
                    <h2 class="text-h5 font-weight-bold mb-2">{{ projectOverview.name }}</h2>
                    <p v-if="projectOverview.description" class="text-body-2 text-medium-emphasis mb-3 text-pre-wrap">
                      {{ projectOverview.description }}
                    </p>
                    <div class="d-flex flex-wrap align-center ga-2 mb-1">
                      <v-chip
                        size="small"
                        variant="tonal"
                        :color="projectOverview.useKanban ? 'primary' : 'secondary'"
                        class="text-none"
                      >
                        <v-icon
                          :icon="projectOverview.useKanban ? 'mdi-view-column' : 'mdi-format-list-bulleted'"
                          start
                          size="16"
                        />
                        {{
                          projectOverview.useKanban
                            ? mt('taskManager.workspaceChipKanban', 'Kanban')
                            : mt('taskManager.workspaceChipList', 'Liste')
                        }}
                      </v-chip>
                      <v-btn
                        v-if="canEdit && selectedProjectId"
                        variant="outlined"
                        size="small"
                        rounded="lg"
                        class="text-none"
                        :to="`/apps/task-manager/projects/${selectedProjectId}/edit`"
                      >
                        <v-icon icon="mdi-pencil-outline" start size="18" />
                        {{ mt('taskManager.editProject', 'Düzenle') }}
                      </v-btn>
                    </div>
                    <div
                      v-if="projectOverview.leadLabel && projectOverview.leadLabel !== '—'"
                      class="d-flex align-center gap-2 mt-3 text-body-2"
                    >
                      <v-icon icon="mdi-account-outline" size="20" class="text-medium-emphasis" />
                      <span class="text-medium-emphasis">{{ mt('taskManager.projectLead', 'Proje lideri') }}:</span>
                      <span class="font-weight-medium">{{ projectOverview.leadLabel }}</span>
                    </div>
                  </div>
                </div>

                <v-row dense class="mb-5">
                  <v-col v-for="(card, idx) in projectStatCards" :key="idx" cols="6" sm="4">
                    <v-sheet rounded="xl" border class="tm-ws-stat pa-4 text-center h-100">
                      <div class="text-h5 font-weight-bold text-primary">{{ card.value }}</div>
                      <div class="text-caption text-medium-emphasis mt-1">{{ card.label }}</div>
                    </v-sheet>
                  </v-col>
                </v-row>

                <p class="text-subtitle-2 font-weight-bold mb-3">
                  {{ mt('taskManager.workspaceOverviewDefinitions', 'Proje tanımları') }}
                </p>

                <v-row dense class="mb-3">
                  <v-col cols="12" md="6">
                    <v-sheet rounded="xl" border class="tm-ws-def pa-3 h-100">
                      <div class="d-flex align-center gap-2 mb-2">
                        <v-icon icon="mdi-state-machine" size="20" class="text-primary" />
                        <span class="text-caption font-weight-bold text-medium-emphasis text-uppercase">{{
                          mt('taskManager.workspaceOverviewStatuses', 'Durumlar (akış)')
                        }}</span>
                      </div>
                      <div v-if="projectOverview.statusNames.length" class="d-flex flex-wrap ga-1">
                        <v-chip
                          v-for="(n, i) in projectOverview.statusNames"
                          :key="'st-' + i"
                          size="small"
                          variant="outlined"
                          color="primary"
                          class="text-none"
                        >
                          {{ n }}
                        </v-chip>
                      </div>
                      <span v-else class="text-body-2 text-medium-emphasis">—</span>
                    </v-sheet>
                  </v-col>
                  <v-col cols="12" md="6">
                    <v-sheet rounded="xl" border class="tm-ws-def pa-3 h-100">
                      <div class="d-flex align-center gap-2 mb-2">
                        <v-icon icon="mdi-priority-high" size="20" class="text-primary" />
                        <span class="text-caption font-weight-bold text-medium-emphasis text-uppercase">{{
                          mt('taskManager.workspaceOverviewPriorities', 'Öncelikler')
                        }}</span>
                      </div>
                      <div v-if="projectOverview.priorityNames.length" class="d-flex flex-wrap ga-1">
                        <v-chip
                          v-for="(n, i) in projectOverview.priorityNames"
                          :key="'pr-' + i"
                          size="small"
                          variant="outlined"
                          color="secondary"
                          class="text-none"
                        >
                          {{ n }}
                        </v-chip>
                      </div>
                      <span v-else class="text-body-2 text-medium-emphasis">—</span>
                    </v-sheet>
                  </v-col>
                </v-row>

                <v-row dense class="mb-3">
                  <v-col cols="12" md="6">
                    <v-sheet rounded="xl" border class="tm-ws-def pa-3 h-100">
                      <div class="d-flex align-center gap-2 mb-2">
                        <v-icon icon="mdi-shape-outline" size="20" class="text-primary" />
                        <span class="text-caption font-weight-bold text-medium-emphasis text-uppercase">{{
                          mt('taskManager.workspaceOverviewIssueTypes', 'Görev tipleri')
                        }}</span>
                      </div>
                      <div v-if="projectOverview.issueTypeNames.length" class="d-flex flex-wrap ga-1">
                        <v-chip
                          v-for="(n, i) in projectOverview.issueTypeNames"
                          :key="'it-' + i"
                          size="small"
                          variant="outlined"
                          class="text-none"
                        >
                          {{ n }}
                        </v-chip>
                      </div>
                      <span v-else class="text-body-2 text-medium-emphasis">—</span>
                    </v-sheet>
                  </v-col>
                  <v-col cols="12" md="6">
                    <v-sheet rounded="xl" border class="tm-ws-def pa-3 h-100">
                      <div class="d-flex align-center gap-2 mb-2">
                        <v-icon icon="mdi-form-select" size="20" class="text-primary" />
                        <span class="text-caption font-weight-bold text-medium-emphasis text-uppercase">{{
                          mt('taskManager.workspaceOverviewFields', 'Alanlar')
                        }}</span>
                      </div>
                      <div v-if="projectOverview.fieldItems.length" class="d-flex flex-wrap ga-1">
                        <v-chip
                          v-for="(f, i) in projectOverview.fieldItems"
                          :key="'fd-' + i"
                          size="small"
                          variant="outlined"
                          class="text-none"
                        >
                          {{ f.label }} ({{ f.key }})
                        </v-chip>
                      </div>
                      <span v-else class="text-body-2 text-medium-emphasis">—</span>
                    </v-sheet>
                  </v-col>
                </v-row>

                <v-sheet rounded="xl" border class="tm-ws-def pa-3 mb-5">
                  <div class="d-flex align-center gap-2 mb-3">
                    <v-icon icon="mdi-shield-account-outline" size="20" class="text-primary" />
                    <span class="text-caption font-weight-bold text-medium-emphasis text-uppercase">{{
                      mt('taskManager.workspaceOverviewPermissions', 'Yetkilendirme')
                    }}</span>
                  </div>
                  <v-list density="compact" class="bg-transparent pa-0">
                    <v-list-item v-for="(row, ri) in projectOverview.permRows" :key="'perm-' + ri" class="px-0 min-h-0">
                      <v-list-item-title class="text-body-2">
                        <span class="text-medium-emphasis">{{ row.title }}:</span>
                        <span class="font-weight-medium ms-1">{{ row.text }}</span>
                      </v-list-item-title>
                    </v-list-item>
                  </v-list>
                </v-sheet>

                <v-alert type="info" variant="tonal" density="comfortable" class="mb-0" rounded="lg">
                  {{ mt('taskManager.workspaceSelectBoardShort', 'Soldan bir board seçerek bu projedeki görevleri listeleyebilirsiniz.') }}
                </v-alert>
              </div>
              <p v-else class="text-body-1 text-medium-emphasis mb-0">
                {{ mt('taskManager.workspaceSelectBoard', 'Sol taraftan bir board veya filtre seçin; görevler burada listelenir.') }}
              </p>
            </template>
            <template v-else-if="selectedFilterId === TM_FILTER_ASSIGNED_TO_ME && !loadingFilterIssues && activeIssues.length === 0">
              <p class="text-body-1 text-medium-emphasis mb-0">
                {{ mt('taskManager.myWorkEmpty', 'Size atanan görev yok.') }}
              </p>
            </template>
            <template v-else>
              <v-text-field
                v-model="listSearch"
                density="comfortable"
                variant="outlined"
                hide-details
                class="mb-4"
                style="max-width: 360px"
                :placeholder="mt('taskManager.searchIssues', 'Görev veya anahtar ara…')"
                prepend-inner-icon="mdi-magnify"
                clearable
              />
              <v-data-table
                item-value="__issueId"
                :headers="tableHeaders"
                :items="tableItems"
                :items-per-page="25"
                :loading="tableLoading"
                class="tm-workspace-table elevation-0"
                hover
                @click:row="onRowClick"
              >
                <template #item.actions="{ item }">
                  <div class="d-inline-flex align-center justify-center">
                    <v-btn
                      icon="mdi-card-account-details-outline"
                      size="small"
                      variant="text"
                      :to="issueProfilePathFromWorkspaceRow(item)"
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
                      @click.stop="openIssueEditFromWorkspaceRow(item)"
                    />
                    <v-btn
                      icon="mdi-delete-outline"
                      size="small"
                      variant="text"
                      color="error"
                      :aria-label="mt('taskManager.deleteIssue', 'Görevi sil')"
                      :title="mt('taskManager.deleteIssue', 'Görevi sil')"
                      @click.stop="requestDeleteIssueFromWorkspaceRow(item)"
                    />
                  </div>
                </template>
              </v-data-table>
            </template>
          </v-card-text>
        </v-card>
      </div>
    </div>

    <v-dialog v-model="boardDialog" max-width="480">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.newBoard', 'Yeni board') }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="boardName" :label="mt('taskManager.boardName', 'Board adı')" density="comfortable" />
          <v-select
            v-model="newBoardIssueFormId"
            class="mt-3"
            :items="newBoardFormSelectItems"
            item-title="title"
            item-value="value"
            density="comfortable"
            variant="outlined"
            :label="mt('taskManager.boardIssueCreateForm', 'Yeni görev formu')"
            :hint="mt('taskManager.boardIssueCreateFormHint', 'İlk seçenek proje varsayılan formunu kullanır.')"
            persistent-hint
          />
          <v-select
            v-model="newBoardIssueProfileFormId"
            class="mt-3"
            :items="newBoardProfileFormSelectItems"
            item-title="title"
            item-value="value"
            density="comfortable"
            variant="outlined"
            :label="mt('taskManager.boardIssueProfileForm', 'Profil ekranı şablonu')"
            :hint="mt('taskManager.boardIssueProfileFormHint', 'İlk seçenek proje varsayılan profil şablonunu kullanır.')"
            persistent-hint
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="boardDialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="primary" :loading="savingBoard" :disabled="!boardName.trim()" @click="submitBoard">
            {{ mt('taskManager.save', 'Kaydet') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <TmIssueEditDialog
      v-model="issueEditOpen"
      :issue="issueBeingEdited"
      :project="issueEditProject"
      :board="selectedBoard"
      @saved="onWorkspaceIssueEditSaved"
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
            :loading="creatingIssue"
            :disabled="!issueForm.title.trim()"
            @click="submitNewIssue"
          >
            {{ mt('taskManager.save', 'Kaydet') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.tm-ws-project-overview .tm-ws-avatar {
  box-shadow: 0 4px 14px rgba(0, 0, 0, 0.08);
}
.tm-ws-stat {
  background: rgb(var(--v-theme-surface));
  transition: box-shadow 0.2s ease;
}
.tm-ws-stat:hover {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}
.tm-ws-def {
  background: rgb(var(--v-theme-surface));
}
.workspace-shell {
  min-height: calc(100vh - 220px);
}
.workspace-tree-scroll {
  max-height: calc(100vh - 280px);
}
.tm-tree-resize-handle {
  width: 6px;
  cursor: col-resize;
  background: transparent;
  transition: background 0.15s;
}
.tm-tree-resize-handle:hover,
.tm-tree-resize-handle.tm-tree-resize-active {
  background: rgb(var(--v-theme-primary));
  opacity: 0.5;
}
.tm-workspace-table :deep(tbody tr) {
  cursor: pointer;
}
</style>
