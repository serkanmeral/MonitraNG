<script setup lang="ts">
import { computed } from 'vue';
import ConfigurationShell from '@/components/apps/configuration/ConfigurationShell.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import ConfigurationMonitoringSection from '@/components/apps/configuration/ConfigurationMonitoringSection.vue';
import ConfigurationAssetTypesSection from '@/components/apps/configuration/ConfigurationAssetTypesSection.vue';
import ConfigurationWidgetsSection from '@/components/apps/configuration/ConfigurationWidgetsSection.vue';
import ConfigurationHTTPAuthSection from '@/components/apps/configuration/ConfigurationHTTPAuthSection.vue';

definePageMeta({ layout: 'default' });

const route = useRoute();
const section = computed(() => (route.query.section as string) || 'monitoring');

const nuxtApp = useNuxtApp();
function t(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const page = { title: t('monitoringConfig.title', 'İzleme Yapılandırması') };
const breadcrumbs = [
  { text: t('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: t('monitoring.pageTitle', 'Monitoring tanımları'), disabled: false, href: '/apps/monitoring' },
  { text: t('monitoringConfig.title', 'İzleme Yapılandırması'), disabled: true, href: '#' },
];

const currentComponent = computed(() => {
  if (section.value === 'asset-types') return ConfigurationAssetTypesSection;
  if (section.value === 'widgets') return ConfigurationWidgetsSection;
  if (section.value === 'http-auth') return ConfigurationHTTPAuthSection;
  return ConfigurationMonitoringSection;
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
    <ConfigurationShell>
      <component :is="currentComponent" />
    </ConfigurationShell>
  </div>
</template>
