<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  SCHEDULER_EXECUTION_HISTORY_LIMIT,
  schedulerGetSystemJobExecutions,
  schedulerGetUserJobExecutions,
} from '@/services/schedulerService';
import type { OcAdminScheduledJobRow, OcSchedulerExecutionRow } from '@/types/apps/scheduler';
import { buildAdminJobFallbackExecutionRows, mapExecutionToAdminRow } from '@/utils/ocSchedulerAdminJobs';

const props = defineProps<{
  modelValue: boolean;
  job: OcAdminScheduledJobRow | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  error: [message: string];
}>();

const { t, locale } = useAppI18n();

const loading = ref(false);
const rows = ref<OcSchedulerExecutionRow[]>([]);
const expanded = ref<string[]>([]);
const usingFallback = ref(false);

const headers = computed(() => [
  { title: '', key: 'data-table-expand', sortable: false, width: 48 },
  { title: t('operationCore.adminScheduledJobs.executionsColTime'), key: 'executedAt', sortable: false },
  { title: t('operationCore.adminScheduledJobs.executionsColStatus'), key: 'displayStatus', sortable: false },
  { title: t('operationCore.adminScheduledJobs.executionsColHttp'), key: 'responseCode', sortable: false, width: 88 },
  { title: t('operationCore.adminScheduledJobs.executionsColDuration'), key: 'responseTimeMs', sortable: false, width: 96 },
]);

watch(
  () => [props.modelValue, props.job?.key] as const,
  ([open]) => {
    if (open && props.job) {
      void loadExecutions(props.job);
    } else if (!open) {
      rows.value = [];
      expanded.value = [];
      usingFallback.value = false;
    }
  }
);

async function loadExecutions(job: OcAdminScheduledJobRow) {
  loading.value = true;
  rows.value = [];
  expanded.value = [];
  usingFallback.value = false;
  try {
    const raw =
      job.scope === 'system'
        ? await schedulerGetSystemJobExecutions(job.jobId)
        : await schedulerGetUserJobExecutions(job.jobId);
    rows.value = raw.map(mapExecutionToAdminRow);
    if (rows.value.length === 0) {
      rows.value = buildAdminJobFallbackExecutionRows(job);
      usingFallback.value = rows.value.length > 0;
    }
  } catch {
    rows.value = buildAdminJobFallbackExecutionRows(job);
    usingFallback.value = rows.value.length > 0;
    if (!usingFallback.value) {
      emit('error', t('operationCore.adminScheduledJobs.executionsError'));
    }
  } finally {
    loading.value = false;
    const firstWithErrors = rows.value.find((row) => row.errors.length > 0);
    if (firstWithErrors) {
      expanded.value = [firstWithErrors.executionId];
    }
  }
}

function close() {
  emit('update:modelValue', false);
}

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

function formatDuration(ms?: number | null): string {
  if (ms == null) return '—';
  if (ms >= 1000) return `${(ms / 1000).toFixed(1)} s`;
  return `${ms} ms`;
}

function statusIcon(tone: OcSchedulerExecutionRow['statusTone']): string {
  if (tone === 'success') return 'mdi-check-circle';
  if (tone === 'warning') return 'mdi-alert-circle-outline';
  if (tone === 'error') return 'mdi-close-circle';
  return 'mdi-information-outline';
}

