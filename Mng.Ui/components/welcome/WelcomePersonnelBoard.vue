<script setup lang="ts">
import { computed, onMounted, provide, ref, watch } from 'vue';
import DashboardLayoutRenderer from '@/components/dashboards/DashboardLayoutRenderer.vue';
import DashboardSurfaceToolbar from '@/components/dashboards/DashboardSurfaceToolbar.vue';
import { useDashboardStore } from '@/stores/apps/dashboard';
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
import { istanbulTrailingDaysRange, readSurfaceVariables } from '@/utils/widgets/dashboardSurfaceVariables';
import { ensurePersonnelDashboard, PERSONNEL_DASHBOARD_SLUG } from '@/services/personnelDashboard';

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string) => {
  if (i18n?.t) return i18n.t(key);
  if (i18n?.global?.t) return i18n.global.t(key);
  return key;
};

const dashboardStore = useDashboardStore();
const authStore = useAuthStore();
const ready = ref(false);
const failed = ref(false);
const errorText = ref('');

const { personId, rangeFrom, rangeTo, calendarRange, context: surfaceContext, mutations: surfaceMutations } =
  useDashboardSurfaceContext('24h');

const surfaceVariableDefs = computed(() => readSurfaceVariables(dashboardStore.currentDashboard?.layout?.meta));
const toolbarVariables = computed(() => surfaceVariableDefs.value.filter((item) => item.kind !== 'person'));
const selfPersonId = computed(() => authStore.userInfo?.mng_person_id?.trim() || '');

const layout = computed(() => dashboardStore.currentDashboard?.layout ?? null);
const layoutWidgetIds = computed(() => collectWidgetIdsFromLayout(layout.value));

const { dataByWidgetId, loading: batchLoading, refresh: refreshBatch } = useDashboardWidgetBatch(
  layoutWidgetIds,
  surfaceContext,
);

provide(DASHBOARD_SURFACE_CONTEXT_KEY, surfaceContext);
provide(DASHBOARD_SURFACE_MUTATIONS_KEY, surfaceMutations);
provide(DASHBOARD_WIDGET_DATA_KEY, dataByWidgetId);
provide(DASHBOARD_WIDGET_BATCH_MODE_KEY, true);

function applyOpeningRange() {
  const span = istanbulTrailingDaysRange(7);
  rangeFrom.value = span.from;
  rangeTo.value = span.to;
  calendarRange.value = true;
}

applyOpeningRange();

watch(
  () => dashboardStore.currentDashboard?.__dataId || dashboardStore.currentDashboard?.dataId,
  () => {
    if (surfaceVariableDefs.value.some((item) => item.kind === 'dateRange')) applyOpeningRange();
    personId.value = selfPersonId.value || null;
  },
);

watch(selfPersonId, (id) => {
  personId.value = id || null;
});

onMounted(async () => {
  try {
    await ensurePersonnelDashboard();
    await dashboardStore.fetchDashboardBySlug(PERSONNEL_DASHBOARD_SLUG);
    ready.value = true;
  } catch (error) {
    errorText.value = error instanceof Error ? error.message : 'Pano açılamadı';
    failed.value = true;
  }
});
</script>

<template>
  <v-container v-if="ready && layout" class="welcome-personnel pt-0">
    <div class="text-h6 mb-1">{{ t('welcome.personnel.title') }}</div>
    <p class="text-body-2 text-medium-emphasis mb-3">{{ t('welcome.personnel.hint') }}</p>
    <DashboardSurfaceToolbar
      time-preset="24h"
      :severity="null"
      workspace-id=""
      :refresh-seconds="0"
      :loading="batchLoading"
      :variables="toolbarVariables"
      :person-id="personId"
      :range-from="rangeFrom"
      :range-to="rangeTo"
      person-locked
      :t="t"
      @update:range-from="rangeFrom = $event"
      @update:range-to="rangeTo = $event"
      @refresh="refreshBatch()"
    />
    <DashboardLayoutRenderer class="mt-4" :rows="layout.rows" :surface-context="surfaceContext" :t="t" />
  </v-container>
  <v-container v-else-if="failed" class="pt-0">
    <v-alert type="warning" variant="tonal" density="compact">
      {{ errorText || t('welcome.personnel.title') }}
    </v-alert>
  </v-container>
</template>
