<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import PmAcksPanel from '@/components/apps/project-management/PmAcksPanel.vue';
import PmAuditPacksPanel from '@/components/apps/project-management/PmAuditPacksPanel.vue';
import PmMeetingsPanel from '@/components/apps/project-management/PmMeetingsPanel.vue';
import PmStakeholdersPanel from '@/components/apps/project-management/PmStakeholdersPanel.vue';
import PmProcessMapsPanel from '@/components/apps/project-management/PmProcessMapsPanel.vue';
import PmBudgetPanel from '@/components/apps/project-management/PmBudgetPanel.vue';
import PmCapacityPanel from '@/components/apps/project-management/PmCapacityPanel.vue';
import PmDecisionsPanel from '@/components/apps/project-management/PmDecisionsPanel.vue';
import PmDiLibrary from '@/components/apps/project-management/PmDiLibrary.vue';
import PmGanttChart from '@/components/apps/project-management/PmGanttChart.vue';
import PmObligationsPanel from '@/components/apps/project-management/PmObligationsPanel.vue';
import PmPackCatalog from '@/components/apps/project-management/PmPackCatalog.vue';
import PmPickDocumentDialog from '@/components/apps/project-management/PmPickDocumentDialog.vue';
import PmPickWorkItemDialog from '@/components/apps/project-management/PmPickWorkItemDialog.vue';
import PmRaidPanel from '@/components/apps/project-management/PmRaidPanel.vue';
import PmStageGatesPanel from '@/components/apps/project-management/PmStageGatesPanel.vue';
import PmStatusPack from '@/components/apps/project-management/PmStatusPack.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePmDate } from '@/composables/usePmDate';
import { useDisplay } from 'vuetify';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import { ocListWorkspaces } from '@/services/operationCoreService';
import {
  pmBindWbsEvidence,
  pmBindWbsReference,
  pmBindWbsWorkItem,
  pmCreateDependency,
  pmCreateWbs,
  pmDateInput,
  pmDatePayload,
  pmDeleteDependency,
  pmDeleteWbs,
  pmGetProject,
  pmGetProjectAcks,
  pmGetProjectAuditPacks,
  pmGetProjectBudget,
  pmGetProjectCapacity,
  pmGetProjectMeetings,
  pmGetProjectObligations,
  pmGetProjectProcessMaps,
  pmGetProjectStakeholders,
  pmGetProjectStatus,
  pmListProjectDecisions,
  pmListProjectRaid,
  pmListWbsEvidence,
  pmListWbsReferences,
  pmSetBaseline,
  pmUnbindWbsEvidence,
  pmUnbindWbsReference,
  pmUnbindWbsWorkItem,
  pmUpdateProject,
  pmUpdateWbs,
} from '@/services/projectManagementService';
import type { OpWorkspace } from '@/types/apps/operationCore';
import type {
  PmDependency,
  PmProjectDetail,
  PmProjectStatus,
  PmWbsItem,
  PmWbsKind,
  PmWorkItemCandidate,
  PmProjectStatusPack,
  PmTraceDocument,
} from '@/types/apps/projectManagement';
import type { DiResource } from '@/types/apps/documentIntelligence';
import {
  ArrowLeftIcon,
  DotsVerticalIcon,
  FlagIcon,
  PlusIcon,
  RefreshIcon,
  TrashIcon,
} from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const { formatPmDateOrDash, formatPmDateRange } = usePmDate();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();
const route = useRoute();
const router = useRouter();

const projectId = computed(() => String(route.params.id || ''));
const loading = ref(false);
const saving = ref(false);
const detail = ref<PmProjectDetail | null>(null);

const projectForm = ref({
  name: '',
  description: '',
  status: 'draft' as PmProjectStatus,
  plannedStart: '',
  plannedFinish: '',
  workspaceId: '',
});

const wbsDialog = ref(false);
const wbsSaving = ref(false);
const wbsEditingId = ref<string | null>(null);
const wbsForm = ref({
  parentId: '',
  kind: 'task' as PmWbsKind,
  name: '',
  plannedStart: '',
  plannedFinish: '',
  actualStart: '',
  actualFinish: '',
  weight: 1,
  percentComplete: 0,
});

const depDialog = ref(false);
const depSaving = ref(false);
const depForm = ref({
  predecessorId: '',
  successorId: '',
  lagDays: 0,
});

const baselineDialog = ref(false);
const baselineSaving = ref(false);
const baselineNote = ref('');

const deleteWbsDialog = ref(false);
const wbsToDelete = ref<PmWbsItem | null>(null);
const deletingWbs = ref(false);
type PmViewTab =
  | 'overview'
  | 'gantt'
  | 'wbs'
  | 'deps'
  | 'decisions'
  | 'raid'
  | 'capacity'
  | 'budget'
  | 'acks'
  | 'obligations'
  | 'audit'
  | 'meetings'
  | 'stakeholders'
  | 'processMaps'
  | 'gates'
  | 'packs'
  | 'library'
  | 'status';

const viewTab = ref<PmViewTab>('gantt');
const { mdAndUp } = useDisplay();
const statusPack = ref<PmProjectStatusPack | null>(null);
const statusLoading = ref(false);
const tabLoaded = ref(new Set<string>());
const tabLoading = ref(false);
const workspaces = ref<OpWorkspace[]>([]);
const bindDialog = ref(false);
const evidenceDialog = ref(false);
const evidenceTarget = ref<PmWbsItem | null>(null);
const evidenceBound = ref<PmTraceDocument[]>([]);
const evidenceLoading = ref(false);
const evidenceSaving = ref(false);
const evidencePickOpen = ref(false);
const referenceDialog = ref(false);
const referenceTarget = ref<PmWbsItem | null>(null);
const referenceBound = ref<PmTraceDocument[]>([]);
const referenceLoading = ref(false);
const referenceSaving = ref(false);
const referencePickOpen = ref(false);
const bindTarget = ref<PmWbsItem | null>(null);
const bindSaving = ref(false);

const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('projectManagement.title'), disabled: false, href: '/apps/project-management' },
  { text: detail.value?.project.name || t('projectManagement.detailTitle'), disabled: true, href: '#' },
]);

const statusItems = computed(() => [
  { title: t('projectManagement.status.draft'), value: 'draft' },
  { title: t('projectManagement.status.active'), value: 'active' },
  { title: t('projectManagement.status.closed'), value: 'closed' },
]);

const navGroups = computed(() => [
  {
    title: t('projectManagement.nav.plan'),
    items: [
      { value: 'overview' as const, title: t('projectManagement.overview'), icon: 'mdi-information-outline' },
      { value: 'gantt' as const, title: t('projectManagement.gantt.title'), icon: 'mdi-chart-gantt' },
      { value: 'wbs' as const, title: t('projectManagement.wbsTitle'), icon: 'mdi-file-tree-outline' },
      { value: 'deps' as const, title: t('projectManagement.depsTitle'), icon: 'mdi-source-fork' },
      { value: 'status' as const, title: t('projectManagement.statusPack.title'), icon: 'mdi-flag-outline' },
    ],
  },
  {
    title: t('projectManagement.nav.delivery'),
    items: [
      { value: 'decisions' as const, title: t('projectManagement.decision.title'), icon: 'mdi-gavel' },
      { value: 'gates' as const, title: t('projectManagement.stageGate.title'), icon: 'mdi-gate' },
      { value: 'packs' as const, title: t('projectManagement.packCatalog.title'), icon: 'mdi-package-variant' },
      { value: 'library' as const, title: t('projectManagement.library.title'), icon: 'mdi-folder-open-outline' },
    ],
  },
  {
    title: t('projectManagement.nav.control'),
    items: [
      { value: 'raid' as const, title: t('projectManagement.raid.title'), icon: 'mdi-alert-octagon-outline' },
      { value: 'capacity' as const, title: t('projectManagement.capacity.title'), icon: 'mdi-account-clock-outline' },
      { value: 'budget' as const, title: t('projectManagement.budget.title'), icon: 'mdi-cash' },
      { value: 'acks' as const, title: t('projectManagement.ack.title'), icon: 'mdi-check-decagram-outline' },
      { value: 'obligations' as const, title: t('projectManagement.obligation.title'), icon: 'mdi-file-document-outline' },
      { value: 'audit' as const, title: t('projectManagement.auditPack.title'), icon: 'mdi-shield-search' },
    ],
  },
  {
    title: t('projectManagement.nav.people'),
    items: [
      { value: 'meetings' as const, title: t('projectManagement.meeting.title'), icon: 'mdi-calendar-account-outline' },
      { value: 'stakeholders' as const, title: t('projectManagement.stakeholder.title'), icon: 'mdi-account-group-outline' },
      { value: 'processMaps' as const, title: t('projectManagement.processMap.title'), icon: 'mdi-sitemap-outline' },
    ],
  },
]);

