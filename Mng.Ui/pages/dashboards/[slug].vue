<script setup lang="ts">
import { computed, onMounted, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DashboardLayoutRenderer from '@/components/dashboards/DashboardLayoutRenderer.vue';
import { useDashboardStore } from '@/stores/apps/dashboard';
import type { DashboardLayout } from '@/stores/apps/dashboard';

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
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

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

    <v-card v-if="notFound" variant="outlined" class="py-12">
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

    <template v-else-if="layout">
      <dashboards-dashboard-layout-renderer
        :rows="layout.rows"
        :t="t"
      />
    </template>

    <v-card v-else variant="outlined" class="py-8">
      <div class="text-center text-medium-emphasis">
        {{ t('dashboards.view.noLayout') }}
      </div>
    </v-card>
  </div>
</template>
