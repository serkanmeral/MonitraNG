<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import OcAdminScheduledJobExecutionsDialog from '@/components/apps/operation-core/admin/OcAdminScheduledJobExecutionsDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcAdminScheduledJobRow } from '@/types/apps/scheduler';
import {
  schedulerLoadAdminJobExplorerRows,
  schedulerRunHttpPostJob,
  schedulerRunOcSchedule,
} from '@/services/schedulerService';
import { mergeAdminScheduledJobRows } from '@/utils/ocSchedulerAdminJobs';

const { t, locale } = useAppI18n();

const loading = ref(true);
const runningKey = ref<string | null>(null);
const errorLocal = ref<string | null>(null);
const infoLocal = ref<string | null>(null);
const rows = ref<OcAdminScheduledJobRow[]>([]);
const filterScope = ref<'all' | 'system' | 'domain'>('all');
const filterActiveOnly = ref(false);

const executionsDialog = ref(false);
const executionsTarget = ref<OcAdminScheduledJobRow | null>(null);

const headers = computed(() => [
  { title: t('operationCore.adminScheduledJobs.colSource'), key: 'sourceLabel', sortable: true },
  { title: t('operationCore.adminScheduledJobs.colScope'), key: 'scope', sortable: true },
  { title: t('operationCore.adminScheduledJobs.colJobId'), key: 'jobId', sortable: true },
  { title: t('operationCore.adminScheduledJobs.colCron'), key: 'cronExpression', sortable: false },
  { title: t('operationCore.adminScheduledJobs.colActive'), key: 'isActive', sortable: true },
  { title: t('operationCore.adminScheduledJobs.colLastRun'), key: 'lastRunAt', sortable: true },
  { title: t('operationCore.adminScheduledJobs.colLastStatus'), key: 'lastStatus', sortable: true },
  { title: t('operationCore.adminScheduledJobs.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const filteredRows = computed(() => {
  let list = rows.value;
  if (filterScope.value !== 'all') {
    list = list.filter((r) => r.scope === filterScope.value);
  }
  if (filterActiveOnly.value) {
    list = list.filter((r) => r.isActive);
  }
  return list;
});

const stats = computed(() => ({
  total: rows.value.length,
  active: rows.value.filter((r) => r.isActive).length,
  system: rows.value.filter((r) => r.scope === 'system').length,
  domain: rows.value.filter((r) => r.scope === 'domain').length,
}));

function formatDate(value?: string | null): string {
  if (!value) return '—';
  try {
    return new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      dateStyle: 'short',
      timeStyle: 'medium',
    }).format(new Date(value));
  } catch {
    return value;
  }
}

function scopeLabel(scope: OcAdminScheduledJobRow['scope']): string {
  return scope === 'system'
    ? t('operationCore.adminScheduledJobs.scopeSystem')
    : t('operationCore.adminScheduledJobs.scopeDomain');
}

function statusColor(status?: string | null): string {
  if (!status) return 'default';
  if (status.includes('completed_with_errors')) return 'warning';
  if (status === 'success' || status === 'completed') return 'success';
  if (status === 'failed' || status === 'timeout') return 'error';
  return 'warning';
}

async function loadRows() {
  loading.value = true;
  errorLocal.value = null;
  try {
    const { systemJobs, userJobs, scheduleById } = await schedulerLoadAdminJobExplorerRows();
    rows.value = mergeAdminScheduledJobRows(systemJobs, userJobs, scheduleById);
  } catch (e) {
    errorLocal.value =
      e instanceof Error ? e.message : t('operationCore.adminScheduledJobs.loadError');
  } finally {
    loading.value = false;
  }
}

async function runJob(row: OcAdminScheduledJobRow) {
  if (!row.canRunManually) return;
  runningKey.value = row.key;
  errorLocal.value = null;
  infoLocal.value = null;
  try {
    if (row.runKind === 'oc-execute' && row.ocScheduleId) {
      await schedulerRunOcSchedule(row.ocScheduleId);
      infoLocal.value = t('operationCore.adminScheduledJobs.runOcSuccess');
    } else if (row.runKind === 'http-post') {
      const result = await schedulerRunHttpPostJob(row.endpointUrl, '{}');
      const summary =
        typeof result === 'object' && result && 'status' in result
          ? String((result as { status?: string }).status)
          : null;
      infoLocal.value = summary
        ? t('operationCore.adminScheduledJobs.runHttpSuccess', { status: summary })
        : t('operationCore.adminScheduledJobs.runHttpSuccessGeneric');
    }
    await loadRows();
  } catch (e) {
    errorLocal.value =
      e instanceof Error ? e.message : t('operationCore.adminScheduledJobs.runError');
  } finally {
    runningKey.value = null;
  }
}

