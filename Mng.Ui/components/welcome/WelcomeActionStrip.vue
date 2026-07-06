<script setup lang="ts">
import { computed, ref, onMounted } from 'vue';
import WelcomePendingApprovals from '@/components/welcome/actions/WelcomePendingApprovals.vue';
import WelcomeActiveAlarms from '@/components/welcome/actions/WelcomeActiveAlarms.vue';
import WelcomeAssignedTasks from '@/components/welcome/actions/WelcomeAssignedTasks.vue';
import WelcomeLicenseStatus from '@/components/welcome/actions/WelcomeLicenseStatus.vue';
import WelcomeRecentPages from '@/components/welcome/actions/WelcomeRecentPages.vue';
import { useAuthStore } from '@/stores/auth';
import { useSideMenuStore } from '@/stores/apps/sideMenu';
import { useRecentPages } from '@/composables/useRecentPages';

const authStore = useAuthStore();
const menuStore = useSideMenuStore();
const { refreshRecentPages } = useRecentPages();

const menuReady = ref(false);

onMounted(async () => {
  await menuStore.loadMenuItems(false);
  refreshRecentPages();
  menuReady.value = true;
});

const isManagerOrAdmin = computed(() => authStore.isManager || authStore.isAdmin);
const isAdmin = computed(() => authStore.isAdmin);

/** Rol bazlı widget sırası */
const widgetOrder = computed(() => {
  if (isAdmin.value) {
    return ['license', 'approvals', 'alarms', 'assigned', 'recent'] as const;
  }
  if (isManagerOrAdmin.value) {
    return ['approvals', 'alarms', 'assigned', 'recent'] as const;
  }
  return ['assigned', 'recent'] as const;
});

const showSection = computed(() => menuReady.value);

/** Devam et kartı her zaman gösterilir (boş durum mesajı ile) */
const showRecentWidget = computed(() => menuReady.value);
</script>

<template>
  <v-container v-if="showSection" fluid class="px-4 px-md-6 pt-0 pb-2">
    <h2 class="text-subtitle-1 font-weight-bold text-medium-emphasis mb-3">
      {{ $t('welcome.actions.title') }}
    </h2>
    <v-row>
      <template v-for="widget in widgetOrder" :key="widget">
        <v-col
          v-if="widget === 'license' && isAdmin"
          cols="12"
          sm="6"
          md="4"
        >
          <WelcomeLicenseStatus />
        </v-col>
        <v-col
          v-else-if="widget === 'approvals' && isManagerOrAdmin"
          cols="12"
          sm="6"
          md="4"
        >
          <WelcomePendingApprovals />
        </v-col>
        <v-col
          v-else-if="widget === 'alarms' && isManagerOrAdmin"
          cols="12"
          sm="6"
          md="4"
        >
          <WelcomeActiveAlarms />
        </v-col>
        <v-col
          v-else-if="widget === 'assigned'"
          cols="12"
          sm="6"
          md="4"
        >
          <WelcomeAssignedTasks />
        </v-col>
        <v-col
          v-else-if="widget === 'recent' && showRecentWidget"
          cols="12"
          sm="6"
          md="4"
        >
          <WelcomeRecentPages />
        </v-col>
      </template>
    </v-row>
  </v-container>
</template>
