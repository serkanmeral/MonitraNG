<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useWelcomeMenuAccess } from '@/composables/useWelcomeMenuAccess';
import { alarmDashboardSnapshot } from '@/services/alarmService';

const authStore = useAuthStore();
const { hasPrefix } = useWelcomeMenuAccess();

const loading = ref(false);
const count = ref(0);
const visible = ref(false);

onMounted(async () => {
  const canSeeAlarms =
    (authStore.isManager || authStore.isAdmin) &&
    (hasPrefix('/apps/alarm-center') || hasPrefix('/apps/monitoring'));
  if (!canSeeAlarms) return;

  loading.value = true;
  try {
    const snapshot = await alarmDashboardSnapshot({ rangeHours: 24, openLimit: 1 });
    count.value = snapshot.openTotal ?? 0;
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
        <v-icon icon="mdi-bell-alert-outline" color="error" size="22" />
        <span class="text-subtitle-2 font-weight-bold">
          {{ $t('welcome.actions.activeAlarms.title') }}
        </span>
      </div>
      <v-skeleton-loader v-if="loading" type="text" />
      <template v-else>
        <p class="text-h5 font-weight-bold mb-2">
          {{ $t('welcome.actions.activeAlarms.count', { count }) }}
        </p>
        <v-btn
          color="error"
          variant="tonal"
          size="small"
          rounded="lg"
          class="text-none"
          to="/apps/alarm-center/alarms"
        >
          {{ $t('welcome.actions.activeAlarms.viewAll') }}
          <v-icon icon="mdi-chevron-right" end />
        </v-btn>
      </template>
    </v-card-text>
  </v-card>
</template>