const headerStatusLabel = computed(() => {
  const status = detail.value?.project.status;
  return statusItems.value.find((item) => item.value === status)?.title || status || '';
});

const headerDates = computed(() => {
  return formatPmDateRange(detail.value?.project.plannedStart, detail.value?.project.plannedFinish);
});

const kindItems = computed(() => [
  { title: t('projectManagement.kind.summary'), value: 'summary' },
  { title: t('projectManagement.kind.task'), value: 'task' },
  { title: t('projectManagement.kind.milestone'), value: 'milestone' },
]);

const wbsKindChoices = computed(() => [
  {
    value: 'summary' as const,
    title: t('projectManagement.kind.summary'),
    hint: t('projectManagement.kindHint.summary'),
    icon: 'mdi-folder-outline',
  },
  {
    value: 'task' as const,
    title: t('projectManagement.kind.task'),
    hint: t('projectManagement.kindHint.task'),
    icon: 'mdi-checkbox-marked-outline',
  },
  {
    value: 'milestone' as const,
    title: t('projectManagement.kind.milestone'),
    hint: t('projectManagement.kindHint.milestone'),
    icon: 'mdi-flag-checkered',
  },
]);

const wbsDialogSubtitle = computed(() =>
  wbsEditingId.value ? t('projectManagement.wbsDialog.editHint') : t('projectManagement.wbsDialog.createHint'),
);

const wbs = computed(() => detail.value?.wbs ?? []);
const dependencies = computed(() => detail.value?.dependencies ?? []);
const decisions = computed(() => detail.value?.decisions ?? []);
const stageGates = computed(() => detail.value?.stageGates ?? []);
const raidItems = computed(() => detail.value?.raidItems ?? []);
const assignments = computed(() => detail.value?.assignments ?? []);
const capacity = computed(() => detail.value?.capacity ?? null);
const budgetLines = computed(() => detail.value?.budgetLines ?? []);
const budget = computed(() => detail.value?.budget ?? null);
const acknowledgements = computed(() => detail.value?.acknowledgements ?? []);
const obligations = computed(() => detail.value?.obligations ?? []);
const auditPacks = computed(() => detail.value?.auditPacks ?? []);
const meetings = computed(() => detail.value?.meetings ?? []);
const stakeholders = computed(() => detail.value?.stakeholders ?? []);
const processMaps = computed(() => detail.value?.processMaps ?? []);

const parentItems = computed(() => {
  const current = wbsEditingId.value;
  return [
    { title: t('projectManagement.wbsRoot'), value: '' },
    ...wbs.value
      .filter((row) => row.id !== current)
      .map((row) => ({
        title: `${row.wbsCode || '—'} ${row.name}`,
        value: row.id,
      })),
  ];
});

const wbsSelectItems = computed(() =>
  wbs.value.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
);

const workspaceItems = computed(() => [
  { title: t('projectManagement.workspaceNone'), value: '' },
  ...workspaces.value.map((row) => ({
    title: row.name,
    value: row.__dataId,
  })),
]);

const editingWbs = computed(() => wbs.value.find((row) => row.id === wbsEditingId.value) ?? null);
const percentLocked = computed(() => {
  const row = editingWbs.value
  if (!row) return false
  if (hasChildren(row.id)) return true
  return Boolean(row.workItemId)
})

const percentHint = computed(() => {
  const row = editingWbs.value
  if (!row) return undefined
  if (hasChildren(row.id)) return t('projectManagement.percentFromChildren')
  if (row.workItemId) return t('projectManagement.percentFromWorkItem')
  return undefined
})

function hasChildren(id: string) {
  return wbs.value.some((row) => row.parentId === id);
}

function workItemHref(id: string) {
  return `/apps/operation-core/work-items/${encodeURIComponent(id)}/profile`;
}

function resourceHref(id: string) {
  return `/apps/document-intelligence/r/${encodeURIComponent(id)}`;
}

function stateChipColor(item: { workItemClosed?: boolean; workItemStateCategory?: string | null }) {
  if (item.workItemClosed) return 'success';
  if (item.workItemStateCategory === 'in_progress') return 'info';
  return 'default';
}

const wbsSearch = ref('');
const wbsKindFilter = ref<'all' | PmWbsKind>('all');
const wbsExtraFilter = ref<'all' | 'bound' | 'unbound' | 'missingEvidence' | 'drifted'>('all');
const wbsPage = ref(1);
const wbsItemsPerPage = ref(25);
const wbsItemsPerPageOptions = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
  { value: 100, title: '100' },
  { value: -1, title: '$vuetify.dataFooter.itemsPerPageAll' },
];

const wbsKindFilterItems = computed(() => [
  { title: t('projectManagement.wbsFilter.all'), value: 'all' },
  ...kindItems.value,
]);

const wbsExtraFilterItems = computed(() => [
  { title: t('projectManagement.wbsFilter.all'), value: 'all' },
  { title: t('projectManagement.wbsFilter.bound'), value: 'bound' },
  { title: t('projectManagement.wbsFilter.unbound'), value: 'unbound' },
  { title: t('projectManagement.wbsFilter.missingEvidence'), value: 'missingEvidence' },
  { title: t('projectManagement.wbsFilter.drifted'), value: 'drifted' },
]);

const filteredWbs = computed(() => {
  const query = wbsSearch.value.trim().toLowerCase();
  return wbs.value.filter((item) => {
    if (wbsKindFilter.value !== 'all' && item.kind !== wbsKindFilter.value) return false;
    switch (wbsExtraFilter.value) {
      case 'bound':
        if (!item.workItemId) return false;
        break;
      case 'unbound':
        if (item.workItemId) return false;
        break;
      case 'missingEvidence':
        if (!(item.workItemId && !item.hasEvidence)) return false;
        break;
      case 'drifted':
        if (!item.baselineDrifted) return false;
        break;
      default:
        break;
    }
    if (!query) return true;
    const haystack = [item.wbsCode, item.name, item.workItemKey, item.workItemTitle, kindLabel(item.kind)]
      .filter(Boolean)
      .join(' ')
      .toLowerCase();
    return haystack.includes(query);
  });
});

watch([wbsSearch, wbsKindFilter, wbsExtraFilter], () => {
  wbsPage.value = 1;
});

const depSearch = ref('');
const depPage = ref(1);
const depItemsPerPage = ref(25);

const filteredDeps = computed(() => {
  const query = depSearch.value.trim().toLowerCase();
  if (!query) return dependencies.value;
  return dependencies.value.filter((item) => {
    const haystack = [
      wbsName(item.predecessorId),
      wbsName(item.successorId),
      item.type,
      String(item.lagDays ?? 0),
    ]
      .join(' ')
      .toLowerCase();
    return haystack.includes(query);
  });
});

watch(depSearch, () => {
  depPage.value = 1;
});

