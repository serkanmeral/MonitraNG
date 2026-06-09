<script setup lang="ts">
import { computed, onMounted, watch, ref, provide, onUnmounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DashboardLayoutRenderer from '@/components/dashboards/DashboardLayoutRenderer.vue';
import DashboardSurfaceToolbar from '@/components/dashboards/DashboardSurfaceToolbar.vue';
import DashboardSnapshotExportMenu from '@/components/dashboards/DashboardSnapshotExportMenu.vue';
import { useDashboardStore } from '@/stores/apps/dashboard';
import { useDashboardPermissions } from '@/composables/useDashboardPermissions';
import { useAuthStore } from '@/stores/auth';
import { useDashboardSurfaceContext } from '@/composables/useDashboardSurfaceContext';
import { useDashboardWidgetBatch } from '@/composables/useDashboardWidgetBatch';
import { collectWidgetIdsFromLayout } from '@/utils/widgets/dashboardLayoutUtils';
import {
  DASHBOARD_SURFACE_CONTEXT_KEY,
  DASHBOARD_WIDGET_BATCH_MODE_KEY,
  DASHBOARD_WIDGET_DATA_KEY,
} from '@/utils/widgets/dashboardSurfaceKeys';
import { DASHBOARD_SURFACE_MUTATIONS_KEY } from '@/utils/widgets/dashboardSurfaceMutations';
import type { DashboardLayout, WidgetConfigOverrides } from '@/stores/apps/dashboard';
import { isSiemCenterDashboardMeta } from '@/types/apps/siemDashboardSurface';

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

const {
  timePreset,
  severity,
  workspaceId,
  customTimeRange,
  crossFilterVariables,
  context: surfaceContext,
  mutations: surfaceMutations,
} = useDashboardSurfaceContext('24h');
const refreshSeconds = ref(0);
let refreshTimer: ReturnType<typeof setInterval> | null = null;

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

const siemCenterSurface = computed(() => {
  const meta = dashboardStore.currentDashboard?.layout?.meta;
  return isSiemCenterDashboardMeta(meta);
});

const redirectingSiemCenter = ref(false);

const layoutWidgetIds = computed(() => collectWidgetIdsFromLayout(layout.value));

const activeCrossFilters = computed(() => {
  const vars = crossFilterVariables.value;
  return Object.entries(vars).filter(([, v]) => v !== null && v !== undefined && v !== '');
});

function clearCrossFilter(name: string) {
  surfaceMutations.clearCrossFilterVariable(name);
}

const {
  dataByWidgetId,
  loading: batchLoading,
  error: batchError,
  refresh: refreshBatch,
} = useDashboardWidgetBatch(layoutWidgetIds, surfaceContext);

provide(DASHBOARD_SURFACE_CONTEXT_KEY, surfaceContext);
provide(DASHBOARD_SURFACE_MUTATIONS_KEY, surfaceMutations);
provide(DASHBOARD_WIDGET_DATA_KEY, dataByWidgetId);
provide(DASHBOARD_WIDGET_BATCH_MODE_KEY, true);

const refreshIntervalMs = computed(() => {
  const sec = refreshSeconds.value;
  if (sec > 0) return sec * 1000;
  return null;
});

provide('dashboardRefreshInterval', refreshIntervalMs);

const notFound = computed(() => !dashboardStore.loading && !dashboardStore.currentDashboard && !!slug.value);

const hasViewPermission = computed(() => {
  const dashboard = dashboardStore.currentDashboard;
  if (!dashboard) return true;
  return canViewDashboard(dashboard.permissions);
});

const unauthorized = computed(() => {
  return !dashboardStore.loading && dashboardStore.currentDashboard && !hasViewPermission.value;
});

async function load() {
  if (!slug.value) return;
  redirectingSiemCenter.value = false;
  try {
    await dashboardStore.fetchDashboardBySlug(slug.value);
    const d = dashboardStore.currentDashboard;
    if (d && isSiemCenterDashboardMeta(d.layout?.meta) && !d.layout?.rows?.length) {
      redirectingSiemCenter.value = true;
      await router.replace('/apps/siem-center');
    }
  } catch {
    dashboardStore.resetCurrent();
  }
}

onMounted(load);
watch(slug, load);

function goToList() {
  router.push('/apps/dashboards');
}

const canEdit = computed(() => {
  const dashboard = dashboardStore.currentDashboard;
  if (!dashboard) return false;
  if (authStore.isAdmin || authStore.userInfo?.is_manager) {
    return canEditDashboard(dashboard.permissions);
  }
  return false;
});

const dashboardId = computed(() => {
  return dashboardStore.currentDashboard?.__dataId ?? dashboardStore.currentDashboard?.dataId ?? '';
});

function goToEdit() {
  if (dashboardId.value) {
    router.push(`/apps/dashboards/${dashboardId.value}/edit`);
  }
}

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

function setupRefreshTimer() {
  if (refreshTimer) {
    clearInterval(refreshTimer);
    refreshTimer = null;
  }
  const ms = refreshIntervalMs.value;
  if (ms && ms > 0) {
    refreshTimer = setInterval(() => refreshBatch(), ms);
  }
}

watch(refreshIntervalMs, setupRefreshTimer, { immediate: true });

onUnmounted(() => {
  if (refreshTimer) clearInterval(refreshTimer);
});
</script>

<template>
  <div>
    <div class="d-flex flex-wrap justify-space-between align-start ga-3 mb-4">
      <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
      <div class="d-flex flex-wrap ga-2 align-center justify-end" style="flex: 1;">
        <DashboardSurfaceToolbar
          v-if="layout && hasViewPermission"
          v-model:time-preset="timePreset"
          v-model:severity="severity"
          v-model:workspace-id="workspaceId"
          v-model:refresh-seconds="refreshSeconds"
          :loading="batchLoading"
          :t="t"
          @refresh="refreshBatch()"
        />
        <DashboardSnapshotExportMenu
          v-if="layout && hasViewPermission && dashboardStore.currentDashboard"
          :dashboard="dashboardStore.currentDashboard"
          :widget-ids="layoutWidgetIds"
          :context="surfaceContext"
          :data-by-widget-id="dataByWidgetId"
          :disabled="batchLoading"
          :t="t"
        />
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

    <v-alert
      v-if="batchError"
      type="warning"
      variant="tonal"
      density="compact"
      class="mb-4"
    >
      {{ batchError }}
    </v-alert>

    <div v-if="activeCrossFilters.length || customTimeRange" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip
        v-if="customTimeRange"
        size="small"
        color="primary"
        variant="tonal"
        closable
        @click:close="timePreset = '24h'"
      >
        {{ t('dashboards.surface.zoomRange') }}
      </v-chip>
      <v-chip
        v-for="[name, value] in activeCrossFilters"
        :key="name"
        size="small"
        color="secondary"
        variant="tonal"
        closable
        @click:close="clearCrossFilter(name)"
      >
        {{ name }}: {{ value }}
      </v-chip>
    </div>

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

    <template v-else-if="dashboardStore.loading && !dashboardStore.currentDashboard">
      <v-card variant="outlined" class="py-12">
        <div class="text-center text-medium-emphasis">
          {{ t('dashboards.view.loading') }}
        </div>
      </v-card>
    </template>

    <template v-else-if="layout && hasViewPermission">
      <dashboards-dashboard-layout-renderer
        :rows="layout.rows"
        :can-edit="canEdit"
        :surface-context="surfaceContext"
        :t="t"
        @update:widget-overrides="onWidgetOverridesChange"
      />
    </template>

    <v-card v-else-if="hasViewPermission && redirectingSiemCenter" variant="outlined" class="py-8">
      <div class="text-center text-medium-emphasis">
        {{ t('dashboards.view.redirectingSiemCenter') }}
      </div>
    </v-card>

    <v-card v-else-if="hasViewPermission && siemCenterSurface" variant="outlined" class="py-8">
      <div class="text-center">
        <p class="text-body-1 text-medium-emphasis mb-4">
          {{ t('dashboards.view.siemCenterSurfaceHint') }}
        </p>
        <v-btn color="primary" variant="flat" to="/apps/siem-center">
          {{ t('dashboards.view.openSiemCenter') }}
        </v-btn>
      </div>
    </v-card>

    <v-card v-else-if="hasViewPermission" variant="outlined" class="py-8">
      <div class="text-center text-medium-emphasis">
        {{ t('dashboards.view.noLayout') }}
      </div>
    </v-card>
  </div>
</template>
