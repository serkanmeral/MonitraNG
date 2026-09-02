<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import PmPortfolioBar from '@/components/apps/project-management/PmPortfolioBar.vue';
import {
  pmCreateProject,
  pmDateInput,
  pmDatePayload,
  pmDeleteProject,
  pmGetPortfolio,
  pmListJobPacks,
  pmListProjects,
} from '@/services/projectManagementService';
import type {
  PmJobPack,
  PmPortfolio,
  PmPortfolioProject,
  PmProject,
  PmProjectStatus,
} from '@/types/apps/projectManagement';
import { applyJobPackDocuments } from '@/utils/pmJobPack';
import { EditIcon, FlagIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();
const router = useRouter();

const loading = ref(false);
const saving = ref(false);
const deleting = ref(false);
const items = ref<PmPortfolioProject[]>([]);
const portfolio = ref<PmPortfolio | null>(null);
const searchQuery = ref('');
const viewFilter = ref<'all' | 'attention' | 'active'>('all');

const dialogOpen = ref(false);
const deleteDialog = ref(false);
const rowToDelete = ref<PmProject | null>(null);

const form = ref({
  code: '',
  name: '',
  description: '',
  status: 'draft' as PmProjectStatus,
  plannedStart: '',
  plannedFinish: '',
  packCode: '',
});
const jobPacks = ref<PmJobPack[]>([]);

const page = computed(() => ({ title: t('projectManagement.title') }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('projectManagement.title'), disabled: true, href: '#' },
]);

const packItems = computed(() => [
  { title: t('projectManagement.pack.none'), value: '' },
  ...jobPacks.value.map((row) => ({ title: row.name, value: row.code })),
]);

const selectedPack = computed(() => jobPacks.value.find((row) => row.code === form.value.packCode) || null);

const statusItems = computed(() => [
  { title: t('projectManagement.status.draft'), value: 'draft' },
  { title: t('projectManagement.status.active'), value: 'active' },
  { title: t('projectManagement.status.closed'), value: 'closed' },
]);

const headers = computed(() => [
  { title: t('projectManagement.fields.code'), key: 'code', width: 140 },
  { title: t('projectManagement.fields.name'), key: 'name', minWidth: 220 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.portfolio.percent'), key: 'percentComplete', width: 90 },
  { title: t('projectManagement.fields.plannedStart'), key: 'plannedStart', width: 140 },
  { title: t('projectManagement.fields.plannedFinish'), key: 'plannedFinish', width: 140 },
  { title: t('projectManagement.fields.baseline'), key: 'baseline', width: 140 },
  { title: t('projectManagement.portfolio.attention'), key: 'flags', minWidth: 180 },
  {
    title: t('projectManagement.actions'),
    key: 'actions',
    width: 120,
    sortable: false,
    align: 'end' as const,
  },
]);

const filteredItems = computed(() => {
  let list = items.value;
  if (viewFilter.value === 'attention') list = list.filter((row) => row.attention);
  if (viewFilter.value === 'active') list = list.filter((row) => row.status === 'active');
  const q = searchQuery.value.trim().toLowerCase();
  if (!q) return list;
  return list.filter((row) =>
    [row.code, row.name, row.status].some((v) => String(v || '').toLowerCase().includes(q)),
  );
});

function statusLabel(status?: string | null) {
  const key = `projectManagement.status.${status || 'draft'}`;
  const label = t(key);
  return label === key ? (status || 'draft') : label;
}

function statusColor(status?: string | null) {
  if (status === 'active') return 'success';
  if (status === 'closed') return 'default';
  return 'warning';
}

function asPortfolioRow(row: PmProject): PmPortfolioProject {
  return {
    ...row,
    percentComplete: 0,
    attention: false,
    flags: [],
    counts: {
      delayed: 0,
      milestoneAtRisk: 0,
      drifted: 0,
      unboundLeaf: 0,
      openWork: 0,
      missingEvidence: 0,
      missingApproval: 0,
    },
  };
}

function flagLabel(flag: string) {
  const key = `projectManagement.statusPack.flag.${flag}`;
  const label = t(key);
  return label === key ? flag : label;
}

function flagColor(flag: string) {
  if (
    flag === 'delayed' ||
    flag === 'milestoneAtRisk' ||
    flag === 'failedGate' ||
    flag === 'openRisk' ||
    flag === 'overloadedResource' ||
    flag === 'overBudget' ||
    flag === 'overdueAck' ||
    flag === 'overdueObligation' ||
    flag === 'overdueAuditPack' ||
    flag === 'overdueMeetingAction' ||
    flag === 'overdueStakeholder'
  ) {
    return 'error';
  }
  return 'warning';
}

async function loadItems() {
  loading.value = true;
  try {
    try {
      const pack = await pmGetPortfolio();
      portfolio.value = pack;
      items.value = pack.items ?? [];
    } catch {
      portfolio.value = null;
      items.value = (await pmListProjects()).map(asPortfolioRow);
    }
    try {
      jobPacks.value = await pmListJobPacks();
    } catch {
      jobPacks.value = [];
    }
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  form.value = {
    code: '',
    name: '',
    description: '',
    status: 'draft',
    plannedStart: '',
    plannedFinish: '',
    packCode: '',
  };
  dialogOpen.value = true;
}

function openDetail(row: PmProject) {
  void router.push(`/apps/project-management/${encodeURIComponent(row.id)}`);
}

function confirmDelete(row: PmProject) {
  rowToDelete.value = row;
  deleteDialog.value = true;
}

async function saveProject() {
  saving.value = true;
  try {
    const created = await pmCreateProject({
      code: form.value.code.trim(),
      name: form.value.name.trim(),
      description: form.value.description.trim() || null,
      status: form.value.status,
      plannedStart: pmDatePayload(form.value.plannedStart),
      plannedFinish: pmDatePayload(form.value.plannedFinish),
      packCode: form.value.packCode || null,
    });
    if (form.value.packCode && selectedPack.value) {
      try {
        await applyJobPackDocuments(created.id, created.code, selectedPack.value);
      } catch (error) {
        panelError(error, 'projectManagement.errors.packDocsFailed');
      }
    }
    dialogOpen.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.created'),
      severity: 'success',
    });
    await router.push(`/apps/project-management/${encodeURIComponent(created.id)}`);
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function executeDelete() {
  if (!rowToDelete.value) return;
  deleting.value = true;
  try {
    await pmDeleteProject(rowToDelete.value.id);
    deleteDialog.value = false;
    rowToDelete.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.deleted'),
      severity: 'success',
    });
    await loadItems();
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  } finally {
    deleting.value = false;
  }
}

