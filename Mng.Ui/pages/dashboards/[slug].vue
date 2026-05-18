<script setup lang="ts">
import { computed, onMounted, watch, ref, provide, onUnmounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DashboardLayoutRenderer from '@/components/dashboards/DashboardLayoutRenderer.vue';
import { useDashboardStore } from '@/stores/apps/dashboard';
import { useDashboardPermissions } from '@/composables/useDashboardPermissions';
import { useAuthStore } from '@/stores/auth';
import type { DashboardLayout, WidgetConfigOverrides } from '@/stores/apps/dashboard';

const route = useRoute();
const router = useRouter();
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string) => {
  if (i18n?.t) return i18n.t(key);
  if (i18n?.global?.t) return i18n.global.t(key);
  return key;
};

const dashboardStore = useDashboardStore();
const { canViewDashboard, canEditDashboard } = useDashboardPermissions();
const authStore = useAuthStore();
const slug = computed(() => (route.params.slug as string) ?? '');

const page = computed(() => ({
  title: dashboardStore.currentDashboard?.title ?? t('dashboards.view.loading'),
}));

const breadcrumbs = computed(() => [
  { text: t('dashboards.breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  {
    text: dashboardStore.currentDashboard?.title ?? slug.value ?? t('dashboards.breadcrumbs.dashboards'),
    disabled: true,
    href: '#',
  },
]);

const layout = computed<DashboardLayout | null>(() => {
  const d = dashboardStore.currentDashboard;
  if (!d?.layout?.rows?.length) return null;
  return d.layout;
});

const notFound = computed(() => !dashboardStore.loading && !dashboardStore.currentDashboard && !!slug.value);

// Permission kontrolü
const hasViewPermission = computed(() => {
  const dashboard = dashboardStore.currentDashboard;
  if (!dashboard) return true; // Dashboard yüklenene kadar true (loading state)
  return canViewDashboard(dashboard.permissions);
});

const unauthorized = computed(() => {
  return !dashboardStore.loading && dashboardStore.currentDashboard && !hasViewPermission.value;
});

async function load() {
  if (!slug.value) return;
  try {
    await dashboardStore.fetchDashboardBySlug(slug.value);
  } catch {
    dashboardStore.resetCurrent();
  }
}

onMounted(load);
watch(slug, load);

function goToList() {
  router.push('/apps/dashboards');
}

// Edit permission kontrolü
const canEdit = computed(() => {
  const dashboard = dashboardStore.currentDashboard;
  if (!dashboard) return false;
  // Admin veya manager ise edit butonu göster
  if (authStore.isAdmin || authStore.userInfo?.is_manager) {
    return canEditDashboard(dashboard.permissions);
  }
  return false;
});

// Dashboard ID'sini al (edit sayfasına yönlendirmek için)
const dashboardId = computed(() => {
  return dashboardStore.currentDashboard?.__dataId ?? dashboardStore.currentDashboard?.dataId ?? '';
});

function goToEdit() {
  if (dashboardId.value) {
    router.push(`/apps/dashboards/${dashboardId.value}/edit`);
  }
}

// Refresh time settings
const refreshTimeUnit = ref<'seconds' | 'minutes'>('seconds');
const refreshTimeValue = ref<number>(30);

// Calculate refresh interval in milliseconds
const refreshIntervalMs = computed(() => {
  if (!refreshTimeValue.value || refreshTimeValue.value <= 0) return null;
  if (refreshTimeUnit.value === 'seconds') {
    return refreshTimeValue.value * 1000;
  } else {
    return refreshTimeValue.value * 60 * 1000;
  }
});

// Provide refresh interval to child components
provide('dashboardRefreshInterval', refreshIntervalMs);

// Widget ayarları override (dashboard görünümünde değiştirilebilir)
async function onWidgetOverridesChange(payload: {
  rowIdx: number;
  colIdx: number;
  overrides: WidgetConfigOverrides;
}) {
  const d = dashboardStore.currentDashboard;
  if (!d?.layout?.rows || !canEdit.value) return;
  const rows = JSON.parse(JSON.stringify(d.layout.rows));
  const row = rows[payload.rowIdx];
  if (!row?.cols?.[payload.colIdx]) return;
  row.cols[payload.colIdx] = {
    ...row.cols[payload.colIdx],
    widgetOverrides: payload.overrides,
  };
  try {
    await dashboardStore.updateDashboard(d.__dataId ?? d.dataId ?? '', { layout: { type: 'rows', rows } });
  } catch {
    /* store sets error */
  }
}
</script>

<template>
  <div>
    <div class="d-flex justify-space-between align-center mb-4">
      <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
      <div class="d-flex ga-2 align-center">
        <!-- Refresh Time Settings -->
        <v-card v-if="layout && hasViewPermission" variant="outlined" class="pa-2">
          <div class="d-flex align-center ga-2">
            <v-icon size="20" color="primary">mdi-refresh</v-icon>
            <span class="text-body-2 text-medium-emphasis mr-2">
              {{ t('dashboards.view.refreshTime') || 'Yenileme:' }}
            </span>
            <v-text-field
              v-model.number="refreshTimeValue"
              type="number"
              min="1"
              variant="outlined"
              density="compact"
              hide-details
              style="width: 80px;"
              class="mr-2"
            ></v-text-field>
            <v-select
              v-model="refreshTimeUnit"
              :items="[
                { title: t('dashboards.view.refreshUnit.seconds') || 'Saniye', value: 'seconds' },
                { title: t('dashboards.view.refreshUnit.minutes') || 'Dakika', value: 'minutes' },
              ]"
              variant="outlined"
              density="compact"
              hide-details
              style="width: 120px;"
            ></v-select>
          </div>
        </v-card>
        <v-btn
          v-if="canEdit && dashboardStore.currentDashboard"
          color="primary"
          variant="flat"
          prepend-icon="mdi-pencil"
          @click="goToEdit"
        >
          {{ t('dashboards.view.edit') }}
        </v-btn>
      </div>
    </div>

    <v-alert
      v-if="dashboardStore.error"
      type="error"
      variant="tonal"
      closable
      class="mb-4"
      @click:close="dashboardStore.clearError"
    >
      {{ dashboardStore.error }}
    </v-alert>

    <!-- Unauthorized (403) -->
    <v-card v-if="unauthorized" variant="outlined" class="py-12">
      <div class="text-center">
        <v-icon size="64" color="error" class="mb-4">mdi-lock-alert</v-icon>
        <p class="text-h6 text-medium-emphasis mb-2">
          {{ t('dashboards.view.unauthorized') }}
        </p>
        <p class="text-body-2 text-medium-emphasis mb-4">
          {{ t('dashboards.view.unauthorizedMessage') }}
        </p>
        <v-btn color="primary" variant="flat" @click="goToList">
          {{ t('dashboards.view.backToList') }}
        </v-btn>
      </div>
    </v-card>

    <!-- Not Found (404) -->
    <v-card v-else-if="notFound" variant="outlined" class="py-12">
      <div class="text-center">
        <p class="text-h6 text-medium-emphasis mb-4">
          {{ t('dashboards.view.notFound') }}
        </p>
        <v-btn color="primary" variant="flat" @click="goToList">
          {{ t('dashboards.view.backToList') }}
        </v-btn>
      </div>
    </v-card>

    <!-- Loading -->
    <template v-else-if="dashboardStore.loading && !dashboardStore.currentDashboard">
      <v-card variant="outlined" class="py-12">
        <div class="text-center text-medium-emphasis">
          {{ t('dashboards.view.loading') }}
        </div>
      </v-card>
    </template>

    <!-- Dashboard Content -->
    <template v-else-if="layout && hasViewPermission">
      <dashboards-dashboard-layout-renderer
        :rows="layout.rows"
        :can-edit="canEdit"
        :t="t"
        @update:widget-overrides="onWidgetOverridesChange"
      />
    </template>

    <!-- No Layout -->
    <v-card v-else-if="hasViewPermission" variant="outlined" class="py-8">
      <div class="text-center text-medium-emphasis">
        {{ t('dashboards.view.noLayout') }}
      </div>
    </v-card>
  </div>
</template>
