<script setup lang="ts">
import { computed, onMounted, watch } from 'vue';
import DashboardBuilder from '@/components/dashboards/builder/DashboardBuilder.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useDashboardStore } from '@/stores/apps/dashboard';

definePageMeta({
  layout: 'default',
});

const route = useRoute();
const router = useRouter();
const dashboardStore = useDashboardStore();

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties?.$i18n;
const t = (key: string) => (i18n?.t ?? i18n?.global?.t)?.(key) ?? key;

const id = computed(() => (route.params.id as string) ?? '');
const initial = computed(() => dashboardStore.currentDashboard);

const page = computed(() => ({ title: t('dashboards.builder.editTitle') }));
const breadcrumbs = computed(() => [
  { text: t('dashboards.breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('dashboards.breadcrumbs.dashboards'), disabled: false, href: '/apps/dashboards' },
  { text: page.value.title, disabled: true, href: '#' },
]);

async function load() {
  if (!id.value) return;
  try {
    await dashboardStore.fetchDashboardById(id.value);
  } catch {
    dashboardStore.resetCurrent();
    router.replace('/apps/dashboards');
  }
}

onMounted(load);
watch(id, load);
</script>

<template>
  <div>
    <template v-if="initial">
      <DashboardBuilder :initial="initial" />
    </template>
    <template v-else>
      <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
      <div v-if="dashboardStore.loading" class="d-flex justify-center align-center py-12">
        <v-progress-circular indeterminate color="primary" size="48" />
      </div>
      <div v-else class="d-flex justify-center align-center py-12">
        <v-alert type="warning" variant="tonal">
          {{ t('dashboards.view.notFound') }}
          <v-btn variant="text" color="primary" class="ml-2" @click="router.push('/apps/dashboards')">
            {{ t('dashboards.view.backToList') }}
          </v-btn>
        </v-alert>
      </div>
    </template>
  </div>
</template>