onMounted(() => {
  void loadItems();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-card elevation="0" class="withbg">
      <v-card-title class="d-flex align-center justify-space-between flex-wrap ga-2 px-6 py-4">
        <div class="d-flex align-center ga-2">
          <FlagIcon size="22" />
          <span class="text-h6">{{ t('projectManagement.title') }}</span>
        </div>
        <div class="d-flex ga-2">
          <v-btn variant="tonal" :loading="loading" @click="loadItems">
            <RefreshIcon size="18" class="mr-1" />
            {{ t('projectManagement.refresh') }}
          </v-btn>
          <v-btn color="primary" @click="openCreate">
            <PlusIcon size="18" class="mr-1" />
            {{ t('projectManagement.newProject') }}
          </v-btn>
        </div>
      </v-card-title>

      <v-card-text class="px-6 pb-2">
        <PmPortfolioBar :filter="viewFilter" :pack="portfolio" class="mb-4" @update:filter="viewFilter = $event" />
        <v-text-field
          v-model="searchQuery"
          :label="t('projectManagement.search')"
          density="comfortable"
          hide-details
          clearable
        />
      </v-card-text>

      <v-card-text class="px-6 pt-0">
        <v-data-table
          :headers="headers"
          :items="filteredItems"
          :loading="loading"
          item-value="id"
          density="comfortable"
          class="rounded-lg border"
          hover
          @click:row="(_e: unknown, { item }: { item: PmPortfolioProject }) => openDetail(item)"
        >
          <template #item.status="{ item }">
            <v-chip size="small" :color="statusColor(item.status)" variant="tonal">
              {{ statusLabel(item.status) }}
            </v-chip>
          </template>
          <template #item.percentComplete="{ item }">
            {{ Math.round(item.percentComplete || 0) }}%
          </template>
          <template #item.plannedStart="{ item }">
            {{ pmDateInput(item.plannedStart) || '—' }}
          </template>
          <template #item.plannedFinish="{ item }">
            {{ pmDateInput(item.plannedFinish) || '—' }}
          </template>
          <template #item.baseline="{ item }">
            <v-chip v-if="item.baselineDrifted" size="small" color="warning" variant="tonal">
              {{ t('projectManagement.drift') }}
            </v-chip>
            <span v-else-if="item.baselineSetAt">{{ pmDateInput(item.baselineSetAt) }}</span>
            <span v-else class="text-medium-emphasis">{{ t('projectManagement.noBaseline') }}</span>
          </template>
          <template #item.flags="{ item }">
            <div class="d-flex flex-wrap ga-1">
              <v-chip
                v-for="flag in (item.flags || []).slice(0, 4)"
                :key="flag"
                size="x-small"
                :color="flagColor(flag)"
                variant="tonal"
              >
                {{ flagLabel(flag) }}
              </v-chip>
              <span v-if="!item.attention" class="text-medium-emphasis">—</span>
            </div>
          </template>
          <template #item.actions="{ item }">
            <div class="d-flex justify-end ga-1" @click.stop>
              <v-btn icon size="small" variant="text" @click="openDetail(item)">
                <EditIcon size="18" />
              </v-btn>
              <v-btn icon size="small" variant="text" color="error" @click="confirmDelete(item)">
                <TrashIcon size="18" />
              </v-btn>
            </div>
          </template>
          <template #no-data>
            <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.empty') }}</div>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <v-dialog v-model="dialogOpen" max-width="560">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.newProject') }}</v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="form.code" :label="t('projectManagement.fields.code')" density="comfortable" />
          <v-text-field v-model="form.name" :label="t('projectManagement.fields.name')" density="comfortable" />
          <v-textarea
            v-model="form.description"
            :label="t('projectManagement.fields.description')"
            density="comfortable"
            rows="2"
            auto-grow
          />
          <v-select
            v-model="form.status"
            :items="statusItems"
            :label="t('projectManagement.fields.status')"
            density="comfortable"
          />
          <div class="d-flex ga-3">
            <v-text-field
              v-model="form.plannedStart"
              type="date"
              :label="t('projectManagement.fields.plannedStart')"
              density="comfortable"
            />
            <v-text-field
              v-model="form.plannedFinish"
              type="date"
              :label="t('projectManagement.fields.plannedFinish')"
              density="comfortable"
            />
          </div>
          <v-select
            v-model="form.packCode"
            :items="packItems"
            :label="t('projectManagement.pack.label')"
            density="comfortable"
          />
          <div v-if="selectedPack" class="text-body-2 text-medium-emphasis">
            {{ selectedPack.description }}
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialogOpen = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" :disabled="!form.code.trim() || !form.name.trim()" @click="saveProject">
            {{ t('projectManagement.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.deleteConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="executeDelete">{{ t('projectManagement.delete') }}</v-btn>
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
