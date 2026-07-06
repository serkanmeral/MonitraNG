<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useWelcomeMenuAccess } from '@/composables/useWelcomeMenuAccess';
import { tmListDataset, TM_DATASETS } from '@/services/taskManagerService';
import { assigneeUserId } from '@/composables/useTaskManagerHelpers';

const authStore = useAuthStore();
const { hasPrefix } = useWelcomeMenuAccess();

const loading = ref(false);
const count = ref<number | null>(null);
const visible = ref(false);

onMounted(async () => {
  if (!hasPrefix('/apps/task-manager')) return;

  const uid = authStore.userInfo?.sub ?? '';
  if (!uid) return;

  visible.value = true;
  loading.value = true;

  try {
    const raw = await tmListDataset(TM_DATASETS.issues, {
      limit: 200,
      sort: 'order:asc',
    });
    const items = Array.isArray(raw) ? raw : [];
    count.value = items.filter((row) => assigneeUserId((row as Record<string, unknown>).assignee) === uid).length;
  } catch {
    count.value = null;
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <v-card v-if="visible" class="action-widget rounded-xl h-100" variant="outlined">
    <v-card-text class="pa-4">
      <div class="d-flex align-center gap-2 mb-2">
        <v-icon icon="mdi-clipboard-check-outline" color="primary" size="22" />
        <span class="text-subtitle-2 font-weight-bold">
          {{ $t('welcome.actions.assignedTasks.title') }}
        </span>
      </div>
      <v-skeleton-loader v-if="loading" type="text" />
      <template v-else>
        <p v-if="count !== null && count > 0" class="text-h5 font-weight-bold mb-2">
          {{ $t('welcome.actions.assignedTasks.count', { count }) }}
        </p>
        <p v-else class="text-body-2 text-medium-emphasis mb-2">
          {{ $t('welcome.actions.assignedTasks.empty') }}
        </p>
        <v-btn
          color="primary"
          variant="tonal"
          size="small"
          rounded="lg"
          class="text-none"
          to="/apps/task-manager/assigned"
        >
          {{ $t('welcome.actions.assignedTasks.viewAll') }}
          <v-icon icon="mdi-chevron-right" end />
        </v-btn>
      </template>
    </v-card-text>
  </v-card>
</template>