function compareWbsCode(a: unknown, b: unknown) {
  const codeOf = (value: unknown) => {
    if (value && typeof value === 'object' && 'wbsCode' in value) {
      return String((value as { wbsCode?: string | null }).wbsCode || '');
    }
    return String(value ?? '');
  };
  const partsA = codeOf(a).split('.').map(Number);
  const partsB = codeOf(b).split('.').map(Number);
  const length = Math.max(partsA.length, partsB.length);
  for (let index = 0; index < length; index += 1) {
    const left = Number.isFinite(partsA[index]) ? partsA[index] : -1;
    const right = Number.isFinite(partsB[index]) ? partsB[index] : -1;
    if (left !== right) return left - right;
  }
  return 0;
}

const wbsHeaders = computed(() => [
  { title: t('projectManagement.fields.wbsCode'), key: 'wbsCode', width: 110 },
  { title: t('projectManagement.fields.name'), key: 'name', minWidth: 220 },
  { title: t('projectManagement.fields.kind'), key: 'kind', width: 130 },
  { title: t('projectManagement.fields.workItem'), key: 'workItemKey', minWidth: 140 },
  { title: t('projectManagement.fields.weight'), key: 'weight', width: 80 },
  { title: t('projectManagement.fields.plannedStart'), key: 'plannedStart', width: 130 },
  { title: t('projectManagement.fields.plannedFinish'), key: 'plannedFinish', width: 130 },
  { title: t('projectManagement.fields.percentComplete'), key: 'percentComplete', width: 90 },
  {
    title: t('projectManagement.fields.baseline'),
    key: 'baseline',
    width: 110,
    value: (item: PmWbsItem) => (item.baselineDrifted ? 2 : item.baselineStart || item.baselineFinish ? 1 : 0),
  },
  {
    title: t('projectManagement.actions'),
    key: 'actions',
    width: 156,
    sortable: false,
    nowrap: true,
  },
]);

const depHeaders = computed(() => [
  { title: t('projectManagement.fields.predecessor'), key: 'predecessorId', minWidth: 220 },
  { title: t('projectManagement.fields.successor'), key: 'successorId', minWidth: 220 },
  { title: t('projectManagement.fields.type'), key: 'type', width: 160 },
  { title: t('projectManagement.fields.lagDays'), key: 'lagDays', width: 120 },
  {
    title: t('projectManagement.actions'),
    key: 'actions',
    width: 88,
    sortable: false,
    nowrap: true,
    align: 'end' as const,
  },
]);

function wbsName(id: string) {
  const row = wbs.value.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function lagLabel(days?: number | null) {
  const lag = days ?? 0;
  if (lag === 0) return t('projectManagement.depsLagZero');
  return t('projectManagement.depsLagDays', { n: lag });
}

function depDateClash(dep: PmDependency) {
  const predecessor = wbs.value.find((item) => item.id === dep.predecessorId);
  const successor = wbs.value.find((item) => item.id === dep.successorId);
  const finish = pmDateInput(predecessor?.plannedFinish);
  const start = pmDateInput(successor?.plannedStart);
  if (!finish || !start) return false;
  const finishDay = Date.parse(`${finish}T00:00:00Z`);
  const startDay = Date.parse(`${start}T00:00:00Z`);
  if (Number.isNaN(finishDay) || Number.isNaN(startDay)) return false;
  return startDay < finishDay + (dep.lagDays ?? 0) * 86_400_000;
}

function kindLabel(kind?: string | null) {
  const key = `projectManagement.kind.${kind || 'task'}`;
  const label = t(key);
  return label === key ? (kind || 'task') : label;
}

function depthOf(code?: string | null) {
  if (!code) return 0;
  return Math.max(0, code.split('.').length - 1);
}

function applyProjectForm(project: PmProjectDetail['project']) {
  projectForm.value = {
    name: project.name || '',
    description: project.description || '',
    status: (project.status as PmProjectStatus) || 'draft',
    plannedStart: pmDateInput(project.plannedStart),
    plannedFinish: pmDateInput(project.plannedFinish),
    workspaceId: project.workspaceId || '',
  };
}

function mergeDetail(patch: Partial<PmProjectDetail>) {
  if (!detail.value) return;
  detail.value = { ...detail.value, ...patch };
}

function markTabLoaded(tab: string) {
  const next = new Set(tabLoaded.value);
  next.add(tab);
  tabLoaded.value = next;
}

function resetTabCache() {
  tabLoaded.value = new Set(['overview', 'gantt', 'wbs', 'deps', 'gates']);
  statusPack.value = null;
}

async function loadDetail() {
  if (!projectId.value) return;
  loading.value = true;
  try {
    const next = await pmGetProject(projectId.value);
    detail.value = next;
    applyProjectForm(next.project);
    resetTabCache();
    await ensureTabData(viewTab.value, true);
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    loading.value = false;
  }
}

async function loadStatusPack() {
  if (!projectId.value) return;
  statusLoading.value = true;
  try {
    statusPack.value = await pmGetProjectStatus(projectId.value);
  } catch (error) {
    statusPack.value = null;
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    statusLoading.value = false;
  }
}

async function ensureTabData(tab: string, force = false) {
  if (!projectId.value || !detail.value) return;
  if (!force && tabLoaded.value.has(tab)) return;

  const coreTabs = new Set(['overview', 'gantt', 'wbs', 'deps', 'gates', 'packs', 'library']);
  if (coreTabs.has(tab)) {
    markTabLoaded(tab);
    return;
  }

  tabLoading.value = true;
  try {
    switch (tab) {
      case 'status':
        await loadStatusPack();
        break;
      case 'decisions':
        mergeDetail({ decisions: await pmListProjectDecisions(projectId.value) });
        break;
      case 'raid':
        mergeDetail({ raidItems: await pmListProjectRaid(projectId.value) });
        break;
      case 'capacity': {
        const pack = await pmGetProjectCapacity(projectId.value);
        mergeDetail({ capacity: pack, assignments: pack.assignments ?? [] });
        break;
      }
      case 'budget': {
        const pack = await pmGetProjectBudget(projectId.value);
        mergeDetail({ budget: pack, budgetLines: pack.lines ?? [] });
        break;
      }
      case 'acks':
        mergeDetail({ acknowledgements: (await pmGetProjectAcks(projectId.value)).items ?? [] });
        break;
      case 'obligations':
        mergeDetail({ obligations: (await pmGetProjectObligations(projectId.value)).items ?? [] });
        break;
      case 'audit':
        mergeDetail({ auditPacks: (await pmGetProjectAuditPacks(projectId.value)).items ?? [] });
        break;
      case 'meetings':
        mergeDetail({ meetings: (await pmGetProjectMeetings(projectId.value)).items ?? [] });
        break;
      case 'stakeholders':
        mergeDetail({ stakeholders: (await pmGetProjectStakeholders(projectId.value)).items ?? [] });
        break;
      case 'processMaps':
        mergeDetail({ processMaps: (await pmGetProjectProcessMaps(projectId.value)).items ?? [] });
        break;
      default:
        break;
    }
    markTabLoaded(tab);
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    tabLoading.value = false;
  }
}

function onLibraryHubReady(id: string) {
  if (!detail.value?.project || detail.value.project.diFolderId === id) return;
  detail.value.project = { ...detail.value.project, diFolderId: id };
}

async function onLibraryBound() {
  if (!projectId.value) return;
  try {
    const next = await pmGetProject(projectId.value);
    mergeDetail({ wbs: next.wbs });
    const loaded = new Set(tabLoaded.value);
    loaded.delete('status');
    loaded.delete('decisions');
    tabLoaded.value = loaded;
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  }
}

function onPanelChanged() {
  void ensureTabData(viewTab.value, true);
}

async function saveProject() {
  if (!detail.value) return;
  saving.value = true;
  try {
    const updated = await pmUpdateProject(detail.value.project.id, {
      name: projectForm.value.name.trim(),
      description: projectForm.value.description.trim(),
      status: projectForm.value.status,
      plannedStart: pmDatePayload(projectForm.value.plannedStart),
      plannedFinish: pmDatePayload(projectForm.value.plannedFinish),
      workspaceId: projectForm.value.workspaceId || null,
    });
    if (detail.value) detail.value.project = updated;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.updated'),
      severity: 'success',
    });
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

function openCreateWbs(parentId?: string | null) {
  wbsEditingId.value = null;
  wbsForm.value = {
    parentId: parentId || '',
    kind: 'task',
    name: '',
    plannedStart: '',
    plannedFinish: '',
    actualStart: '',
    actualFinish: '',
    weight: 1,
    percentComplete: 0,
  };
  wbsDialog.value = true;
}

function openEditWbs(row: PmWbsItem) {
  wbsEditingId.value = row.id;
  wbsForm.value = {
    parentId: row.parentId || '',
    kind: (row.kind as PmWbsKind) || 'task',
    name: row.name,
    plannedStart: pmDateInput(row.plannedStart),
    plannedFinish: pmDateInput(row.plannedFinish),
    actualStart: pmDateInput(row.actualStart),
    actualFinish: pmDateInput(row.actualFinish),
    weight: row.weight ?? 1,
    percentComplete: row.percentComplete ?? 0,
  };
  wbsDialog.value = true;
}

async function saveWbs() {
  if (!detail.value) return;
  wbsSaving.value = true;
  try {
    if (wbsEditingId.value) {
      await pmUpdateWbs(wbsEditingId.value, {
        parentId: wbsForm.value.parentId,
        kind: wbsForm.value.kind,
        name: wbsForm.value.name.trim(),
        plannedStart: pmDatePayload(wbsForm.value.plannedStart),
        plannedFinish: pmDatePayload(wbsForm.value.plannedFinish),
        actualStart: pmDatePayload(wbsForm.value.actualStart),
        actualFinish: pmDatePayload(wbsForm.value.actualFinish),
        weight: wbsForm.value.weight,
        percentComplete: percentLocked.value ? undefined : wbsForm.value.percentComplete,
      });
    } else {
      await pmCreateWbs(detail.value.project.id, {
        parentId: wbsForm.value.parentId || null,
        kind: wbsForm.value.kind,
        name: wbsForm.value.name.trim(),
        plannedStart: pmDatePayload(wbsForm.value.plannedStart),
        plannedFinish: pmDatePayload(wbsForm.value.plannedFinish),
        weight: wbsForm.value.weight,
        percentComplete: wbsForm.value.percentComplete,
      });
    }
    wbsDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.wbsSaved'),
      severity: 'success',
    });
    await loadDetail();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    wbsSaving.value = false;
  }
}

