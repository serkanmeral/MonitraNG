<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import PmAcksPanel from '@/components/apps/project-management/PmAcksPanel.vue';
import PmAuditPacksPanel from '@/components/apps/project-management/PmAuditPacksPanel.vue';
import PmMeetingsPanel from '@/components/apps/project-management/PmMeetingsPanel.vue';
import PmStakeholdersPanel from '@/components/apps/project-management/PmStakeholdersPanel.vue';
import PmProcessMapsPanel from '@/components/apps/project-management/PmProcessMapsPanel.vue';
import PmBudgetPanel from '@/components/apps/project-management/PmBudgetPanel.vue';
import PmCapacityPanel from '@/components/apps/project-management/PmCapacityPanel.vue';
import PmDecisionsPanel from '@/components/apps/project-management/PmDecisionsPanel.vue';
import PmGanttChart from '@/components/apps/project-management/PmGanttChart.vue';
import PmObligationsPanel from '@/components/apps/project-management/PmObligationsPanel.vue';
import PmPackCatalog from '@/components/apps/project-management/PmPackCatalog.vue';
import PmRaidPanel from '@/components/apps/project-management/PmRaidPanel.vue';
import PmStageGatesPanel from '@/components/apps/project-management/PmStageGatesPanel.vue';
import PmStatusPack from '@/components/apps/project-management/PmStatusPack.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import { ocListWorkspaces } from '@/services/operationCoreService';
import {
  pmBindWbsWorkItem,
  pmCreateDependency,
  pmCreateWbs,
  pmDateInput,
  pmDatePayload,
  pmDeleteDependency,
  pmDeleteWbs,
  pmGetProject,
  pmGetProjectStatus,
  pmSearchProjectWorkItems,
  pmSetBaseline,
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
} from '@/types/apps/projectManagement';
import {
  ArrowLeftIcon,
  FlagIcon,
  PlusIcon,
  RefreshIcon,
  TrashIcon,
} from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
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
const viewTab = ref<'gantt' | 'wbs' | 'deps' | 'decisions' | 'raid' | 'capacity' | 'budget' | 'acks' | 'obligations' | 'audit' | 'meetings' | 'stakeholders' | 'processMaps' | 'gates' | 'packs' | 'status'>('gantt');
const statusPack = ref<PmProjectStatusPack | null>(null);
const statusLoading = ref(false);
const workspaces = ref<OpWorkspace[]>([]);
const bindDialog = ref(false);
const bindTarget = ref<PmWbsItem | null>(null);
const bindQuery = ref('');
const bindLoading = ref(false);
const bindSaving = ref(false);
const bindCandidates = ref<PmWorkItemCandidate[]>([]);

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

const kindItems = computed(() => [
  { title: t('projectManagement.kind.summary'), value: 'summary' },
  { title: t('projectManagement.kind.task'), value: 'task' },
  { title: t('projectManagement.kind.milestone'), value: 'milestone' },
]);

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
const percentLocked = computed(() => Boolean(editingWbs.value?.workItemId));

function hasChildren(id: string) {
  return wbs.value.some((row) => row.parentId === id);
}

function workItemHref(id: string) {
  return `/apps/operation-core/work-items/${encodeURIComponent(id)}/profile`;
}

function stateChipColor(item: { workItemClosed?: boolean; workItemStateCategory?: string | null }) {
  if (item.workItemClosed) return 'success';
  if (item.workItemStateCategory === 'in_progress') return 'info';
  return 'default';
}

