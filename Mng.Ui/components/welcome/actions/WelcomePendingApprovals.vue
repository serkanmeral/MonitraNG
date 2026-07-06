<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { workflowListApprovals } from '@/services/workflowService';
import type { WorkflowApprovalSummary } from '@/types/apps/workflow';

const authStore = useAuthStore();
const loading = ref(false);
const count = ref(0);
const visible = ref(false);

onMounted(async () => {
  if (!authStore.isManager && !authStore.isAdmin) return;

  loading.value = true;
  try {
    const rows: WorkflowApprovalSummary[] = await workflowListApprovals('Pending');
    count.value = rows.length;
    visible.value = count.value > 0;
  } catch {
    visible.value = false;
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <v-card
    v-if="visible || loading"
    class="action-widget rounded-xl h-100"
    variant="outlined"
  >
    <v-card-text class="pa-4">
      <div class="d-flex align-center gap-2 mb-2">
        <v-icon icon="mdi-check-decagram-outline" color="warning" size="22" />
        <span class="text-subtitle-2 font-weight-bold">
          {{ $t('welcome.actions.pendingApprovals.title') }}
        </span>
      </div>
      <v-skeleton-loader v-if="loading" type="text" />
      <template v-else>
        <p class="text-h5 font-weight-bold mb-2">
          {{ $t('welcome.actions.pendingApprovals.count', { count }) }}
        </p>
        <v-btn
          color="warning"
          variant="tonal"
          size="small"
          rounded="lg"
          class="text-none"
          to="/apps/operation-core/approvals"
        >
          {{ $t('welcome.actions.pendingApprovals.viewAll') }}
          <v-icon icon="mdi-chevron-right" end />
        </v-btn>
      </template>
    </v-card-text>
  </v-card>
</template>