function confirmDeleteWbs(row: PmWbsItem) {
  wbsToDelete.value = row;
  deleteWbsDialog.value = true;
}

async function executeDeleteWbs() {
  if (!wbsToDelete.value) return;
  deletingWbs.value = true;
  try {
    await pmDeleteWbs(wbsToDelete.value.id);
    deleteWbsDialog.value = false;
    wbsToDelete.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.wbsDeleted'),
      severity: 'success',
    });
    await loadDetail();
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  } finally {
    deletingWbs.value = false;
  }
}

async function saveDependency() {
  if (!detail.value) return;
  depSaving.value = true;
  try {
    await pmCreateDependency(detail.value.project.id, {
      predecessorId: depForm.value.predecessorId,
      successorId: depForm.value.successorId,
      type: 'FS',
      lagDays: depForm.value.lagDays || 0,
    });
    depDialog.value = false;
    depForm.value = { predecessorId: '', successorId: '', lagDays: 0 };
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.depCreated'),
      severity: 'success',
    });
    await loadDetail();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    depSaving.value = false;
  }
}

async function removeDependency(row: PmDependency) {
  try {
    await pmDeleteDependency(row.id);
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.depDeleted'),
      severity: 'success',
    });
    await loadDetail();
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  }
}

async function applyBaseline() {
  if (!detail.value) return;
  baselineSaving.value = true;
  try {
    detail.value = await pmSetBaseline(detail.value.project.id, baselineNote.value.trim() || null);
    applyProjectForm(detail.value.project);
    resetTabCache();
    await ensureTabData(viewTab.value, true);
    baselineDialog.value = false;
    baselineNote.value = '';
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.baselineSet'),
      severity: 'success',
    });
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    baselineSaving.value = false;
  }
}

async function loadWorkspaces() {
  try {
    workspaces.value = await ocListWorkspaces();
  } catch {
    workspaces.value = [];
  }
}

function openBindWorkItem(row: PmWbsItem) {
  bindTarget.value = row;
  bindDialog.value = true;
}

async function bindSelected(candidate: PmWorkItemCandidate) {
  if (!bindTarget.value) return;
  bindSaving.value = true;
  try {
    await pmBindWbsWorkItem(bindTarget.value.id, candidate.id);
    bindDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.workItemBound'),
      severity: 'success',
    });
    await loadDetail();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    bindSaving.value = false;
  }
}

async function unbindWorkItem(row: PmWbsItem) {
  try {
    await pmUnbindWbsWorkItem(row.id);
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.workItemUnbound'),
      severity: 'success',
    });
    await loadDetail();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  }
}

async function loadEvidenceCandidates() {
  if (!evidenceTarget.value) return;
  evidenceLoading.value = true;
  try {
    evidenceBound.value = await pmListWbsEvidence(evidenceTarget.value.id);
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    evidenceLoading.value = false;
  }
}

function openBindEvidence(row: PmWbsItem) {
  evidenceTarget.value = row;
  evidenceBound.value = [];
  evidenceDialog.value = true;
  void loadEvidenceCandidates();
}

async function bindEvidenceSelected(resourceId: string) {
  if (!evidenceTarget.value || !resourceId.trim()) return;
  evidenceSaving.value = true;
  try {
    await pmBindWbsEvidence(evidenceTarget.value.id, resourceId.trim());
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.evidenceBound'),
      severity: 'success',
    });
    await loadDetail();
    const current = wbs.value.find((item) => item.id === evidenceTarget.value?.id);
    if (current) evidenceTarget.value = current;
    await loadEvidenceCandidates();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    evidenceSaving.value = false;
  }
}

async function unbindEvidence(resourceId: string) {
  if (!evidenceTarget.value) return;
  evidenceSaving.value = true;
  try {
    await pmUnbindWbsEvidence(evidenceTarget.value.id, resourceId);
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.evidenceUnbound'),
      severity: 'success',
    });
    await loadDetail();
    const current = wbs.value.find((item) => item.id === evidenceTarget.value?.id);
    if (current) evidenceTarget.value = current;
    await loadEvidenceCandidates();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    evidenceSaving.value = false;
  }
}

async function loadReferenceCandidates() {
  if (!referenceTarget.value) return;
  referenceLoading.value = true;
  try {
    referenceBound.value = await pmListWbsReferences(referenceTarget.value.id);
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    referenceLoading.value = false;
  }
}

function openBindReference(row: PmWbsItem) {
  referenceTarget.value = row;
  referenceBound.value = [];
  referenceDialog.value = true;
  void loadReferenceCandidates();
}

async function bindReferenceSelected(resourceId: string) {
  if (!referenceTarget.value || !resourceId.trim()) return;
  referenceSaving.value = true;
  try {
    await pmBindWbsReference(referenceTarget.value.id, resourceId.trim());
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.referenceBound'),
      severity: 'success',
    });
    await loadDetail();
    const current = wbs.value.find((item) => item.id === referenceTarget.value?.id);
    if (current) referenceTarget.value = current;
    await loadReferenceCandidates();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    referenceSaving.value = false;
  }
}