function openExecutions(row: OcAdminScheduledJobRow) {
  executionsTarget.value = row;
  executionsDialog.value = true;
}

function onExecutionsError(message: string) {
  errorLocal.value = message;
}

onMounted(() => {
  void loadRows();
});
</script>

<template>
  <div class="oc-admin-scheduled-jobs">
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>
    <v-alert v-if="infoLocal" type="success" variant="tonal" class="mb-4" closable @click:close="infoLocal = null">
      {{ infoLocal }}
    </v-alert>

    <div class="d-flex flex-wrap align-center ga-3 mb-4">
      <v-chip size="small" variant="outlined">
        {{ t('operationCore.adminScheduledJobs.statTotal', { count: stats.total }) }}
      </v-chip>
      <v-chip size="small" variant="outlined" color="success">
        {{ t('operationCore.adminScheduledJobs.statActive', { count: stats.active }) }}
      </v-chip>
      <v-chip size="small" variant="outlined">
        {{ t('operationCore.adminScheduledJobs.statSystem', { count: stats.system }) }}
      </v-chip>
      <v-chip size="small" variant="outlined">
        {{ t('operationCore.adminScheduledJobs.statDomain', { count: stats.domain }) }}
      </v-chip>
      <v-spacer />
      <v-btn
        variant="outlined"
        prepend-icon="mdi-refresh"
        :loading="loading"
        @click="loadRows"
      >
        {{ t('operationCore.adminScheduledJobs.refresh') }}
      </v-btn>
    </div>

    <div class="d-flex flex-wrap ga-3 mb-4">
      <v-btn-toggle v-model="filterScope" mandatory density="compact" color="primary">
        <v-btn value="all">{{ t('operationCore.adminScheduledJobs.filterAll') }}</v-btn>
        <v-btn value="system">{{ t('operationCore.adminScheduledJobs.filterSystem') }}</v-btn>
        <v-btn value="domain">{{ t('operationCore.adminScheduledJobs.filterDomain') }}</v-btn>
      </v-btn-toggle>
      <v-switch
        v-model="filterActiveOnly"
        :label="t('operationCore.adminScheduledJobs.filterActiveOnly')"
        density="compact"
        hide-details
        color="primary"
      />
    </div>

    <v-data-table
      :headers="headers"
      :items="filteredRows"
      :loading="loading"
      item-value="key"
      class="oc-admin-scheduled-jobs__table rounded-lg"
      density="comfortable"
    >
      <template #item.scope="{ item }">
        <v-chip size="small" :color="item.scope === 'system' ? 'primary' : 'secondary'" variant="tonal">
          {{ scopeLabel(item.scope) }}
        </v-chip>
      </template>

      <template #item.jobId="{ item }">
        <code class="text-caption">{{ item.jobId }}</code>
      </template>

      <template #item.isActive="{ item }">
        <v-chip size="small" :color="item.isActive ? 'success' : 'default'" variant="tonal">
          {{
            item.isActive
              ? t('operationCore.adminScheduledJobs.activeYes')
              : t('operationCore.adminScheduledJobs.activeNo')
          }}
        </v-chip>
      </template>

      <template #item.lastRunAt="{ item }">
        {{ formatDate(item.lastRunAt) }}
      </template>

      <template #item.lastStatus="{ item }">
        <v-chip v-if="item.lastStatus" size="small" :color="statusColor(item.lastStatus)" variant="tonal">
          {{ item.lastStatus }}
        </v-chip>
        <span v-else class="text-medium-emphasis">—</span>
        <div v-if="item.lastError" class="text-caption text-error mt-1 text-truncate" style="max-width: 220px">
          {{ item.lastError }}
        </div>
      </template>

      <template #item.actions="{ item }">
        <div class="d-flex justify-end ga-1">
          <v-btn
            v-if="item.canRunManually"
            size="small"
            variant="tonal"
            color="primary"
            :loading="runningKey === item.key"
            prepend-icon="mdi-play-circle-outline"
            @click="runJob(item)"
          >
            {{ t('operationCore.adminScheduledJobs.runNow') }}
          </v-btn>
          <v-btn
            size="small"
            variant="text"
            icon="mdi-history"
            :title="t('operationCore.adminScheduledJobs.viewExecutions')"
            @click="openExecutions(item)"
          />
        </div>
      </template>

      <template #no-data>
        <div class="text-center pa-6 text-medium-emphasis">
          {{ t('operationCore.adminScheduledJobs.empty') }}
        </div>
      </template>
    </v-data-table>

    <OcAdminScheduledJobExecutionsDialog
      v-model="executionsDialog"
      :job="executionsTarget"
      @error="onExecutionsError"
    />
  </div>
</template>

<style scoped>
.oc-admin-scheduled-jobs__table :deep(code) {
  font-size: 0.75rem;
}
</style>
