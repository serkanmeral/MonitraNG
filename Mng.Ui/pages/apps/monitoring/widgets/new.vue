<script setup lang="ts">
import { computed } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import MonitoringWidgetForm from '@/components/apps/monitoring/MonitoringWidgetForm.vue';
import { useWidgetStore, type CreateWidgetDto } from '@/stores/apps/widget';

definePageMeta({ layout: 'default' });

const router = useRouter();
const widgetStore = useWidgetStore();

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const page = computed(() => ({ title: mt('monitoring.widgets.newWidget', 'Yeni Monitoring Widget') }));
const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('monitoring.pageTitle', 'Monitoring tanımları'), disabled: false, href: '/apps/monitoring' },
  { text: mt('monitoring.widgets.pageTitle', 'Monitoring Widget\'ları'), disabled: false, href: '/apps/monitoring/widgets' },
  { text: page.value.title, disabled: true, href: '#' },
]);

async function handleSubmit(dto: CreateWidgetDto) {
  try {
    await widgetStore.createWidget(dto);
    router.push('/apps/monitoring/widgets');
  } catch {
    /* store sets error */
  }
}

function handleCancel() {
  router.push('/apps/monitoring/widgets');
}
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
    <v-container fluid>
      <v-alert v-if="widgetStore.error" type="error" variant="tonal" dismissible class="mb-4" @click:close="widgetStore.clearError">
        {{ widgetStore.error }}
      </v-alert>
      <MonitoringWidgetForm :t="mt" @submit="handleSubmit" @cancel="handleCancel" />
    </v-container>
  </div>
</template>