async function unbindReference(resourceId: string) {
  if (!referenceTarget.value) return;
  referenceSaving.value = true;
  try {
    await pmUnbindWbsReference(referenceTarget.value.id, resourceId);
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.referenceUnbound'),
      severity: 'success',
    });
    await loadDetail();
    const current = wbs.value.find((item) => item.id === referenceTarget.value?.id);
    if (current) referenceTarget.value = current;
    await loadReferenceCandidates();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    referenceSaving.value = false;
  }
}

function onEvidencePicked(resource: DiResource) {
  void bindEvidenceSelected(resource.id);
}

function onReferencePicked(resource: DiResource) {
  void bindReferenceSelected(resource.id);
}

onMounted(() => {
  void loadWorkspaces();
  void loadDetail();
});

watch(viewTab, (tab) => {
  void ensureTabData(tab);
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="detail?.project.name || t('projectManagement.detailTitle')" :breadcrumbs="breadcrumbs" />

    <v-card elevation="0" class="withbg mb-4">
      <v-card-title class="d-flex align-center justify-space-between flex-wrap ga-2 px-6 py-4">
        <div class="d-flex align-center ga-2 flex-wrap">
          <v-btn icon variant="text" @click="router.push('/apps/project-management')">
            <ArrowLeftIcon size="20" />
          </v-btn>
          <FlagIcon size="22" />
          <div>
            <div class="text-h6">
              {{
                loading && !detail
                  ? t('projectManagement.loadingDetail')
                  : `${detail?.project.code || ''} — ${detail?.project.name || t('projectManagement.detailTitle')}`
              }}
            </div>
            <div v-if="detail" class="text-caption text-medium-emphasis">
              <span v-if="headerStatusLabel">{{ headerStatusLabel }}</span>
              <span v-if="headerStatusLabel && headerDates"> · </span>
              <span v-if="headerDates">{{ headerDates }}</span>
            </div>
          </div>
          <v-chip v-if="detail?.project.baselineDrifted" size="small" color="warning" variant="tonal">
            {{ t('projectManagement.drift') }}
          </v-chip>
        </div>
        <v-btn variant="tonal" :loading="loading" @click="loadDetail">
          <RefreshIcon size="18" class="mr-1" />
          {{ t('projectManagement.refresh') }}
        </v-btn>
      </v-card-title>
      <v-divider />
      <div class="d-flex flex-column flex-md-row-reverse">
        <v-tabs
          v-model="viewTab"
          :direction="mdAndUp ? 'vertical' : 'horizontal'"
          color="primary"
          class="pm-side-tabs flex-shrink-0 py-2"
          show-arrows
        >
          <template v-for="group in navGroups" :key="group.title">
            <div v-if="mdAndUp" class="pm-side-tabs__group">{{ group.title }}</div>
            <v-tab
              v-for="item in group.items"
              :key="item.value"
              :value="item.value"
              class="text-none justify-start"
            >
              <v-icon :icon="item.icon" start size="20" />
              {{ item.title }}
            </v-tab>
          </template>
        </v-tabs>
        <v-divider :vertical="mdAndUp" />
        <div class="pm-project-main">
      <v-card-text v-if="viewTab === 'overview'" class="px-6 py-4">
        <v-skeleton-loader v-if="loading && !detail" type="article, list-item-two-line@4" />
        <div v-else class="d-flex flex-column ga-3" style="max-width: 720px">
          <v-text-field v-model="projectForm.name" :label="t('projectManagement.fields.name')" density="comfortable" />
          <v-textarea
            v-model="projectForm.description"
            :label="t('projectManagement.fields.description')"
            density="comfortable"
            rows="2"
            auto-grow
          />
          <v-select
            v-model="projectForm.status"
            :items="statusItems"
            :label="t('projectManagement.fields.status')"
            density="comfortable"
          />
          <v-select
            v-model="projectForm.workspaceId"
            :items="workspaceItems"
            :label="t('projectManagement.fields.workspace')"
            density="comfortable"
            :hint="t('projectManagement.workspaceHint')"
            persistent-hint
          />
          <div class="d-flex ga-3">
            <v-text-field
              v-model="projectForm.plannedStart"
              type="date"
              :label="t('projectManagement.fields.plannedStart')"
              density="comfortable"
            />
            <v-text-field
              v-model="projectForm.plannedFinish"
              type="date"
              :label="t('projectManagement.fields.plannedFinish')"
              density="comfortable"
            />
          </div>
          <div class="text-medium-emphasis text-body-2">
            <span v-if="detail?.project.baselineSetAt">
              {{ t('projectManagement.baselineAt') }}: {{ formatPmDateOrDash(detail.project.baselineSetAt) }}
              <span v-if="detail.project.baselineSetBy"> ({{ detail.project.baselineSetBy }})</span>
            </span>
            <span v-else>{{ t('projectManagement.noBaseline') }}</span>
          </div>
          <div class="d-flex ga-2 flex-wrap">
            <v-btn color="primary" :loading="saving" :disabled="!detail || !projectForm.name.trim()" @click="saveProject">
              {{ t('projectManagement.save') }}
            </v-btn>
            <v-btn color="primary" variant="tonal" :disabled="!detail" @click="baselineDialog = true">
              {{ t('projectManagement.setBaseline') }}
            </v-btn>
          </div>
        </div>
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'gantt'" class="px-6 py-4">
        <div class="d-flex justify-end mb-3">
          <v-btn color="primary" @click="openCreateWbs()">
            <PlusIcon size="18" class="mr-1" />
            {{ t('projectManagement.newWbs') }}
          </v-btn>
        </div>
        <PmGanttChart :items="wbs" :dependencies="dependencies" @edit="openEditWbs" />
      </v-card-text>

      <template v-else-if="viewTab === 'wbs'">
      <v-card-title class="d-flex flex-column align-stretch px-6 py-4 pb-2">
        <div>{{ t('projectManagement.wbsTitle') }}</div>
        <div class="text-body-2 text-medium-emphasis font-weight-regular mt-1 text-wrap">
          {{ t('projectManagement.wbsHint') }}
        </div>
      </v-card-title>
      <v-card-text class="px-6 pt-0">
        <div class="d-flex flex-wrap ga-3 mb-4 align-center">
          <v-text-field
            v-model="wbsSearch"
            :label="t('projectManagement.wbsSearch')"
            density="comfortable"
            hide-details
            clearable
            style="max-width: 280px"
          />
          <v-select
            v-model="wbsKindFilter"
            :items="wbsKindFilterItems"
            :label="t('projectManagement.fields.kind')"
            density="comfortable"
            hide-details
            style="max-width: 180px"
          />
          <v-select
            v-model="wbsExtraFilter"
            :items="wbsExtraFilterItems"
            :label="t('projectManagement.wbsFilter.label')"
            density="comfortable"
            hide-details
            style="max-width: 200px"
          />
          <v-spacer />
          <v-btn color="primary" @click="openCreateWbs()">
            <PlusIcon size="18" class="mr-1" />
            {{ t('projectManagement.newWbs') }}
          </v-btn>
        </div>
        <div class="pm-wbs-table-wrap">
        <v-data-table
          v-model:page="wbsPage"
          v-model:items-per-page="wbsItemsPerPage"
          :headers="wbsHeaders"
          :items="filteredWbs"
          :loading="loading"
          item-value="id"
          density="comfortable"
          class="rounded-lg border pm-wbs-table"
          :items-per-page-options="wbsItemsPerPageOptions"
          :custom-key-sort="{ wbsCode: compareWbsCode }"
        >
          <template #item.actions="{ item }">
            <div class="d-flex align-center ga-1 flex-nowrap">
              <v-btn size="small" variant="tonal" color="primary" @click="openEditWbs(item)">
                {{ t('projectManagement.update') }}
              </v-btn>
              <v-menu>
                <template #activator="{ props }">
                  <v-btn icon size="small" variant="text" v-bind="props">
                    <DotsVerticalIcon size="18" />
                  </v-btn>
                </template>
                <v-list density="compact">
                  <v-list-item :title="t('projectManagement.addChild')" @click="openCreateWbs(item.id)" />
                  <v-list-item
                    v-if="item.workItemId"
                    :title="t('projectManagement.unbindWorkItem')"
                    @click="unbindWorkItem(item)"
                  />
                  <v-list-item
                    v-else
                    :title="t('projectManagement.bindWorkItem')"
                    :disabled="!detail?.project.workspaceId"
                    @click="openBindWorkItem(item)"
                  />
                  <v-list-item
                    v-if="item.workItemId"
                    :title="t('projectManagement.bindReference')"
                    @click="openBindReference(item)"
                  />
                  <v-list-item
                    v-if="item.workItemId"
                    :title="t('projectManagement.bindEvidence')"
                    @click="openBindEvidence(item)"
                  />
                  <v-list-item
                    :title="t('projectManagement.delete')"
                    class="text-error"
                    @click="confirmDeleteWbs(item)"
                  />
                </v-list>
              </v-menu>
            </div>
          </template>
          <template #item.name="{ item }">
            <span :style="{ paddingLeft: `${depthOf(item.wbsCode) * 16}px` }">{{ item.name }}</span>
            <v-chip
              v-if="item.gateLocked"
              size="x-small"
              color="warning"
              variant="tonal"
              class="ml-2"
            >
              {{ t('projectManagement.stageGate.locked') }}
            </v-chip>
            <v-chip
              v-if="item.hasEvidence"
              size="x-small"
              color="success"
              variant="tonal"
              class="ml-2"
            >
              {{ t('projectManagement.hasEvidence') }}
            </v-chip>
            <v-chip
              v-else-if="item.workItemId"
              size="x-small"
              color="warning"
              variant="tonal"
              class="ml-2"
            >
              {{ t('projectManagement.missingEvidenceChip') }}
            </v-chip>
            <v-chip
              v-if="item.hasReference"
              size="x-small"
              color="primary"
              variant="tonal"
              class="ml-2"
            >
              {{ t('projectManagement.hasReference') }}
            </v-chip>
            <v-chip
              v-else-if="item.workItemId"
              size="x-small"
              color="warning"
              variant="tonal"
              class="ml-2"
            >
              {{ t('projectManagement.missingReferenceChip') }}
            </v-chip>
          </template>
          <template #item.kind="{ item }">
            {{ kindLabel(item.kind) }}
          </template>
          <template #item.workItemKey="{ item }">
            <NuxtLink
              v-if="item.workItemId && item.workItemKey"
              :to="workItemHref(item.workItemId)"
              class="text-decoration-none"
              @click.stop
            >
              <v-chip size="small" :color="stateChipColor(item)" variant="tonal">
                {{ item.workItemKey }}
              </v-chip>
            </NuxtLink>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.weight="{ item }">
            {{ item.weight ?? 1 }}
          </template>
          <template #item.plannedStart="{ item }">
            {{ formatPmDateOrDash(item.plannedStart) }}
          </template>
          <template #item.plannedFinish="{ item }">
            {{ formatPmDateOrDash(item.plannedFinish) }}
          </template>
          <template #item.percentComplete="{ item }">
            {{ item.percentComplete ?? 0 }}%
          </template>
          <template #item.baseline="{ item }">
            <v-chip v-if="item.baselineDrifted" size="small" color="warning" variant="tonal">
              {{ t('projectManagement.drift') }}
            </v-chip>
            <span v-else-if="item.baselineStart || item.baselineFinish">{{ t('projectManagement.onBaseline') }}</span>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #no-data>
            <div class="text-center py-8 text-medium-emphasis">
              {{ wbs.length ? t('projectManagement.wbsFilter.empty') : t('projectManagement.emptyWbs') }}
            </div>
          </template>
        </v-data-table>
        </div>
      </v-card-text>
      </template>

      <template v-else-if="viewTab === 'deps'">
      <v-card-title class="d-flex flex-column align-stretch px-6 py-4 pb-2">
        <div>{{ t('projectManagement.depsTitle') }}</div>
        <div class="text-body-2 text-medium-emphasis font-weight-regular mt-1 text-wrap">
          {{ t('projectManagement.depsHint') }}
        </div>
      </v-card-title>
      <v-card-text class="px-6 pt-0">
        <div class="d-flex align-center justify-space-between flex-wrap ga-3 mb-4">
          <v-text-field
            v-model="depSearch"
            :label="t('projectManagement.depsSearch')"
            density="comfortable"
            hide-details
            clearable
            class="flex-grow-1"
            style="max-width: 320px"
          />
          <v-btn color="primary" :disabled="wbs.length < 2" @click="depDialog = true">
            <PlusIcon size="18" class="mr-1" />
            {{ t('projectManagement.newDependency') }}
          </v-btn>
        </div>
        <div class="pm-wbs-table-wrap">
        <v-data-table
          v-model:page="depPage"
          v-model:items-per-page="depItemsPerPage"
          :headers="depHeaders"
          :items="filteredDeps"
          :loading="loading"
          item-value="id"
          density="comfortable"
          class="rounded-lg border pm-wbs-table"
          :items-per-page-options="wbsItemsPerPageOptions"
        >
          <template #item.predecessorId="{ item }">
            <div>{{ wbsName(item.predecessorId) }}</div>
          </template>
          <template #item.successorId="{ item }">
            <div class="d-flex align-center ga-2 flex-wrap">
              <span>{{ wbsName(item.successorId) }}</span>
              <v-chip v-if="depDateClash(item)" size="x-small" color="warning" variant="tonal">
                {{ t('projectManagement.depsDateClash') }}
              </v-chip>
            </div>
          </template>
          <template #item.type>
            <v-chip size="small" color="primary" variant="tonal">
              {{ t('projectManagement.depsTypeFs') }}
            </v-chip>
          </template>
          <template #item.lagDays="{ item }">
            {{ lagLabel(item.lagDays) }}
          </template>
          <template #item.actions="{ item }">
            <div class="d-flex justify-end">
              <v-btn icon size="small" variant="text" color="error" @click="removeDependency(item)">
                <TrashIcon size="18" />
                <v-tooltip activator="parent" location="top">{{ t('projectManagement.delete') }}</v-tooltip>
              </v-btn>
            </div>
          </template>
          <template #no-data>
            <div class="text-center py-8 text-medium-emphasis">
              {{ dependencies.length ? t('projectManagement.depsFilterEmpty') : t('projectManagement.emptyDeps') }}
            </div>
          </template>
        </v-data-table>
        </div>
      </v-card-text>
      </template>

      <v-card-text v-else-if="viewTab === 'decisions'" class="px-6 py-4">
        <PmDecisionsPanel
          :project-id="projectId"
          :project-code="detail?.project.code || ''"
          :hub-folder-id="detail?.project.diFolderId"
          :decisions="decisions"
          :wbs="wbs"
          :loading="loading || tabLoading"
          @changed="onPanelChanged"
          @hub-ready="onLibraryHubReady"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'raid'" class="px-6 py-4">
        <PmRaidPanel
          :project-id="projectId"
          :items="raidItems"
          :wbs="wbs"
          :loading="loading || tabLoading"
          @changed="onPanelChanged"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'capacity'" class="px-6 py-4">
        <PmCapacityPanel
          :project-id="projectId"
          :assignments="assignments"
          :capacity="capacity"
          :wbs="wbs"
          :loading="loading || tabLoading"
          @changed="onPanelChanged"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'budget'" class="px-6 py-4">
        <PmBudgetPanel
          :project-id="projectId"
          :lines="budgetLines"
          :budget="budget"
          :wbs="wbs"
          :loading="loading || tabLoading"
          @changed="onPanelChanged"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'acks'" class="px-6 py-4">
        <PmAcksPanel
          :project-id="projectId"
          :project-code="detail?.project.code || ''"
          :hub-folder-id="detail?.project.diFolderId"
          :items="acknowledgements"
          :wbs="wbs"
          :loading="loading || tabLoading"
          @changed="onPanelChanged"
          @hub-ready="onLibraryHubReady"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'obligations'" class="px-6 py-4">
        <PmObligationsPanel
          :project-id="projectId"
          :project-code="detail?.project.code || ''"
          :hub-folder-id="detail?.project.diFolderId"
          :items="obligations"
          :wbs="wbs"
          :loading="loading || tabLoading"
          @changed="onPanelChanged"
          @hub-ready="onLibraryHubReady"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'audit'" class="px-6 py-4">
        <PmAuditPacksPanel
          :project-id="projectId"
          :project-code="detail?.project.code || ''"
          :hub-folder-id="detail?.project.diFolderId"
          :items="auditPacks"
          :wbs="wbs"
          :loading="loading || tabLoading"
          @changed="onPanelChanged"
          @hub-ready="onLibraryHubReady"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'meetings'" class="px-6 py-4">
        <PmMeetingsPanel
          :project-id="projectId"
          :items="meetings"
          :wbs="wbs"
          :loading="loading || tabLoading"
          @changed="onPanelChanged"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'stakeholders'" class="px-6 py-4">
        <PmStakeholdersPanel
          :project-id="projectId"
          :project-code="detail?.project.code || ''"
          :hub-folder-id="detail?.project.diFolderId"
          :items="stakeholders"
          :wbs="wbs"
          :loading="loading || tabLoading"
          @changed="onPanelChanged"
          @hub-ready="onLibraryHubReady"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'processMaps'" class="px-6 py-4">
        <PmProcessMapsPanel
          :project-id="projectId"
          :items="processMaps"
          :wbs="wbs"
          :loading="loading || tabLoading"
          @changed="onPanelChanged"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'gates'" class="px-6 py-4">
        <PmStageGatesPanel
          :project-id="projectId"
          :gates="stageGates"
          :wbs="wbs"
          :loading="loading"
          @changed="loadDetail"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'packs'" class="px-6 py-4">
        <PmPackCatalog
          :project-id="projectId"
          :project-code="detail?.project.code || ''"
          @changed="loadDetail"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'library'" class="pa-0">
        <PmDiLibrary
          :project-id="projectId"
          :project-code="detail?.project.code || ''"
          :hub-folder-id="detail?.project.diFolderId"
          :wbs="wbs"
          @hub-ready="onLibraryHubReady"
          @bound="onLibraryBound"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'status'" class="px-6 py-4">
        <PmStatusPack :pack="statusPack" :loading="statusLoading || tabLoading || loading" />
      </v-card-text>
        </div>
      </div>
    </v-card>

    <v-dialog v-model="wbsDialog" max-width="640">
      <v-card rounded="lg">
        <v-card-title class="d-flex align-start ga-3 px-6 py-4">
          <v-avatar color="primary" variant="tonal" rounded="lg">
            <v-icon :icon="wbsEditingId ? 'mdi-pencil-outline' : 'mdi-file-tree-outline'" />
          </v-avatar>
          <div class="flex-grow-1">
            <div class="d-flex align-center ga-2 flex-wrap">
              <span>{{ wbsEditingId ? t('projectManagement.editWbs') : t('projectManagement.newWbs') }}</span>
              <v-chip v-if="editingWbs?.wbsCode" size="small" variant="tonal">{{ editingWbs.wbsCode }}</v-chip>
            </div>
            <div class="text-body-2 text-medium-emphasis font-weight-regular mt-1">
              {{ wbsDialogSubtitle }}
            </div>
          </div>
        </v-card-title>
        <v-divider />
        <v-card-text class="d-flex flex-column ga-5 px-6 py-5">
          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.wbsDialog.sectionKind') }}</div>
            <div class="d-flex flex-column ga-2">
              <v-card
                v-for="choice in wbsKindChoices"
                :key="choice.value"
                :variant="wbsForm.kind === choice.value ? 'tonal' : 'outlined'"
                :color="wbsForm.kind === choice.value ? 'primary' : undefined"
                rounded="lg"
                class="pm-wbs-kind-card pa-3"
                role="button"
                @click="wbsForm.kind = choice.value"
              >
                <div class="d-flex align-start ga-3">
                  <v-icon :icon="choice.icon" size="22" />
                  <div>
                    <div class="font-weight-medium">{{ choice.title }}</div>
                    <div class="text-caption text-medium-emphasis">{{ choice.hint }}</div>
                  </div>
                </div>
              </v-card>
            </div>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.wbsDialog.sectionIdentity') }}</div>
            <v-text-field
              v-model="wbsForm.name"
              :label="t('projectManagement.fields.name')"
              density="comfortable"
              autofocus
              hide-details="auto"
              class="mb-3"
            />
            <v-select
              v-model="wbsForm.parentId"
              :items="parentItems"
              :label="t('projectManagement.fields.parent')"
              :hint="t('projectManagement.wbsDialog.parentHint')"
              persistent-hint
              density="comfortable"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.wbsDialog.sectionPlan') }}</div>
            <p class="text-caption text-medium-emphasis mb-3">{{ t('projectManagement.wbsDialog.planHint') }}</p>
            <div class="d-flex ga-3">
              <v-text-field
                v-model="wbsForm.plannedStart"
                type="date"
                :label="t('projectManagement.fields.plannedStart')"
                density="comfortable"
                hide-details
                prepend-inner-icon="mdi-calendar-start"
              />
              <v-text-field
                v-model="wbsForm.plannedFinish"
                type="date"
                :label="t('projectManagement.fields.plannedFinish')"
                density="comfortable"
                hide-details
                prepend-inner-icon="mdi-calendar-end"
              />
            </div>
          </section>

          <section v-if="wbsEditingId">
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.wbsDialog.sectionActual') }}</div>
            <p class="text-caption text-medium-emphasis mb-3">{{ t('projectManagement.wbsDialog.actualHint') }}</p>
            <div class="d-flex ga-3">
              <v-text-field
                v-model="wbsForm.actualStart"
                type="date"
                :label="t('projectManagement.fields.actualStart')"
                density="comfortable"
                hide-details
                prepend-inner-icon="mdi-calendar-check"
              />
              <v-text-field
                v-model="wbsForm.actualFinish"
                type="date"
                :label="t('projectManagement.fields.actualFinish')"
                density="comfortable"
                hide-details
                prepend-inner-icon="mdi-calendar-check-outline"
              />
            </div>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.wbsDialog.sectionProgress') }}</div>
            <div class="d-flex ga-3">
              <v-text-field
                v-model.number="wbsForm.weight"
                type="number"
                min="0"
                :label="t('projectManagement.fields.weight')"
                :hint="t('projectManagement.weightHint')"
                persistent-hint
                density="comfortable"
              />
              <v-text-field
                v-model.number="wbsForm.percentComplete"
                type="number"
                min="0"
                max="100"
                :label="t('projectManagement.fields.percentComplete')"
                density="comfortable"
                :disabled="percentLocked"
                :hint="percentHint || t('projectManagement.wbsDialog.percentHint')"
                persistent-hint
                suffix="%"
              />
            </div>
          </section>

          <section v-if="wbsEditingId && editingWbs">
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.wbsDialog.sectionWork') }}</div>
            <p class="text-caption text-medium-emphasis mb-3">
              {{ editingWbs.workItemId ? t('projectManagement.wbsDialog.workBound') : t('projectManagement.wbsDialog.workUnbound') }}
            </p>
            <div class="d-flex align-center ga-2 flex-wrap">
              <NuxtLink
                v-if="editingWbs.workItemId && editingWbs.workItemKey"
                :to="workItemHref(editingWbs.workItemId)"
                class="text-decoration-none"
                @click.stop
              >
                <v-chip size="small" :color="stateChipColor(editingWbs)" variant="tonal">
                  {{ editingWbs.workItemKey }}
                  <span v-if="editingWbs.workItemStateName" class="ml-1">· {{ editingWbs.workItemStateName }}</span>
                </v-chip>
              </NuxtLink>
              <v-btn
                v-if="editingWbs.workItemId"
                size="small"
                variant="tonal"
                @click="unbindWorkItem(editingWbs)"
              >
                {{ t('projectManagement.unbindWorkItem') }}
              </v-btn>
              <v-btn
                v-else
                size="small"
                variant="tonal"
                :disabled="!detail?.project.workspaceId"
                @click="openBindWorkItem(editingWbs)"
              >
                {{ t('projectManagement.bindWorkItem') }}
              </v-btn>
              <v-btn
                v-if="editingWbs.workItemId"
                size="small"
                variant="tonal"
                @click="openBindEvidence(editingWbs)"
              >
                {{ t('projectManagement.bindEvidence') }}
              </v-btn>
            </div>
          </section>
        </v-card-text>
        <v-divider />
        <v-card-actions class="px-6 py-3">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="wbsDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn
            color="primary"
            rounded="lg"
            class="text-none"
            :loading="wbsSaving"
            :disabled="!wbsForm.name.trim()"
            @click="saveWbs"
          >
            {{ t('projectManagement.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="depDialog" max-width="520">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.newDependency') }}</v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-select
            v-model="depForm.predecessorId"
            :items="wbsSelectItems"
            :label="t('projectManagement.fields.predecessor')"
            density="comfortable"
          />
          <v-select
            v-model="depForm.successorId"
            :items="wbsSelectItems"
            :label="t('projectManagement.fields.successor')"
            density="comfortable"
          />
          <v-text-field
            v-model.number="depForm.lagDays"
            type="number"
            :label="t('projectManagement.fields.lagDays')"
            density="comfortable"
          />
          <div class="text-medium-emphasis text-body-2">{{ t('projectManagement.fsOnlyHint') }}</div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="depDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn
            color="primary"
            :loading="depSaving"
            :disabled="!depForm.predecessorId || !depForm.successorId"
            @click="saveDependency"
          >
            {{ t('projectManagement.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="baselineDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.setBaseline') }}</v-card-title>
        <v-card-text>
          <p class="mb-4">{{ t('projectManagement.baselineConfirm') }}</p>
          <v-textarea
            v-model="baselineNote"
            :label="t('projectManagement.fields.baselineNote')"
            density="comfortable"
            rows="2"
            auto-grow
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="baselineDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="baselineSaving" @click="applyBaseline">
            {{ t('projectManagement.setBaseline') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteWbsDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.deleteWbsTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.deleteWbsConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteWbsDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="deletingWbs" @click="executeDeleteWbs">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <PmPickWorkItemDialog
      v-model="bindDialog"
      :project-id="projectId"
      :title="t('projectManagement.bindWorkItem')"
      :hint="t('projectManagement.bindHint')"
      :confirm-label="t('projectManagement.bindWorkItem')"
      @pick="bindSelected"
    />
    <v-dialog v-model="evidenceDialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.bindEvidence') }}</v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <div class="text-medium-emphasis text-body-2">{{ t('projectManagement.bindEvidenceHint') }}</div>
          <div v-if="evidenceBound.length" class="d-flex flex-column ga-2">
            <div class="text-subtitle-2">{{ t('projectManagement.boundEvidence') }}</div>
            <v-list class="rounded-lg border" density="comfortable">
              <v-list-item v-for="doc in evidenceBound" :key="doc.resourceId">
                <v-list-item-title>
                  <NuxtLink :to="resourceHref(doc.resourceId)" class="text-primary" @click.stop>
                    {{ doc.name }}
                  </NuxtLink>
                </v-list-item-title>
                <v-list-item-subtitle>{{ doc.relationType }}</v-list-item-subtitle>
                <template #append>
                  <v-btn
                    size="small"
                    variant="text"
                    :disabled="evidenceSaving"
                    @click="unbindEvidence(doc.resourceId)"
                  >
                    {{ t('projectManagement.unbindEvidence') }}
                  </v-btn>
                </template>
              </v-list-item>
            </v-list>
          </div>
          <v-btn
            color="primary"
            variant="tonal"
            class="text-none align-self-start"
            :disabled="evidenceSaving"
            @click="evidencePickOpen = true"
          >
            {{ t('projectManagement.pickDocument.open') }}
          </v-btn>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="evidenceDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="referenceDialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.bindReference') }}</v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <div class="text-medium-emphasis text-body-2">{{ t('projectManagement.bindReferenceHint') }}</div>
          <div v-if="referenceBound.length" class="d-flex flex-column ga-2">
            <div class="text-subtitle-2">{{ t('projectManagement.boundReference') }}</div>
            <v-list class="rounded-lg border" density="comfortable">
              <v-list-item v-for="doc in referenceBound" :key="doc.resourceId">
                <v-list-item-title>
                  <NuxtLink :to="resourceHref(doc.resourceId)" class="text-primary" @click.stop>
                    {{ doc.name }}
                  </NuxtLink>
                </v-list-item-title>
                <v-list-item-subtitle>{{ doc.relationType }}</v-list-item-subtitle>
                <template #append>
                  <v-btn
                    size="small"
                    variant="text"
                    :disabled="referenceSaving"
                    @click="unbindReference(doc.resourceId)"
                  >
                    {{ t('projectManagement.unbindReference') }}
                  </v-btn>
                </template>
              </v-list-item>
            </v-list>
          </div>
          <v-btn
            color="primary"
            variant="tonal"
            class="text-none align-self-start"
            :disabled="referenceSaving"
            @click="referencePickOpen = true"
          >
            {{ t('projectManagement.pickDocument.open') }}
          </v-btn>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="referenceDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <PmPickDocumentDialog
      v-model="evidencePickOpen"
      :project-id="projectId"
      :project-code="detail?.project.code || ''"
      :hub-folder-id="detail?.project.diFolderId"
      :exclude-resource-ids="evidenceBound.map((row) => row.resourceId)"
      @pick="onEvidencePicked"
      @hub-ready="onLibraryHubReady"
    />
    <PmPickDocumentDialog
      v-model="referencePickOpen"
      :project-id="projectId"
      :project-code="detail?.project.code || ''"
      :hub-folder-id="detail?.project.diFolderId"
      :exclude-resource-ids="referenceBound.map((row) => row.resourceId)"
      @pick="onReferencePicked"
      @hub-ready="onLibraryHubReady"
    />
  </div>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.pm-side-tabs {
  min-width: 232px;
  max-width: 260px;
  max-height: calc(100vh - 220px);
  overflow-y: auto;
}
.pm-side-tabs__group {
  padding: 12px 16px 4px;
  font-size: 0.75rem;
  font-weight: 500;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  opacity: 0.6;
}
@media (max-width: 959px) {
  .pm-side-tabs {
    min-width: 0;
    max-width: none;
    max-height: none;
    overflow: visible;
  }
}
.pm-side-tabs :deep(.v-tab) {
  justify-content: flex-start;
  text-align: left;
}
.pm-project-main {
  min-width: 0;
  flex: 1 1 auto;
}
.pm-wbs-kind-card {
  cursor: pointer;
}
.pm-wbs-table-wrap {
  overflow-x: auto;
}
.pm-wbs-table :deep(thead th:last-child),
.pm-wbs-table :deep(tbody td:last-child) {
  position: sticky;
  right: 0;
  z-index: 2;
  background: rgb(var(--v-theme-surface));
  box-shadow: -4px 0 8px -4px rgba(0, 0, 0, 0.12);
  white-space: nowrap;
}
.pm-wbs-table :deep(thead th:last-child) {
  z-index: 3;
}
</style>
