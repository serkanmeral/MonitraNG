<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';
import type { WorkflowApprovalSummary } from '@/types/apps/workflow';
import { workflowDecideApproval, workflowListApprovals } from '@/services/workflowService';

const { t, locale } = useAppI18n();
const auth = useAuthStore();

const loading = ref(true);
const decidingId = ref<string | null>(null);
const errorLocal = ref<string | null>(null);
const infoLocal = ref<string | null>(null);
const rows = ref<WorkflowApprovalSummary[]>([]);
const pendingOnly = ref(true);
const commentById = ref<Record<string, string>>({});

const headers = computed(() => [
  { title: t('operationCore.adminApprovals.colCreated'), key: 'createdAt', sortable: true },
  { title: t('operationCore.adminApprovals.colWorkflow'), key: 'workflowId', sortable: true },
  { title: t('operationCore.adminApprovals.colNode'), key: 'nodeId', sortable: false },
  { title: t('operationCore.adminApprovals.colTarget'), key: 'approverTarget', sortable: false },
  { title: t('operationCore.adminApprovals.colStatus'), key: 'status', sortable: true },
  { title: t('operationCore.adminApprovals.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

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

function statusLabel(status: WorkflowApprovalSummary['status']): string {
  if (status === 'Pending') return t('operationCore.adminApprovals.statusPending');
  if (status === 'Approved') return t('operationCore.adminApprovals.statusApproved');
  return t('operationCore.adminApprovals.statusRejected');
}

function statusColor(status: WorkflowApprovalSummary['status']): string {
  if (status === 'Pending') return 'warning';
  if (status === 'Approved') return 'success';
  return 'error';
}

async function loadRows() {
  loading.value = true;
  errorLocal.value = null;
  try {
    rows.value = await workflowListApprovals(pendingOnly.value ? 'Pending' : undefined);
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('operationCore.adminApprovals.loadError');
    rows.value = [];
  } finally {
    loading.value = false;
  }
}

async function decide(row: WorkflowApprovalSummary, approved: boolean) {
  decidingId.value = row.id;
  errorLocal.value = null;
  infoLocal.value = null;
  try {
    await workflowDecideApproval(row.id, {
      approved,
      comment: commentById.value[row.id]?.trim() || undefined,
      decidedBy: auth.userInfo?.preferred_username || auth.userInfo?.sub,
    });
    infoLocal.value = t('operationCore.adminApprovals.decideSuccess');
    await loadRows();
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('operationCore.adminApprovals.decideError');
  } finally {
    decidingId.value = null;
  }
}

onMounted(() => {
  void loadRows();
});
</script>

<template>
  <div>
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>
    <v-alert v-if="infoLocal" type="success" variant="tonal" class="mb-4" closable @click:close="infoLocal = null">
      {{ infoLocal }}
    </v-alert>

    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <v-btn-toggle v-model="pendingOnly" mandatory density="compact" color="primary" @update:model-value="loadRows">
        <v-btn :value="true">{{ t('operationCore.adminApprovals.filterPending') }}</v-btn>
        <v-btn :value="false">{{ t('operationCore.adminApprovals.filterAll') }}</v-btn>
      </v-btn-toggle>
      <v-spacer />
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="loadRows">
        {{ t('operationCore.adminApprovals.refresh') }}
      </v-btn>
    </div>

    <v-data-table
      :headers="headers"
      :items="rows"
      :loading="loading"
      item-value="id"
      class="rounded-lg"
      density="comfortable"
    >
      <template #item.createdAt="{ item }">
        {{ formatDate(item.createdAt) }}
      </template>
      <template #item.workflowId="{ item }">
        <span class="text-caption font-weight-medium">{{ item.workflowId }}</span>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item.status)" variant="tonal">
          {{ statusLabel(item.status) }}
        </v-chip>
      </template>
      <template #item.actions="{ item }">
        <div v-if="item.status === 'Pending'" class="d-flex flex-column align-end gap-2 py-2" style="min-width: 220px">
          <v-text-field
            v-model="commentById[item.id]"
            density="compact"
            hide-details
            variant="outlined"
            :label="t('operationCore.adminApprovals.comment')"
          />
          <div class="d-flex gap-2">
            <v-btn
              size="small"
              color="success"
              variant="flat"
              :loading="decidingId === item.id"
              @click="decide(item, true)"
            >
              {{ t('operationCore.adminApprovals.approve') }}
            </v-btn>
            <v-btn
              size="small"
              color="error"
              variant="outlined"
              :loading="decidingId === item.id"
              @click="decide(item, false)"
            >
              {{ t('operationCore.adminApprovals.reject') }}
            </v-btn>
          </div>
        </div>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">
          {{ t('operationCore.adminApprovals.empty') }}
        </div>
      </template>
    </v-data-table>
  </div>
</template>