function formatResponseBody(body: string | null): string {
  if (!body) return '';
  try {
    return JSON.stringify(JSON.parse(body), null, 2);
  } catch {
    return body;
  }
}
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="920"
    scrollable
    @update:model-value="emit('update:modelValue', $event)"
  >
    <v-card class="oc-scheduled-executions-dialog">
      <v-card-title class="d-flex align-center ga-2 py-4 px-5">
        <v-icon icon="mdi-history" color="primary" />
        <div class="flex-grow-1">
          <div class="text-h6">{{ t('operationCore.adminScheduledJobs.executionsTitle') }}</div>
          <div v-if="job" class="text-body-2 text-medium-emphasis mt-1">
            {{ job.name }} · <code class="oc-scheduled-executions-dialog__job-id">{{ job.jobId }}</code>
          </div>
        </div>
        <v-btn icon="mdi-close" variant="text" @click="close" />
      </v-card-title>

      <v-divider />

      <v-card-text class="px-5 py-4">
        <div class="d-flex flex-wrap align-center ga-2 mb-4">
          <v-chip size="small" variant="tonal" color="primary" prepend-icon="mdi-format-list-numbered">
            {{
              usingFallback
                ? t('operationCore.adminScheduledJobs.executionsFallbackNote')
                : t('operationCore.adminScheduledJobs.executionsLimitNote', {
                    limit: SCHEDULER_EXECUTION_HISTORY_LIMIT,
                  })
            }}
          </v-chip>
          <v-chip v-if="rows.length" size="small" variant="outlined">
            {{ t('operationCore.adminScheduledJobs.executionsCount', { count: rows.length }) }}
          </v-chip>
        </div>

        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3 rounded" />

        <v-data-table
          v-else-if="rows.length"
          v-model:expanded="expanded"
          :headers="headers"
          :items="rows"
          item-value="executionId"
          show-expand
          hide-default-footer
          density="comfortable"
          class="oc-scheduled-executions-dialog__table rounded-lg border"
        >
          <template #item.executedAt="{ item }">
            <span class="text-body-2">{{ formatDate(item.executedAt) }}</span>
          </template>

          <template #item.displayStatus="{ item }">
            <div>
              <v-chip
                size="small"
                :color="item.statusTone"
                variant="tonal"
                :prepend-icon="statusIcon(item.statusTone)"
              >
                {{ item.displayStatus }}
              </v-chip>
              <div
                v-if="item.schedulerStatus && item.schedulerStatus !== item.displayStatus"
                class="text-caption text-medium-emphasis mt-1"
              >
                {{
                  t('operationCore.adminScheduledJobs.executionsSchedulerStatus', {
                    status: item.schedulerStatus,
                  })
                }}
              </div>
            </div>
          </template>

          <template #item.responseCode="{ item }">
            <v-chip
              v-if="item.responseCode != null"
              size="x-small"
              :color="item.responseCode >= 400 ? 'error' : 'default'"
              variant="outlined"
            >
              {{ item.responseCode }}
            </v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>

          <template #item.responseTimeMs="{ item }">
            <span class="text-body-2 text-medium-emphasis">{{ formatDuration(item.responseTimeMs) }}</span>
          </template>

          <template #expanded-row="{ columns, item }">
            <tr>
              <td :colspan="columns.length" class="oc-scheduled-executions-dialog__expanded px-4 py-3">
                <div v-if="item.errors.length" class="mb-3">
                  <div class="text-caption font-weight-medium mb-2">
                    {{ t('operationCore.adminScheduledJobs.executionsErrorsTitle') }}
                  </div>
                  <v-alert
                    v-for="(err, idx) in item.errors"
                    :key="idx"
                    type="error"
                    variant="tonal"
                    density="compact"
                    class="mb-2 text-pre-wrap"
                  >
                    {{ err }}
                  </v-alert>
                </div>

                <div v-if="item.summary && !item.errors.length" class="text-body-2 text-medium-emphasis mb-3">
                  {{ item.summary }}
                </div>

                <v-expansion-panels v-if="item.responseBody" variant="accordion" density="compact">
                  <v-expansion-panel>
                    <v-expansion-panel-title class="text-body-2">
                      {{ t('operationCore.adminScheduledJobs.executionsResponseBody') }}
                    </v-expansion-panel-title>
                    <v-expansion-panel-text>
                      <pre class="oc-scheduled-executions-dialog__body">{{ formatResponseBody(item.responseBody) }}</pre>
                    </v-expansion-panel-text>
                  </v-expansion-panel>
                </v-expansion-panels>
              </td>
            </tr>
          </template>
        </v-data-table>

        <v-empty-state
          v-else-if="!loading"
          icon="mdi-calendar-blank-outline"
          :title="t('operationCore.adminScheduledJobs.executionsEmpty')"
        />
      </v-card-text>

      <v-divider />

      <v-card-actions class="px-5 py-3">
        <v-spacer />
        <v-btn variant="text" @click="close">
          {{ t('operationCore.adminScheduledJobs.executionsClose') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.oc-scheduled-executions-dialog__job-id {
  font-size: 0.8125rem;
}

.oc-scheduled-executions-dialog__table :deep(thead th) {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgba(var(--v-theme-on-surface), 0.6);
}

.oc-scheduled-executions-dialog__expanded {
  background: rgba(var(--v-theme-on-surface), 0.03);
}

.oc-scheduled-executions-dialog__body {
  margin: 0;
  padding: 12px;
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.04);
  font-size: 0.75rem;
  line-height: 1.45;
  overflow-x: auto;
  white-space: pre-wrap;
  word-break: break-word;
}

.text-pre-wrap {
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