const wbsHeaders = computed(() => [
  { title: t('projectManagement.fields.wbsCode'), key: 'wbsCode', width: 110 },
  { title: t('projectManagement.fields.name'), key: 'name', minWidth: 220 },
  { title: t('projectManagement.fields.kind'), key: 'kind', width: 130 },
  { title: t('projectManagement.fields.workItem'), key: 'workItem', minWidth: 140 },
  { title: t('projectManagement.fields.weight'), key: 'weight', width: 80 },
  { title: t('projectManagement.fields.plannedStart'), key: 'plannedStart', width: 130 },
  { title: t('projectManagement.fields.plannedFinish'), key: 'plannedFinish', width: 130 },
  { title: t('projectManagement.fields.percentComplete'), key: 'percentComplete', width: 90 },
  { title: t('projectManagement.fields.baseline'), key: 'baseline', width: 110 },
  {
    title: t('projectManagement.actions'),
    key: 'actions',
    width: 180,
    sortable: false,
    align: 'end' as const,
  },
]);

const depHeaders = computed(() => [
  { title: t('projectManagement.fields.predecessor'), key: 'predecessor', minWidth: 180 },
  { title: t('projectManagement.fields.successor'), key: 'successor', minWidth: 180 },
  { title: t('projectManagement.fields.type'), key: 'type', width: 80 },
  { title: t('projectManagement.fields.lagDays'), key: 'lagDays', width: 90 },
  {
    title: t('projectManagement.actions'),
    key: 'actions',
    width: 80,
    sortable: false,
    align: 'end' as const,
  },
]);

function wbsName(id: string) {
  const row = wbs.value.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
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

async function loadDetail() {
  if (!projectId.value) return;
  loading.value = true;
  try {
    const next = await pmGetProject(projectId.value);
    detail.value = next;
    applyProjectForm(next.project);
    await loadStatusPack();
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

async function searchBindCandidates() {
  if (!detail.value) return;
  bindLoading.value = true;
  try {
    bindCandidates.value = await pmSearchProjectWorkItems(detail.value.project.id, bindQuery.value);
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    bindLoading.value = false;
  }
}

function openBindWorkItem(row: PmWbsItem) {
  bindTarget.value = row;
  bindQuery.value = '';
  bindCandidates.value = [];
  bindDialog.value = true;
  void searchBindCandidates();
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

let bindSearchTimer: ReturnType<typeof setTimeout> | null = null;
watch(bindQuery, () => {
  if (!bindDialog.value) return;
  if (bindSearchTimer) clearTimeout(bindSearchTimer);
  bindSearchTimer = setTimeout(() => {
    void searchBindCandidates();
  }, 300);
});

onMounted(() => {
  void loadWorkspaces();
  void loadDetail();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="detail?.project.name || t('projectManagement.detailTitle')" :breadcrumbs="breadcrumbs" />

    <v-card elevation="0" class="withbg mb-4">
      <v-card-title class="d-flex align-center justify-space-between flex-wrap ga-2 px-6 py-4">
        <div class="d-flex align-center ga-2">
          <v-btn icon variant="text" @click="router.push('/apps/project-management')">
            <ArrowLeftIcon size="20" />
          </v-btn>
          <FlagIcon size="22" />
          <span class="text-h6">{{ detail?.project.code }} — {{ detail?.project.name }}</span>
          <v-chip v-if="detail?.project.baselineDrifted" size="small" color="warning" variant="tonal">
            {{ t('projectManagement.drift') }}
          </v-chip>
        </div>
        <div class="d-flex ga-2">
          <v-btn variant="tonal" :loading="loading" @click="loadDetail">
            <RefreshIcon size="18" class="mr-1" />
            {{ t('projectManagement.refresh') }}
          </v-btn>
          <v-btn color="primary" variant="tonal" @click="baselineDialog = true">
            {{ t('projectManagement.setBaseline') }}
          </v-btn>
        </div>
      </v-card-title>
      <v-card-text class="px-6 pb-6">
        <div class="d-flex flex-column ga-3" style="max-width: 720px">
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
              {{ t('projectManagement.baselineAt') }}: {{ pmDateInput(detail.project.baselineSetAt) }}
              <span v-if="detail.project.baselineSetBy"> ({{ detail.project.baselineSetBy }})</span>
            </span>
            <span v-else>{{ t('projectManagement.noBaseline') }}</span>
          </div>
          <div>
            <v-btn color="primary" :loading="saving" :disabled="!projectForm.name.trim()" @click="saveProject">
              {{ t('projectManagement.save') }}
            </v-btn>
          </div>
        </div>
      </v-card-text>
    </v-card>

    <v-card elevation="0" class="withbg mb-4">
      <v-tabs v-model="viewTab" color="primary" class="px-4 pt-2">
        <v-tab value="gantt">{{ t('projectManagement.gantt.title') }}</v-tab>
        <v-tab value="wbs">{{ t('projectManagement.wbsTitle') }}</v-tab>
        <v-tab value="deps">{{ t('projectManagement.depsTitle') }}</v-tab>
        <v-tab value="decisions">{{ t('projectManagement.decision.title') }}</v-tab>
        <v-tab value="raid">{{ t('projectManagement.raid.title') }}</v-tab>
        <v-tab value="capacity">{{ t('projectManagement.capacity.title') }}</v-tab>
        <v-tab value="budget">{{ t('projectManagement.budget.title') }}</v-tab>
        <v-tab value="acks">{{ t('projectManagement.ack.title') }}</v-tab>
        <v-tab value="obligations">{{ t('projectManagement.obligation.title') }}</v-tab>
        <v-tab value="audit">{{ t('projectManagement.auditPack.title') }}</v-tab>
        <v-tab value="meetings">{{ t('projectManagement.meeting.title') }}</v-tab>
        <v-tab value="stakeholders">{{ t('projectManagement.stakeholder.title') }}</v-tab>
        <v-tab value="processMaps">{{ t('projectManagement.processMap.title') }}</v-tab>
        <v-tab value="gates">{{ t('projectManagement.stageGate.title') }}</v-tab>
        <v-tab value="packs">{{ t('projectManagement.packCatalog.title') }}</v-tab>
        <v-tab value="status">{{ t('projectManagement.statusPack.title') }}</v-tab>
      </v-tabs>
      <v-divider />

      <v-card-text v-if="viewTab === 'gantt'" class="px-6 py-4">
        <div class="d-flex justify-end mb-3">
          <v-btn color="primary" @click="openCreateWbs()">
            <PlusIcon size="18" class="mr-1" />
            {{ t('projectManagement.newWbs') }}
          </v-btn>
        </div>
        <PmGanttChart :items="wbs" :dependencies="dependencies" @edit="openEditWbs" />
      </v-card-text>

      <template v-else-if="viewTab === 'wbs'">
    <v-card-title class="d-flex align-center justify-space-between px-6 py-4">
        <span>{{ t('projectManagement.wbsTitle') }}</span>
        <v-btn color="primary" @click="openCreateWbs()">
          <PlusIcon size="18" class="mr-1" />
          {{ t('projectManagement.newWbs') }}
        </v-btn>
      </v-card-title>
      <v-card-text class="px-6 pt-0">
        <v-data-table
          :headers="wbsHeaders"
          :items="wbs"
          :loading="loading"
          item-value="id"
          density="comfortable"
          class="rounded-lg border"
          hide-default-footer
          :items-per-page="-1"
        >
          <template #item.name="{ item }">
            <span :style="{ paddingLeft: `${depthOf(item.wbsCode) * 16}px` }">{{ item.name }}</span>
          </template>
          <template #item.kind="{ item }">
            {{ kindLabel(item.kind) }}
          </template>
          <template #item.workItem="{ item }">
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
            {{ pmDateInput(item.plannedStart) || '—' }}
          </template>
          <template #item.plannedFinish="{ item }">
            {{ pmDateInput(item.plannedFinish) || '—' }}
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
          <template #item.actions="{ item }">
            <div class="d-flex justify-end ga-1">
              <v-btn size="small" variant="text" @click="openCreateWbs(item.id)">
                {{ t('projectManagement.addChild') }}
              </v-btn>
              <v-btn size="small" variant="text" @click="openEditWbs(item)">
                {{ t('projectManagement.edit') }}
              </v-btn>
              <v-btn
                v-if="item.workItemId"
                size="small"
                variant="text"
                @click="unbindWorkItem(item)"
              >
                {{ t('projectManagement.unbindWorkItem') }}
              </v-btn>
              <v-btn
                v-else-if="!hasChildren(item.id)"
                size="small"
                variant="text"
                :disabled="!detail?.project.workspaceId"
                @click="openBindWorkItem(item)"
              >
                {{ t('projectManagement.bindWorkItem') }}
              </v-btn>
              <v-btn icon size="small" variant="text" color="error" @click="confirmDeleteWbs(item)">
                <TrashIcon size="18" />
              </v-btn>
            </div>
          </template>
          <template #no-data>
            <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.emptyWbs') }}</div>
          </template>
        </v-data-table>
      </v-card-text>
      </template>

      <template v-else-if="viewTab === 'deps'">
      <v-card-title class="d-flex align-center justify-space-between px-6 py-4">
        <span>{{ t('projectManagement.depsTitle') }}</span>
        <v-btn color="primary" :disabled="wbs.length < 2" @click="depDialog = true">
          <PlusIcon size="18" class="mr-1" />
          {{ t('projectManagement.newDependency') }}
        </v-btn>
      </v-card-title>
      <v-card-text class="px-6 pt-0">
        <v-data-table
          :headers="depHeaders"
          :items="dependencies"
          :loading="loading"
          item-value="id"
          density="comfortable"
          class="rounded-lg border"
          hide-default-footer
          :items-per-page="-1"
        >
          <template #item.predecessor="{ item }">
            {{ wbsName(item.predecessorId) }}
          </template>
          <template #item.successor="{ item }">
            {{ wbsName(item.successorId) }}
          </template>
          <template #item.actions="{ item }">
            <div class="d-flex justify-end">
              <v-btn icon size="small" variant="text" color="error" @click="removeDependency(item)">
                <TrashIcon size="18" />
              </v-btn>
            </div>
          </template>
          <template #no-data>
            <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.emptyDeps') }}</div>
          </template>
        </v-data-table>
      </v-card-text>
      </template>

      <v-card-text v-else-if="viewTab === 'decisions'" class="px-6 py-4">
        <PmDecisionsPanel
          :project-id="projectId"
          :decisions="decisions"
          :wbs="wbs"
          :loading="loading"
          @changed="loadDetail"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'raid'" class="px-6 py-4">
        <PmRaidPanel
          :project-id="projectId"
          :items="raidItems"
          :wbs="wbs"
          :loading="loading"
          @changed="loadDetail"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'capacity'" class="px-6 py-4">
        <PmCapacityPanel
          :project-id="projectId"
          :assignments="assignments"
          :capacity="capacity"
          :wbs="wbs"
          :loading="loading"
          @changed="loadDetail"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'budget'" class="px-6 py-4">
        <PmBudgetPanel
          :project-id="projectId"
          :lines="budgetLines"
          :budget="budget"
          :wbs="wbs"
          :loading="loading"
          @changed="loadDetail"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'acks'" class="px-6 py-4">
        <PmAcksPanel
          :project-id="projectId"
          :items="acknowledgements"
          :wbs="wbs"
          :loading="loading"
          @changed="loadDetail"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'obligations'" class="px-6 py-4">
        <PmObligationsPanel
          :project-id="projectId"
          :items="obligations"
          :wbs="wbs"
          :loading="loading"
          @changed="loadDetail"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'audit'" class="px-6 py-4">
        <PmAuditPacksPanel
          :project-id="projectId"
          :items="auditPacks"
          :wbs="wbs"
          :loading="loading"
          @changed="loadDetail"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'meetings'" class="px-6 py-4">
        <PmMeetingsPanel
          :project-id="projectId"
          :items="meetings"
          :wbs="wbs"
          :loading="loading"
          @changed="loadDetail"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'stakeholders'" class="px-6 py-4">
        <PmStakeholdersPanel
          :project-id="projectId"
          :items="stakeholders"
          :wbs="wbs"
          :loading="loading"
          @changed="loadDetail"
        />
      </v-card-text>

      <v-card-text v-else-if="viewTab === 'processMaps'" class="px-6 py-4">
        <PmProcessMapsPanel
          :project-id="projectId"
          :items="processMaps"
          :wbs="wbs"
          :loading="loading"
          @changed="loadDetail"
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

      <v-card-text v-else-if="viewTab === 'status'" class="px-6 py-4">
        <PmStatusPack :pack="statusPack" :loading="statusLoading || loading" />
      </v-card-text>
    </v-card>

    <v-dialog v-model="wbsDialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title>
          {{ wbsEditingId ? t('projectManagement.editWbs') : t('projectManagement.newWbs') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-select
            v-model="wbsForm.parentId"
            :items="parentItems"
            :label="t('projectManagement.fields.parent')"
            density="comfortable"
          />
          <v-select v-model="wbsForm.kind" :items="kindItems" :label="t('projectManagement.fields.kind')" density="comfortable" />
          <v-text-field v-model="wbsForm.name" :label="t('projectManagement.fields.name')" density="comfortable" />
          <div class="d-flex ga-3">
            <v-text-field
              v-model="wbsForm.plannedStart"
              type="date"
              :label="t('projectManagement.fields.plannedStart')"
              density="comfortable"
            />
            <v-text-field
              v-model="wbsForm.plannedFinish"
              type="date"
              :label="t('projectManagement.fields.plannedFinish')"
              density="comfortable"
            />
          </div>
          <div v-if="wbsEditingId" class="d-flex ga-3">
            <v-text-field
              v-model="wbsForm.actualStart"
              type="date"
              :label="t('projectManagement.fields.actualStart')"
              density="comfortable"
            />
            <v-text-field
              v-model="wbsForm.actualFinish"
              type="date"
              :label="t('projectManagement.fields.actualFinish')"
              density="comfortable"
            />
          </div>
          <v-text-field
            v-model.number="wbsForm.weight"
            type="number"
            min="0"
            :label="t('projectManagement.fields.weight')"
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
            :hint="percentLocked ? t('projectManagement.percentFromWorkItem') : undefined"
            persistent-hint
          />
          <div v-if="wbsEditingId && editingWbs" class="d-flex align-center ga-2 flex-wrap">
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
              v-else-if="!hasChildren(editingWbs.id)"
              size="small"
              variant="tonal"
              :disabled="!detail?.project.workspaceId"
              @click="openBindWorkItem(editingWbs)"
            >
              {{ t('projectManagement.bindWorkItem') }}
            </v-btn>
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="wbsDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="wbsSaving" :disabled="!wbsForm.name.trim()" @click="saveWbs">
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

    <v-dialog v-model="bindDialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.bindWorkItem') }}</v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <div class="text-medium-emphasis text-body-2">{{ t('projectManagement.bindHint') }}</div>
          <v-text-field
            v-model="bindQuery"
            :label="t('projectManagement.searchWorkItem')"
            density="comfortable"
            clearable
            hide-details
          />
          <v-list v-if="bindCandidates.length" class="rounded-lg border" density="comfortable">
            <v-list-item
              v-for="candidate in bindCandidates"
              :key="candidate.id"
              :disabled="bindSaving"
              @click="bindSelected(candidate)"
            >
              <v-list-item-title>{{ candidate.key }} · {{ candidate.title }}</v-list-item-title>
              <v-list-item-subtitle>
                {{ candidate.stateName || (candidate.closed ? t('projectManagement.status.closed') : '—') }}
              </v-list-item-subtitle>
            </v-list-item>
          </v-list>
          <div v-else-if="!bindLoading" class="text-medium-emphasis text-body-2 py-2">
            {{ t('projectManagement.emptyWorkItems') }}
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="bindDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
